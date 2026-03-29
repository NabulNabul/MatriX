
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Matrix.Models;

namespace Matrix.Controls
{
    public partial class SmithChartControl : Control
    {
        public static readonly StyledProperty<ObservableCollection<SParameterData>?> MeasuredDataProperty =
            AvaloniaProperty.Register<SmithChartControl, ObservableCollection<SParameterData>?>(nameof(MeasuredData));

        public ObservableCollection<SParameterData>? MeasuredData
        {
            get => GetValue(MeasuredDataProperty);
            set => SetValue(MeasuredDataProperty, value);
        }

        public static readonly StyledProperty<ObservableCollection<SParameterData>?> MatchedDataProperty =
            AvaloniaProperty.Register<SmithChartControl, ObservableCollection<SParameterData>?>(nameof(MatchedData));

        public ObservableCollection<SParameterData>? MatchedData
        {
            get => GetValue(MatchedDataProperty);
            set => SetValue(MatchedDataProperty, value);
        }

        public static readonly StyledProperty<ObservableCollection<List<SParameterData>>?> MatchingPathsProperty =
            AvaloniaProperty.Register<SmithChartControl, ObservableCollection<List<SParameterData>>?>(nameof(MatchingPaths));

        public ObservableCollection<List<SParameterData>>? MatchingPaths
        {
            get => GetValue(MatchingPathsProperty);
            set => SetValue(MatchingPathsProperty, value);
        }

        public static readonly StyledProperty<bool> ShowVswrCircleProperty =
            AvaloniaProperty.Register<SmithChartControl, bool>(nameof(ShowVswrCircle), true);

        public bool ShowVswrCircle
        {
            get => GetValue(ShowVswrCircleProperty);
            set => SetValue(ShowVswrCircleProperty, value);
        }

        public static readonly StyledProperty<double> TargetVswrProperty =
            AvaloniaProperty.Register<SmithChartControl, double>(nameof(TargetVswr), 2.0);

        public double TargetVswr
        {
            get => GetValue(TargetVswrProperty);
            set => SetValue(TargetVswrProperty, value);
        }

        public static readonly DirectProperty<SmithChartControl, Point> MouseGammaProperty =
            AvaloniaProperty.RegisterDirect<SmithChartControl, Point>(nameof(MouseGamma), o => o.MouseGamma);
        private Point _mouseGamma;
        public Point MouseGamma { get => _mouseGamma; private set => SetAndRaise(MouseGammaProperty, ref _mouseGamma, value); }

        public static readonly DirectProperty<SmithChartControl, Point> MouseImpedanceProperty =
            AvaloniaProperty.RegisterDirect<SmithChartControl, Point>(nameof(MouseImpedance), o => o.MouseImpedance);
        private Point _mouseImpedance;
        public Point MouseImpedance { get => _mouseImpedance; private set => SetAndRaise(MouseImpedanceProperty, ref _mouseImpedance, value); }

        public static readonly DirectProperty<SmithChartControl, string> AutoMatchInfoProperty =
            AvaloniaProperty.RegisterDirect<SmithChartControl, string>(nameof(AutoMatchInfo), o => o.AutoMatchInfo);
        private string _autoMatchInfo = string.Empty;
        public string AutoMatchInfo { get => _autoMatchInfo; private set => SetAndRaise(AutoMatchInfoProperty, ref _autoMatchInfo, value); }


        private bool _isMouseOver = false;
        
        // Zoom & Pan 상태 변수
        private double _zoomFactor = 1.0;
        private Point _panOffset = new Point(0, 0);
        private bool _isPanning = false;
        private Point _lastMousePosition;

        public SmithChartControl()
        {
            this.ClipToBounds = true; // 확대 시 컨트롤 영역 밖으로 그려지는 것을 방지
            this.AttachedToVisualTree += (_, __) => SubscribeCollections();
            this.DetachedFromVisualTree += (_, __) => UnsubscribeCollections();
            this.GetObservable(MeasuredDataProperty).Subscribe(_ => SubscribeCollections());
            this.GetObservable(MatchedDataProperty).Subscribe(_ => SubscribeCollections());
            this.GetObservable(MatchingPathsProperty).Subscribe(_ => SubscribeCollections());
            this.GetObservable(ShowVswrCircleProperty).Subscribe(_ => InvalidateVisual());
            this.GetObservable(TargetVswrProperty).Subscribe(_ => InvalidateVisual());
            this.SizeChanged += (_, __) => InvalidateVisual();
            this.PointerMoved += OnPointerMoved;
            this.PointerEntered += OnPointerMoved;
            this.PointerExited += OnPointerLeave;
            this.PointerPressed += OnPointerPressed;
            this.PointerReleased += OnPointerReleased;
            this.PointerWheelChanged += OnPointerWheelChanged;
        }

