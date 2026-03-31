using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Matrix.Models;

namespace Matrix.Controls
{
    public class SchematicControl : Control
    {
        private readonly Dictionary<CircuitComponent, Rect> _componentBounds = [];
        private readonly Border _editorOverlay;
        private readonly NumericUpDown _editorNumeric;
        private readonly Button _editorRemove;
        private CircuitComponent? _hoveredComponent;
        private bool _editorFocused = false;

        private const double WireY   = 90;
        private const double CellW   = 110;
        private const double SymbolW = 66;
        private const double ShuntH  = 70;
        private const double LeadW   = (CellW - SymbolW) / 2;
        private const double StartX  = 28;

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

            _editorNumeric = new NumericUpDown
            {
                FormatString = "F2",
                Increment    = 0.1m,
                Minimum      = 0m,
                Width        = 140,
                Height       = 30,
                Margin       = new Thickness(0, 0, 5, 0)
            };
            _editorNumeric.ValueChanged += OnEditorValueChanged;
            _editorNumeric.GotFocus   += (_, _) => _editorFocused = true;
            _editorNumeric.LostFocus  += (_, _) => { _editorFocused = false; TryHideOverlay(); };

            _editorRemove = new Button
            {
                Content    = "삭제",
                Background = Brushes.IndianRed,
                Foreground = Brushes.White,
                Height     = 30
            };
            _editorRemove.Click += OnEditorRemoveClick;

            var sp = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal };
            sp.Children.Add(_editorNumeric);
            sp.Children.Add(_editorRemove);

            _editorOverlay = new Border
            {
                Child               = sp,
                Background          = new SolidColorBrush(Color.FromArgb(240, 30, 30, 30)),
                BorderBrush         = new SolidColorBrush(Colors.CornflowerBlue),
                BorderThickness     = new Thickness(1),
                CornerRadius        = new CornerRadius(4),
                Padding             = new Thickness(5),
                IsVisible           = false,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                VerticalAlignment   = Avalonia.Layout.VerticalAlignment.Top,
                ZIndex              = 100
            };

            LogicalChildren.Add(_editorOverlay);
            VisualChildren.Add(_editorOverlay);

            PointerMoved  += OnPointerMoved;
            PointerExited += OnPointerExited;

