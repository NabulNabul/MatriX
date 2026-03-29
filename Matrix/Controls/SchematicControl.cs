using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Matrix.Models;

namespace Matrix.Controls
{
    public class SchematicControl : Panel
    {
        private Dictionary<CircuitComponent, Rect> _componentBounds = new();
        private CircuitComponent? _hoveredComponent;
        private Border _editorOverlay;
        private NumericUpDown _editorNumeric;
        private Button _editorRemove;
        public static readonly StyledProperty<IEnumerable<CircuitComponent>?> CircuitProperty =
            AvaloniaProperty.Register<SchematicControl, IEnumerable<CircuitComponent>?>(nameof(Circuit));

        public IEnumerable<CircuitComponent>? Circuit
        {
            get => GetValue(CircuitProperty);
            set => SetValue(CircuitProperty, value);
        }

        public SchematicControl()
        {
            ClipToBounds = true;

            // 호버 에디터 (값 변경 숫자 입력기)
            _editorNumeric = new NumericUpDown 
            { 
                FormatString = "F2", 
                Increment = 0.1m, 
                Minimum = 0m,
                Width = 90,
                Height = 30,
                Margin = new Thickness(0, 0, 5, 0)
            };
            _editorNumeric.ValueChanged += OnEditorValueChanged;

            // 호버 에디터 (삭제 버튼)
            _editorRemove = new Button 
            { 
                Content = "삭제", 
                Background = Brushes.IndianRed, 
                Foreground = Brushes.White,
                Height = 30
            };
            _editorRemove.Click += OnEditorRemoveClick;

            var sp = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal };
            sp.Children.Add(_editorNumeric);
            sp.Children.Add(_editorRemove);

            _editorOverlay = new Border 
            { 
                Child = sp, 
                Background = new SolidColorBrush(Color.FromArgb(240, 255, 255, 255)),
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(5),
                IsVisible = false,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
                ZIndex = 100
            };

            Children.Add(_editorOverlay);

            this.PointerMoved += OnPointerMoved;
            this.PointerExited += OnPointerExited;