        private void SubscribeCollections()
        {
            if (MeasuredData is INotifyCollectionChanged measured)
                measured.CollectionChanged += OnCollectionChanged;
            if (MatchedData is INotifyCollectionChanged matched)
                matched.CollectionChanged += OnCollectionChanged;
            if (MatchingPaths is INotifyCollectionChanged paths)
                paths.CollectionChanged += OnCollectionChanged;
            InvalidateVisual();
        }

        private void UnsubscribeCollections()
        {
            if (MeasuredData is INotifyCollectionChanged measured)
                measured.CollectionChanged -= OnCollectionChanged;
            if (MatchedData is INotifyCollectionChanged matched)
                matched.CollectionChanged -= OnCollectionChanged;
            if (MatchingPaths is INotifyCollectionChanged paths)
                paths.CollectionChanged -= OnCollectionChanged;
        }

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            InvalidateVisual();
        }

        // 공통된 줌/팬 좌표 계산 로직
        private bool GetChartLayout(out double cx, out double cy, out double radius)
        {
            double width = Bounds.Width;
            double height = Bounds.Height;
            double size = Math.Min(width, height) - 20;
            
            if (size <= 0)
            {
                cx = cy = radius = 0;
                return false;
            }

            cx = (width / 2) + _panOffset.X;
            cy = (height / 2) + _panOffset.Y;
            radius = (size / 2) * _zoomFactor;
            return true;
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            if (!GetChartLayout(out double cx, out double cy, out double radius)) return;

            DrawSmithChartGrid(context, cx, cy, radius);
            DrawVswrCircle(context, cx, cy, radius);
            DrawData(context, MeasuredData, cx, cy, radius, Brushes.Blue);
            DrawMatchingPaths(context, cx, cy, radius);
            DrawData(context, MatchedData, cx, cy, radius, Brushes.Red);
            DrawCursorInfo(context, Bounds.Height);
        }