            this.GetObservable(CircuitProperty).Subscribe(circuit =>
            {
                if (circuit is INotifyCollectionChanged obs)
                {
                    obs.CollectionChanged -= OnCollectionChanged;
                    obs.CollectionChanged += OnCollectionChanged;
                }
                AttachPropertyChangedListeners(circuit);
                InvalidateMeasure();
                InvalidateVisual();
            });
        }

        // ── 레이아웃 ──────────────────────────────────────────────────

        protected override Size MeasureOverride(Size availableSize)
        {
            var components = Circuit?.ToList() ?? [];
            bool hasShunt  = components.Any(c =>
                c.Type is ComponentType.ShuntInductor or ComponentType.ShuntCapacitor);

            double height = hasShunt ? WireY + 10 + ShuntH + 75 : WireY + 38;
            double width  = System.Math.Max(220, StartX * 2 + System.Math.Max(components.Count, 1) * CellW);

            _editorOverlay.Measure(availableSize);
            return new Size(width, height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            _editorOverlay.Arrange(new Rect(finalSize));
            return finalSize;
        }

        // ── 렌더링 ────────────────────────────────────────────────────

        public override void Render(DrawingContext context)
        {
            base.Render(context);
            _componentBounds.Clear();

            var components = Circuit?.ToList() ?? [];

            IBrush strokeBrush = Brushes.White;
            if (Application.Current?.TryGetResource(
                    "SystemControlForegroundBaseHighBrush", ActualThemeVariant, out var res) == true
                && res is IBrush rb)
                strokeBrush = rb;

            IBrush accentBrush = Brushes.CornflowerBlue;
            IBrush portBrush   = new SolidColorBrush(Color.Parse("#4CAF50"));
            IBrush dimBrush    = new SolidColorBrush(Color.FromArgb(120,
                ((SolidColorBrush)strokeBrush).Color.R,
                ((SolidColorBrush)strokeBrush).Color.G,
                ((SolidColorBrush)strokeBrush).Color.B));

            var wirePen      = new Pen(strokeBrush, 2);
            var highlightPen = new Pen(accentBrush, 2.5);

            double totalWidth = Bounds.Width > 0
                ? Bounds.Width
                : StartX * 2 + System.Math.Max(components.Count, 1) * CellW;

            if (components.Count == 0)
            {
                context.DrawLine(wirePen,
                    new Point(StartX, WireY), new Point(totalWidth - StartX, WireY));
                DrawPortDot(context, portBrush, StartX, WireY);
                DrawPortDot(context, portBrush, totalWidth - StartX, WireY);
                DrawText(context, "← 아래에서 소자를 추가하세요",
                    new Point(totalWidth / 2, WireY - 18), dimBrush, 11, true);
                return;
            }

            DrawPortDot(context, portBrush, StartX, WireY);
            DrawText(context, "IN", new Point(StartX, WireY + 8), portBrush, 9, true);

            double x = StartX;
            foreach (var comp in components)
            {
                bool isHovered  = comp == _hoveredComponent;
                var  compPen    = isHovered ? highlightPen : wirePen;
                var  compBrush  = isHovered ? accentBrush  : strokeBrush;

                if (comp.Type is ComponentType.SeriesInductor or ComponentType.SeriesCapacitor)
                    RenderSeriesComponent(context, comp, x, wirePen, compPen, compBrush);
                else
                    RenderShuntComponent(context, comp, x, wirePen, compPen, compBrush);

                x += CellW;
            }

            context.DrawLine(wirePen, new Point(x, WireY), new Point(x + StartX, WireY));
            DrawPortDot(context, portBrush, x + StartX, WireY);
            DrawText(context, "OUT", new Point(x + StartX, WireY + 8), portBrush, 9, true);
        }

        private void RenderSeriesComponent(DrawingContext context, CircuitComponent comp,
            double x, IPen wirePen, IPen compPen, IBrush compBrush)
        {
            context.DrawLine(wirePen, new Point(x, WireY), new Point(x + LeadW, WireY));
            context.DrawLine(wirePen, new Point(x + LeadW + SymbolW, WireY), new Point(x + CellW, WireY));

            if (comp.Type == ComponentType.SeriesInductor)
                DrawSeriesInductor(context, compPen, x + LeadW, WireY, SymbolW);
            else
                DrawSeriesCapacitor(context, compPen, x + LeadW, WireY, SymbolW);

            _componentBounds[comp] = new Rect(x + LeadW - 4, WireY - 16, SymbolW + 8, 32);

            string unit  = comp.Type == ComponentType.SeriesInductor ? "nH" : "pF";
            string sym   = comp.Type == ComponentType.SeriesInductor ? "L" : "C";
            string type  = comp.Type == ComponentType.SeriesInductor ? "Series L" : "Series C";
            DrawText(context, $"{sym} = {comp.Value:F1} {unit}",
                new Point(x + CellW / 2, WireY - 28), compBrush, 10, true);
            DrawText(context, type,
                new Point(x + CellW / 2, WireY + 18), compBrush, 9, true);
        }

        private void RenderShuntComponent(DrawingContext context, CircuitComponent comp,
            double x, IPen wirePen, IPen compPen, IBrush compBrush)
        {
            context.DrawLine(wirePen, new Point(x, WireY), new Point(x + CellW, WireY));

            double cx      = x + CellW / 2;
            double branchY = WireY + 10;
            context.DrawLine(wirePen, new Point(cx, WireY), new Point(cx, branchY));

            if (comp.Type == ComponentType.ShuntInductor)
                DrawShuntInductor(context, compPen, cx, branchY, ShuntH);
            else
                DrawShuntCapacitor(context, compPen, cx, branchY, ShuntH);

            DrawGround(context, wirePen, cx, branchY + ShuntH);

            _componentBounds[comp] = new Rect(cx - 20, branchY - 4, 40, ShuntH + 8);

            double labelY = branchY + ShuntH / 2 - 10;
            string unit   = comp.Type == ComponentType.ShuntInductor ? "nH" : "pF";
            string sym    = comp.Type == ComponentType.ShuntInductor ? "L" : "C";
            string type   = comp.Type == ComponentType.ShuntInductor ? "Shunt L" : "Shunt C";
            DrawText(context, $"{sym} = {comp.Value:F1} {unit}",
                new Point(cx + 22, labelY), compBrush, 10, false);
            DrawText(context, type,
                new Point(cx + 22, labelY + 14), compBrush, 9, false);
        }

        // ── 회로 기호 드로잉 ─────────────────────────────────────────

        private static void DrawSeriesCapacitor(DrawingContext context, IPen pen, double x, double y, double width)
        {
            double gap = 6, h = 12;
            context.DrawLine(pen, new Point(x,                      y), new Point(x + width / 2 - gap / 2, y));
            context.DrawLine(pen, new Point(x + width / 2 - gap / 2, y - h), new Point(x + width / 2 - gap / 2, y + h));
            context.DrawLine(pen, new Point(x + width / 2 + gap / 2, y - h), new Point(x + width / 2 + gap / 2, y + h));
            context.DrawLine(pen, new Point(x + width / 2 + gap / 2, y), new Point(x + width, y));
        }

        private static void DrawShuntCapacitor(DrawingContext context, IPen pen, double x, double y, double height)
        {
            double gap = 6, w = 12;
            context.DrawLine(pen, new Point(x, y),             new Point(x, y + height / 2 - gap / 2));
            context.DrawLine(pen, new Point(x - w, y + height / 2 - gap / 2), new Point(x + w, y + height / 2 - gap / 2));
            context.DrawLine(pen, new Point(x - w, y + height / 2 + gap / 2), new Point(x + w, y + height / 2 + gap / 2));
            context.DrawLine(pen, new Point(x, y + height / 2 + gap / 2), new Point(x, y + height));
        }

        private static void DrawSeriesInductor(DrawingContext context, IPen pen, double x, double y, double width)
        {
            double loopW = width / 3;
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(new Point(x, y), false);
                ctx.ArcTo(new Point(x + loopW,     y), new Size(loopW / 2, 8), 0, false, SweepDirection.Clockwise);
                ctx.ArcTo(new Point(x + loopW * 2, y), new Size(loopW / 2, 8), 0, false, SweepDirection.Clockwise);
                ctx.ArcTo(new Point(x + width,      y), new Size(loopW / 2, 8), 0, false, SweepDirection.Clockwise);
            }
            context.DrawGeometry(null, pen, geometry);
        }

        private static void DrawShuntInductor(DrawingContext context, IPen pen, double x, double y, double height)
        {
            double loopH = height / 3;
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(new Point(x, y), false);
                ctx.ArcTo(new Point(x, y + loopH),     new Size(8, loopH / 2), 0, false, SweepDirection.CounterClockwise);
                ctx.ArcTo(new Point(x, y + loopH * 2), new Size(8, loopH / 2), 0, false, SweepDirection.CounterClockwise);
                ctx.ArcTo(new Point(x, y + height),    new Size(8, loopH / 2), 0, false, SweepDirection.CounterClockwise);
            }
            context.DrawGeometry(null, pen, geometry);
        }

        private static void DrawGround(DrawingContext context, IPen pen, double x, double y)
        {
            context.DrawLine(pen, new Point(x - 12, y),      new Point(x + 12, y));
            context.DrawLine(pen, new Point(x - 8,  y + 5),  new Point(x + 8,  y + 5));
            context.DrawLine(pen, new Point(x - 4,  y + 10), new Point(x + 4,  y + 10));
        }

        private static void DrawPortDot(DrawingContext context, IBrush brush, double x, double y)
            => context.DrawEllipse(brush, null, new Point(x, y), 4, 4);

        private static void DrawText(DrawingContext context, string text, Point center,
            IBrush brush, double size = 10, bool centerAlign = false)
        {
            var ft = new FormattedText(
                text,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                size,
                brush);
            Point origin = centerAlign
                ? new Point(center.X - ft.Width / 2, center.Y - ft.Height / 2)
                : new Point(center.X, center.Y - ft.Height / 2);
            context.DrawText(ft, origin);
        }

        // ── 이벤트 핸들러 ─────────────────────────────────────────────

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            AttachPropertyChangedListeners(Circuit);
            InvalidateMeasure();
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
            if (e.PropertyName is "Value" or "Type")
                InvalidateVisual();
        }

        // 에디터 or 컴포넌트 영역을 포함하는 결합 Rect 안에 있는지 확인
        private bool IsInsideEditorZone(Point point)
        {
            if (_editorOverlay.IsVisible && _editorOverlay.Bounds.Contains(point))
                return true;

            if (_hoveredComponent != null &&
                _componentBounds.TryGetValue(_hoveredComponent, out var cb))
            {
                var ob = _editorOverlay.Bounds;
                var combined = new Rect(
                    System.Math.Min(cb.Left,   ob.Left)   - 15,
                    System.Math.Min(cb.Top,    ob.Top)    - 10,
                    System.Math.Max(cb.Right,  ob.Right)  - System.Math.Min(cb.Left, ob.Left) + 30,
                    System.Math.Max(cb.Bottom, ob.Bottom) - System.Math.Min(cb.Top,  ob.Top)  + 20);
                if (combined.Contains(point)) return true;
            }

            return false;
        }

        private void TryHideOverlay()
        {
            if (_editorFocused) return;
            _hoveredComponent        = null;
            _editorOverlay.IsVisible = false;
            InvalidateVisual();
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            var point = e.GetPosition(this);

            // 편집 중이거나 컴포넌트↔오버레이 결합 영역 안이면 숨기지 않음
            if (_editorFocused) return;
            if (IsInsideEditorZone(point)) return;

            CircuitComponent? found = null;
            foreach (var kv in _componentBounds)
            {
                if (kv.Value.Inflate(8).Contains(point))
                {
                    found = kv.Key;
                    break;
                }
            }

            if (found == _hoveredComponent) return;

            _hoveredComponent = found;
            if (_hoveredComponent != null)
            {
                _editorNumeric.Value = (decimal)_hoveredComponent.Value;
                var rect = _componentBounds[_hoveredComponent];
                _editorOverlay.Margin    = new Thickness(System.Math.Max(0, rect.X - 10), rect.Bottom + 6, 0, 0);
                _editorOverlay.IsVisible = true;
            }
            else
            {
                _editorOverlay.IsVisible = false;
            }
            InvalidateVisual();
        }

        private void OnPointerExited(object? sender, PointerEventArgs e)
        {
            if (_editorFocused) return;
            TryHideOverlay();
        }

        private void OnEditorRemoveClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (_hoveredComponent != null && Circuit is System.Collections.IList list)
            {
                list.Remove(_hoveredComponent);
                _hoveredComponent        = null;
                _editorOverlay.IsVisible = false;
                InvalidateVisual();
            }
        }

        private void OnEditorValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
        {
            if (_hoveredComponent != null && e.NewValue.HasValue)
            {
                _hoveredComponent.Value = (double)e.NewValue.Value;
                InvalidateVisual();
            }
        }
    }
}