            this.GetObservable(CircuitProperty).Subscribe(circuit =>
            {
                if (circuit is INotifyCollectionChanged obs)
                {
                    obs.CollectionChanged -= OnCollectionChanged;
                    obs.CollectionChanged += OnCollectionChanged;
                }
                AttachPropertyChangedListeners(circuit);
                InvalidateVisual();
            });
        }

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            AttachPropertyChangedListeners(Circuit);
            InvalidateVisual();
        }

        private void AttachPropertyChangedListeners(IEnumerable<CircuitComponent>? circuit)
        {
            if (circuit == null) return;
            foreach (var comp in circuit)
            {
                if (comp is INotifyPropertyChanged inpc)
                {
                    inpc.PropertyChanged -= OnComponentPropertyChanged;
                    inpc.PropertyChanged += OnComponentPropertyChanged;
                }
            }
        }

        private void OnComponentPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Value" || e.PropertyName == "Type")
            {
                InvalidateVisual();
            }
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            var point = e.GetPosition(this);
            
            // 에디터 패널 안에서 마우스가 움직일 때는 닫히지 않도록 유지
            if (_editorOverlay.IsVisible && _editorOverlay.Bounds.Contains(point))
                return;

            CircuitComponent? found = null;
            foreach (var kv in _componentBounds)
            {
                var rect = kv.Value.Inflate(15); // 인식 영역을 살짝 확장하여 호버를 편하게 함
                if (rect.Contains(point))
                {
                    found = kv.Key;
                    break;
                }
            }

            // 호버 대상이 변경된 경우 에디터 오버레이 표시 업데이트
            if (found != _hoveredComponent)
            {
                _hoveredComponent = found;
                if (_hoveredComponent != null)
                {
                    _editorNumeric.Value = (decimal)_hoveredComponent.Value;
                    var rect = _componentBounds[_hoveredComponent];
                    _editorOverlay.Margin = new Thickness(rect.X - 20, rect.Bottom + 5, 0, 0);
                    _editorOverlay.IsVisible = true;
                }
                else
                {
                    _editorOverlay.IsVisible = false;
                }
                InvalidateVisual();
            }
        }

        private void OnPointerExited(object? sender, PointerEventArgs e)
        {
            var point = e.GetPosition(this);
            if (!_editorOverlay.Bounds.Contains(point))
            {
                _hoveredComponent = null;
                _editorOverlay.IsVisible = false;
                InvalidateVisual();
            }
        }

        private void OnEditorRemoveClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (_hoveredComponent != null && Circuit is System.Collections.IList list)
            {
                list.Remove(_hoveredComponent);
                _hoveredComponent = null;
                _editorOverlay.IsVisible = false;
                InvalidateVisual();
            }
        }

        private void OnEditorValueChanged(object? sender, Avalonia.Controls.NumericUpDownValueChangedEventArgs e)
        {
            if (_hoveredComponent != null && e.NewValue.HasValue)
            {
                _hoveredComponent.Value = (double)e.NewValue.Value;
                InvalidateVisual();
            }
        }

        private void DrawSeriesCapacitor(DrawingContext context, IPen pen, double x, double y, double width)
        {
            double gap = 6;
            double h = 12;
            context.DrawLine(pen, new Point(x, y), new Point(x + width / 2 - gap / 2, y));
            context.DrawLine(pen, new Point(x + width / 2 - gap / 2, y - h), new Point(x + width / 2 - gap / 2, y + h));
            context.DrawLine(pen, new Point(x + width / 2 + gap / 2, y - h), new Point(x + width / 2 + gap / 2, y + h));
            context.DrawLine(pen, new Point(x + width / 2 + gap / 2, y), new Point(x + width, y));
        }

        private void DrawShuntCapacitor(DrawingContext context, IPen pen, double x, double y, double height)
        {
            double gap = 6;
            double w = 12;
            context.DrawLine(pen, new Point(x, y), new Point(x, y + height / 2 - gap / 2));
            context.DrawLine(pen, new Point(x - w, y + height / 2 - gap / 2), new Point(x + w, y + height / 2 - gap / 2));
            context.DrawLine(pen, new Point(x - w, y + height / 2 + gap / 2), new Point(x + w, y + height / 2 + gap / 2));
            context.DrawLine(pen, new Point(x, y + height / 2 + gap / 2), new Point(x, y + height));
        }

        private void DrawSeriesInductor(DrawingContext context, IPen pen, double x, double y, double width)
        {
            double loopW = width / 3;
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(new Point(x, y), false);
                ctx.ArcTo(new Point(x + loopW, y), new Size(loopW / 2, 8), 0, false, SweepDirection.Clockwise);
                ctx.ArcTo(new Point(x + loopW * 2, y), new Size(loopW / 2, 8), 0, false, SweepDirection.Clockwise);
                ctx.ArcTo(new Point(x + width, y), new Size(loopW / 2, 8), 0, false, SweepDirection.Clockwise);
            }
            context.DrawGeometry(null, pen, geometry);
        }

        private void DrawShuntInductor(DrawingContext context, IPen pen, double x, double y, double height)
        {
            double loopH = height / 3;
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(new Point(x, y), false);
                ctx.ArcTo(new Point(x, y + loopH), new Size(8, loopH / 2), 0, false, SweepDirection.CounterClockwise);
                ctx.ArcTo(new Point(x, y + loopH * 2), new Size(8, loopH / 2), 0, false, SweepDirection.CounterClockwise);
                ctx.ArcTo(new Point(x, y + height), new Size(8, loopH / 2), 0, false, SweepDirection.CounterClockwise);
            }
            context.DrawGeometry(null, pen, geometry);
        }

        private void DrawText(DrawingContext context, string text, Point center, IBrush brush, double size = 10, bool centerAlign = false)
        {
            var formattedText = new FormattedText(
                text,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                size,
                brush);

            Point textOrigin = centerAlign
                ? new Point(center.X - formattedText.Width / 2, center.Y - formattedText.Height / 2)
                : new Point(center.X, center.Y - formattedText.Height / 2);

            context.DrawText(formattedText, textOrigin);
        }
    }
}