        private void DrawSmithChartGrid(DrawingContext context, double cx, double cy, double r)
        {
            var majorPen = new Pen(Brushes.Gray, 1.0);
            var minorPen = new Pen(Brushes.LightGray, 0.5);
            var axisPen = new Pen(Brushes.Black, 1.0);
            var highlightPen = new Pen(Brushes.Blue, 1.5);
            var admMajorPen = new Pen(new SolidColorBrush(Color.FromArgb(150, 0, 100, 0)), 0.8,
                new DashStyle(new double[] { 4, 3 }, 0));
            var admMinorPen = new Pen(new SolidColorBrush(Color.FromArgb(70, 0, 120, 0)), 0.4,
                new DashStyle(new double[] { 2, 4 }, 0));
            var labelBrush = Brushes.DarkSlateGray;

            // Grid values matching smith.ps (r=20, r=50 추가)
            double[] rMajorValues = { 0.2, 0.5, 1.0, 2.0, 5.0 };
            double[] rMinorValues = { 0.1, 0.3, 0.4, 0.6, 0.7, 0.8, 0.9, 1.2, 1.4, 1.6, 1.8, 3.0, 4.0, 10.0, 20.0, 50.0 };
            double[] xMajorValues = { 0.2, 0.5, 1.0, 2.0, 5.0 };
            double[] xMinorValues = { 0.1, 0.3, 0.4, 0.6, 0.7, 0.8, 0.9, 1.2, 1.4, 1.6, 1.8, 3.0, 4.0, 10.0, 20.0, 50.0 };

            // 단위원으로 클리핑 — 리액턴스/어드미턴스 원이 경계 밖으로 넘치지 않도록
            using (context.PushGeometryClip(new EllipseGeometry(new Rect(cx - r, cy - r, r * 2, r * 2))))
            {
                // 어드미턴스 그리드 (점선 녹색, 임피던스 그리드 아래 먼저 그림)
                foreach (var gVal in rMajorValues)
                    DrawAdmittanceGCircle(context, cx, cy, r, admMajorPen, gVal);
                foreach (var gVal in rMinorValues)
                    DrawAdmittanceGCircle(context, cx, cy, r, admMinorPen, gVal);
                foreach (var bVal in xMajorValues)
                {
                    DrawAdmittanceBCircle(context, cx, cy, r, admMajorPen, bVal);
                    DrawAdmittanceBCircle(context, cx, cy, r, admMajorPen, -bVal);
                }
                foreach (var bVal in xMinorValues)
                {
                    DrawAdmittanceBCircle(context, cx, cy, r, admMinorPen, bVal);
                    DrawAdmittanceBCircle(context, cx, cy, r, admMinorPen, -bVal);
                }

                // 수평축 (R-axis)
                context.DrawLine(axisPen, new Point(cx - r, cy), new Point(cx + r, cy));

                // 등저항 원
                foreach (var rVal in rMajorValues)
                {
                    var pen = Math.Abs(rVal - 1.0) < 1e-9 ? highlightPen : majorPen;
                    DrawResistanceCircle(context, cx, cy, r, pen, rVal);
                }
                foreach (var rVal in rMinorValues)
                    DrawResistanceCircle(context, cx, cy, r, minorPen, rVal);

                // 등리액턴스 원 (정확한 원 방정식, 클리핑으로 경계 처리)
                foreach (var xVal in xMajorValues)
                {
                    var pen = Math.Abs(xVal - 1.0) < 1e-9 ? highlightPen : majorPen;
                    DrawReactanceCircle(context, cx, cy, r, pen, xVal);
                    DrawReactanceCircle(context, cx, cy, r, pen, -xVal);
                }
                foreach (var xVal in xMinorValues)
                {
                    DrawReactanceCircle(context, cx, cy, r, minorPen, xVal);
                    DrawReactanceCircle(context, cx, cy, r, minorPen, -xVal);
                }
            }

            // 경계원 (클리핑 해제 후 위에 덮어 그림)
            context.DrawEllipse(null, new Pen(Brushes.Black, 1.5), new Point(cx, cy), r, r);

            // R 라벨 (수평축 위)
            foreach (var rVal in rMajorValues)
            {
                double labelX = cx + r * (rVal - 1) / (rVal + 1);
                double labelYOffset = Math.Abs(rVal - 1.0) < 1e-9 ? -12 : 0;
                DrawText(context, rVal.ToString("0.0#"), new Point(labelX, cy + labelYOffset), labelBrush, 9);
            }

            // X 라벨 (경계원 바깥)
            foreach (var xVal in xMajorValues)
            {
                Point gammaPos = GammaFromZ(0, xVal);
                DrawText(context, xVal.ToString("0.0#"),
                    new Point(cx + gammaPos.X * (r + 10), cy - gammaPos.Y * (r + 10)), labelBrush, 9);
                Point gammaNeg = GammaFromZ(0, -xVal);
                DrawText(context, (-xVal).ToString("0.0#"),
                    new Point(cx + gammaNeg.X * (r + 10), cy - gammaNeg.Y * (r + 10)), labelBrush, 9);
            }
        }

        // 등저항 원: 중심=(r/(r+1), 0), 반지름=1/(r+1) in gamma space
        private static void DrawResistanceCircle(DrawingContext context, double cx, double cy, double r, IPen pen, double rVal)
        {
            double circleCx = cx + r * (rVal / (rVal + 1));
            double circleR = r / (rVal + 1);
            context.DrawEllipse(null, pen, new Point(circleCx, cy), circleR, circleR);
        }

        // 등리액턴스 원: 중심=(1, 1/x), 반지름=|1/x| in gamma space  (smith.ps DrawXarc와 동일)
        private static void DrawReactanceCircle(DrawingContext context, double cx, double cy, double r, IPen pen, double xVal)
        {
            if (Math.Abs(xVal) < 1e-9) return;
            double arcCx = cx + r;              // gamma u=1
            double arcCy = cy - r / xVal;       // gamma v=1/x, Y축 반전
            double arcR = r / Math.Abs(xVal);
            context.DrawEllipse(null, pen, new Point(arcCx, arcCy), arcR, arcR);
        }

        // 등전도 어드미턴스 원: 중심=(-g/(g+1), 0), 반지름=1/(g+1) in gamma space
        private static void DrawAdmittanceGCircle(DrawingContext context, double cx, double cy, double r, IPen pen, double gVal)
        {
            double circleCx = cx - r * (gVal / (gVal + 1));
            double circleR = r / (gVal + 1);
            context.DrawEllipse(null, pen, new Point(circleCx, cy), circleR, circleR);
        }

        // 등서셉턴스 어드미턴스 원: 중심=(-1, -1/b), 반지름=|1/b| in gamma space
        private static void DrawAdmittanceBCircle(DrawingContext context, double cx, double cy, double r, IPen pen, double bVal)
        {
            if (Math.Abs(bVal) < 1e-9) return;
            double arcCx = cx - r;              // gamma u=-1
            double arcCy = cy + r / bVal;       // gamma v=-1/b, Y축 반전: cy - r*(-1/b) = cy + r/b
            double arcR = r / Math.Abs(bVal);
            context.DrawEllipse(null, pen, new Point(arcCx, arcCy), arcR, arcR);
        }

        private void DrawVswrCircle(DrawingContext context, double cx, double cy, double r)
        {
            if (!ShowVswrCircle || TargetVswr <= 1.0) return;

            double gammaMag = (TargetVswr - 1.0) / (TargetVswr + 1.0);
            double vswrRadius = gammaMag * r;

            var vswrPen = new Pen(Brushes.Red, 1.5, new DashStyle(new double[] { 4, 4 }, 0));
            context.DrawEllipse(null, vswrPen, new Point(cx, cy), vswrRadius, vswrRadius);

            DrawText(context, $"VSWR {TargetVswr:F1}", new Point(cx + vswrRadius + 2, cy - 10), Brushes.Red, 11, alignLeft: true);
        }

        private Point GammaFromZ(double r, double x)
        {
            // gamma = (z-1)/(z+1) where z = r + jx
            double denominator = (r + 1) * (r + 1) + x * x;
            if (Math.Abs(denominator) < 1e-9) return new Point(1, 0); // Represents infinity
            double u = (r * r + x * x - 1) / denominator;
            double v = (2 * x) / denominator;
            return new Point(u, v);
        }

        private Point ZFromGamma(double u, double v)
        {
            var gamma = new Complex(u, v);
            var z = (1.0 + gamma) / (1.0 - gamma);
            return new Point(z.Real, z.Imaginary);
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            var p = e.GetCurrentPoint(this);
            var pos = p.Position;

            if (_isPanning)
            {
                double dx = pos.X - _lastMousePosition.X;
                double dy = pos.Y - _lastMousePosition.Y;
                _panOffset = new Point(_panOffset.X + dx, _panOffset.Y + dy);
                _lastMousePosition = pos;
                InvalidateVisual();
            }

            if (!GetChartLayout(out double cx, out double cy, out double radius)) return;
            
            double u = (pos.X - cx) / radius;
            double v = -(pos.Y - cy) / radius;

            _isMouseOver = u * u + v * v <= 1.005; // Give a little tolerance

            MouseGamma = new Point(u, v);
            MouseImpedance = _isMouseOver ? ZFromGamma(u, v) : new Point(double.NaN, double.NaN);
            InvalidateVisual();
        }

        private void OnPointerLeave(object? sender, PointerEventArgs e)
        {
            _isMouseOver = false;
            MouseImpedance = new Point(double.NaN, double.NaN);
            InvalidateVisual();
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            var p = e.GetCurrentPoint(this);

            if (p.Properties.IsRightButtonPressed || p.Properties.IsMiddleButtonPressed)
            {
                _isPanning = true;
                _lastMousePosition = p.Position;
                e.Handled = true;
                return;
            }

            if (p.Properties.IsLeftButtonPressed && e.ClickCount == 2)
            {
                _zoomFactor = 1.0;
                _panOffset = new Point(0, 0);
                AutoMatchInfo = string.Empty;
                InvalidateVisual();
                return;
            }

            if (!p.Properties.IsLeftButtonPressed)
            {
                AutoMatchInfo = string.Empty;
                InvalidateVisual();
                return;
            }

            // 1. Get Target Impedance from click
            if (!GetChartLayout(out double cx, out double cy, out double radius)) return;

            var pos = p.Position;
            double u = (pos.X - cx) / radius;
            double v = -(pos.Y - cy) / radius;

            if (u * u + v * v > 1.005) // Click outside chart
            {
                AutoMatchInfo = string.Empty;
                InvalidateVisual();
                return;
            }

            var z_target_pt = ZFromGamma(u, v);
            var z_target = new Complex(z_target_pt.X, z_target_pt.Y);

            // 2. Get Source Impedance from the last data point
            SParameterData? sourceData = (MatchedData?.Any() == true) ? MatchedData.Last() : MeasuredData?.LastOrDefault();
            if (sourceData == null) return;

            var gamma_start = new Complex(sourceData.Real, sourceData.Imaginary);
            var z_start = (1.0 + gamma_start) / (1.0 - gamma_start);

            // 3. Get Frequency (use the one from the source point)
            double f = sourceData.Frequency * 1e9; // GHz to Hz
            double omega = 2 * Math.PI * f;
            const double Z0 = 50.0;

            var results = new List<string>();

            // 4. Check for Series Match (constant resistance circle)
            if (Math.Abs(z_start.Real - z_target.Real) < 0.01) // Tolerance for constant-R
            {
                double x_needed = (z_target.Imaginary - z_start.Imaginary) * Z0;
                if (Math.Abs(x_needed) > 1e-9)
                {
                    if (x_needed > 0) // Inductor
                    {
                        double L = x_needed / omega; // in H
                        results.Add($"Series L: {L * 1e9:F2} nH");
                    }
                    else // Capacitor
                    {
                        double C = -1 / (omega * x_needed); // in F
                        results.Add($"Series C: {C * 1e12:F2} pF");
                    }
                }
            }

            // 5. Check for Shunt Match (constant conductance circle)
            var y_start = 1.0 / z_start;
            var y_target = 1.0 / z_target;
            if (Math.Abs(y_start.Real - y_target.Real) < 0.01) // Tolerance for constant-G
            {
                double b_needed = (y_target.Imaginary - y_start.Imaginary) * Z0; // Admittance is normalized to Y0 = 1/Z0
                if (Math.Abs(b_needed) > 1e-9)
                {
                    if (b_needed > 0) // Capacitor
                    {
                        double C = b_needed / omega; // in F
                        results.Add($"Shunt C: {C * 1e12:F2} pF");
                    }
                    else // Inductor
                    {
                        double L = -1 / (omega * b_needed); // in H
                        results.Add($"Shunt L: {L * 1e9:F2} nH");
                    }
                }
            }

            AutoMatchInfo = results.Any() ? string.Join(" | ", results) : "No simple match found";
            InvalidateVisual();
        }

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            var p = e.GetCurrentPoint(this);
            if (_isPanning && (!p.Properties.IsRightButtonPressed && !p.Properties.IsMiddleButtonPressed))
            {
                _isPanning = false;
            }
        }

        private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            // 스크롤 변화량(e.Delta.Y)을 반영하여 지수적으로 부드러운 줌 배율 적용
            double zoomMultiplier = 1.15;
            double zoomDelta = Math.Pow(zoomMultiplier, e.Delta.Y);
            double newZoom = _zoomFactor * zoomDelta;

            // 줌 한계 설정
            if (newZoom < 0.1) newZoom = 0.1;
            if (newZoom > 50.0) newZoom = 50.0;

            // 마우스 커서 위치를 중심으로 줌 인/아웃 수행
            var mousePos = e.GetPosition(this);
            double centerX = Bounds.Width / 2;
            double centerY = Bounds.Height / 2;

            // 마우스 커서의 화면 중심 대비 상대 좌표
            double relX = mousePos.X - centerX;
            double relY = mousePos.Y - centerY;

            // 실제 변경된 줌 비율에 맞춰 화면 이동 오프셋(Pan) 보정
            double ratio = newZoom / _zoomFactor;
            _panOffset = new Point(
                relX - (relX - _panOffset.X) * ratio,
                relY - (relY - _panOffset.Y) * ratio
            );

            _zoomFactor = newZoom;
            InvalidateVisual();
            e.Handled = true;
        }


        private void DrawCursorInfo(DrawingContext context, double controlHeight)
        {
            var bgBrush = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255));
            double yPos = controlHeight - 45;
            double requiredHeight = 0;

            if (_isMouseOver && !double.IsNaN(MouseImpedance.X))
            {
                requiredHeight += 40;
            }
            if (!string.IsNullOrEmpty(AutoMatchInfo))
            {
                requiredHeight += 25;
            }
            if (requiredHeight == 0) return;

            context.DrawRectangle(bgBrush, null, new Rect(5, controlHeight - 5 - requiredHeight, 350, requiredHeight));

            if (!string.IsNullOrEmpty(AutoMatchInfo))
            {
                DrawText(context, $"Click Match: {AutoMatchInfo}", new Point(10, yPos - 20), Brushes.DarkMagenta, 12, alignLeft: true);
            }
            if (_isMouseOver && !double.IsNaN(MouseImpedance.X))
            {
                string gammaText = $"Γ: {MouseGamma.X:F3}, {MouseGamma.Y:F3}j";
                string impedanceText = $"Z: {MouseImpedance.X:F3:F3} + {MouseImpedance.Y:F3}j";
                DrawText(context, gammaText, new Point(10, yPos), Brushes.Black, 12, alignLeft: true);
                DrawText(context, impedanceText, new Point(10, yPos + 20), Brushes.Black, 12, alignLeft: true);
            }
        }

        private void DrawText(DrawingContext context, string text, Point origin, IBrush brush, double size = 10, bool alignLeft = false)
        {
            var formattedText = new FormattedText(
                text,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                size,
                brush);

            Point textOrigin;
            if (alignLeft)
                textOrigin = new Point(origin.X, origin.Y);
            else // Center the text on the origin point
                textOrigin = new Point(origin.X - formattedText.Width / 2, origin.Y - formattedText.Height / 2);
            
            context.DrawText(formattedText, textOrigin);
        }

        private void DrawMatchingPaths(DrawingContext context, double cx, double cy, double radius)
        {
            if (MatchingPaths == null || MatchingPaths.Count == 0) return;

            var pathPen = new Pen(Brushes.DarkGreen, 1.5, new DashStyle(new double[] { 4, 2 }, 0));

            foreach (var path in MatchingPaths)
            {
                Point? last = null;
                foreach (var point in path)
                {
                    double gammaR = point.Real;
                    double gammaI = point.Imaginary;
                    double x = cx + gammaR * radius;
                    double y = cy - gammaI * radius;
                    var pt = new Point(x, y);
                    if (last != null)
                    {
                        context.DrawLine(pathPen, last.Value, pt);
                    }
                    // Draw a small circle for each intermediate step
                    context.DrawEllipse(Brushes.DarkGreen, null, pt, 2, 2);
                    last = pt;
                }
            }
        }

        private void DrawData(DrawingContext context, ObservableCollection<SParameterData>? data, double cx, double cy, double radius, IBrush brush)
        {
            if (data == null || data.Count == 0) return;
            Point? last = null;
            foreach (var point in data)
            {
                double gammaR = point.Real;
                double gammaI = point.Imaginary;
                double x = cx + gammaR * radius;
                double y = cy - gammaI * radius;
                var pt = new Point(x, y);
                if (last != null)
                    context.DrawLine(new Pen(brush, 2), last.Value, pt);
                last = pt;
            }
        }

        public void SaveAsPng(Stream stream)
        {
            double width = Bounds.Width;
            double height = Bounds.Height;
            
            if (width <= 0 || height <= 0) return;

            var pixelSize = new PixelSize((int)width, (int)height);
            using var bitmap = new RenderTargetBitmap(pixelSize, new Avalonia.Vector(96, 96));

            using (var context = bitmap.CreateDrawingContext())
            {
                // 1. Draw a white background rectangle that fills the entire bitmap.
                context.DrawRectangle(Brushes.White, null, new Rect(pixelSize.ToSize(1.0)));

                // 2. Create a VisualBrush from this control. The control's own background is transparent,
                //    so the brush will only contain the drawn chart elements.
                var brush = new VisualBrush { Visual = this, Stretch = Stretch.None };

                // 3. Draw the content of the control (from the brush) on top of the white background.
                context.DrawRectangle(brush, null, new Rect(pixelSize.ToSize(1.0)));
            }

            bitmap.Save(stream);
        }
    }
}