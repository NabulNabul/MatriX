
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

        public static readonly StyledProperty<string> VswrTextProperty =
            AvaloniaProperty.Register<SmithChartControl, string>(nameof(VswrText), string.Empty);
        public string VswrText
        {
            get => GetValue(VswrTextProperty);
            set => SetValue(VswrTextProperty, value);
        }

        public static readonly StyledProperty<string> ReturnLossTextProperty =
            AvaloniaProperty.Register<SmithChartControl, string>(nameof(ReturnLossText), string.Empty);
        public string ReturnLossText
        {
            get => GetValue(ReturnLossTextProperty);
            set => SetValue(ReturnLossTextProperty, value);
        }


        private bool _isMouseOver = false;

        // 오버레이 상태
        private bool   _overlayIsOver;
        private Point  _overlayGamma;
        private Point  _overlayImpedance = new Point(double.NaN, double.NaN);
        private string _overlayAutoMatch = string.Empty;
        private string _overlayVswr      = string.Empty;
        private string _overlayRl        = string.Empty;


        // Zoom & Pan 상태 변수
        private double _zoomFactor = 1.0;
        private Point _panOffset = new Point(0, 0);
        private bool _isPanning = false;
        private Point _lastMousePosition;

        public SmithChartControl()
        {
            this.ClipToBounds = true;

            this.AttachedToVisualTree += (_, __) => SubscribeCollections();
            this.DetachedFromVisualTree += (_, __) => UnsubscribeCollections();
            this.GetObservable(MeasuredDataProperty).Subscribe(_ => InvalidateChartCache());
            this.GetObservable(MatchedDataProperty).Subscribe(_ => InvalidateChartCache());
            this.GetObservable(MatchingPathsProperty).Subscribe(_ => InvalidateChartCache());
            this.GetObservable(ShowVswrCircleProperty).Subscribe(_ => InvalidateChartCache());
            this.GetObservable(TargetVswrProperty).Subscribe(_ => InvalidateChartCache());
            this.GetObservable(VswrTextProperty).Subscribe(_ => { _overlayVswr = VswrText; InvalidateVisual(); });
            this.GetObservable(ReturnLossTextProperty).Subscribe(_ => { _overlayRl = ReturnLossText; InvalidateVisual(); });
            this.SizeChanged += (_, __) => InvalidateChartCache();
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
            InvalidateChartCache();
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
            => InvalidateChartCache();

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

        // 차트 무효화 — 데이터/줌/팬/크기 변경 시 호출
        private void InvalidateChartCache()
        {
            InvalidateVisual();
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);
            if (!GetChartLayout(out double cx, out double cy, out double radius)) return;

            // Avalonia DrawingContext는 GPU 벡터 파이프라인 — 직접 그리면 벡터 품질 그대로
            DrawSmithChartGrid(context, cx, cy, radius);
            DrawVswrCircle(context, cx, cy, radius);
            DrawData(context, MeasuredData, cx, cy, radius, Brushes.Blue);
            DrawMatchingPaths(context, cx, cy, radius);
            DrawData(context, MatchedData, cx, cy, radius, Brushes.Red);
            DrawOverlay(context);
        }

        private static readonly Typeface   _overlayTf   = new Typeface("Segoe UI");
        private static readonly IBrush     _overlayBg   = new SolidColorBrush(Color.FromArgb(200, 30, 30, 30));
        private static readonly IBrush     _overlayFg   = Brushes.WhiteSmoke;
        private static readonly IBrush     _overlayPlum = new SolidColorBrush(Colors.Plum);
        private const double OverlayFs    = 15;
        private const double OverlayLineH = 25;
        private const double OverlayPad   = 8;

        private void DrawOverlay(DrawingContext ctx)
        {
            double w = Bounds.Width;
            double h = Bounds.Height;
            if (w <= 0 || h <= 0) return;

            // ── 좌측 하단: Γ / Z / AutoMatch ─────────────────────────
            var btmLines = new List<(string text, IBrush brush)>();
            if (!string.IsNullOrEmpty(_overlayAutoMatch))
                btmLines.Add(($"Match: {_overlayAutoMatch}", _overlayPlum));
            if (_overlayIsOver && !double.IsNaN(_overlayImpedance.X))
            {
                btmLines.Add(($"Γ  {_overlayGamma.X:+0.000;-0.000} {_overlayGamma.Y:+0.000;-0.000}j", _overlayFg));
                btmLines.Add(($"Z  {_overlayImpedance.X:F3} {_overlayImpedance.Y:+0.000;-0.000}j Ω",  _overlayFg));
            }
            if (btmLines.Count > 0)
            {
                double bh = btmLines.Count * OverlayLineH + OverlayPad * 2;
                double by = h - bh - 5;
                ctx.DrawRectangle(_overlayBg, null, new Rect(5, by, 290, bh), 4, 4);
                for (int i = 0; i < btmLines.Count; i++)
                    DrawOverlayStr(ctx, btmLines[i].text, 5 + OverlayPad, by + OverlayPad + i * OverlayLineH, btmLines[i].brush);
            }

            // ── 우측 상단: VSWR / ReturnLoss ─────────────────────────
            var topLines = new List<string>();
            if (!string.IsNullOrEmpty(_overlayVswr)) topLines.Add(_overlayVswr);
            if (!string.IsNullOrEmpty(_overlayRl))   topLines.Add(_overlayRl);
            if (topLines.Count > 0)
            {
                const double boxW = 250;
                double th = topLines.Count * OverlayLineH + OverlayPad * 2;
                double tx = w - boxW - 5;
                ctx.DrawRectangle(_overlayBg, null, new Rect(tx, 5, boxW, th), 4, 4);
                for (int i = 0; i < topLines.Count; i++)
                    DrawOverlayStr(ctx, topLines[i], tx + OverlayPad, 5 + OverlayPad + i * OverlayLineH, _overlayFg);
            }
        }

        private static void DrawOverlayStr(DrawingContext ctx, string text, double x, double y, IBrush brush)
        {
            var ft = new FormattedText(text,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, _overlayTf, OverlayFs, brush);
            ctx.DrawText(ft, new Point(x, y));
        }

        // smith.ps ZRegions/ZMinordiv/ZMajordiv 기반 그리드 생성
        private enum GridWeight { Thin, Minor, Major, Highlight }

        private static List<(double value, GridWeight weight)> BuildZGrid()
        {
            // smith.ps: ZRegions=[0,0.2,0.5,1,2,5,10,20,50], ZMinordiv=[0.01,0.02,0.05,0.1,0.2,1,2,10], ZMajordiv=[5,5,2,2,5,5,5,5]
            double[] regions = { 0, 0.2, 0.5, 1.0, 2.0, 5.0, 10.0, 20.0, 50.0 };
            double[] steps   = { 0.01, 0.02, 0.05, 0.1, 0.2, 1.0, 2.0, 10.0 };
            int[]    majorN  = { 5, 5, 2, 2, 5, 5, 5, 5 };

            var result = new List<(double, GridWeight)>();
            for (int seg = 0; seg < regions.Length - 1; seg++)
            {
                double end   = regions[seg + 1];
                double step  = steps[seg];
                int    major = majorN[seg];
                int    count = 0;

                for (double v = regions[seg] + step; v <= end + step * 0.01; v += step, count++)
                {
                    double rv = Math.Round(v, 10);
                    bool isBoundary  = Math.Abs(rv - end)  < step * 0.1;
                    bool isHighlight = Math.Abs(rv - 1.0)  < step * 0.1;
                    bool isMajorDiv  = ((count + 1) % major == 0) || isBoundary;

                    GridWeight w;
                    if      (isHighlight) w = GridWeight.Highlight;
                    else if (isMajorDiv)  w = GridWeight.Major;
                    else if (seg == 0)    w = GridWeight.Thin;
                    else                  w = GridWeight.Minor;

                    result.Add((rv, w));
                }
            }
            return result;
        }

        private void DrawSmithChartGrid(DrawingContext context, double cx, double cy, double r)
        {
            // 선 두께: Thin=0.3, Minor=0.5, Major=0.85, Highlight=1.6 (blue)
            var thinPen      = new Pen(new SolidColorBrush(Color.FromArgb(110, 190, 190, 190)), 0.3);
            var minorPen     = new Pen(new SolidColorBrush(Color.FromArgb(170, 120, 120, 120)), 0.5);
            var majorPen     = new Pen(new SolidColorBrush(Color.FromArgb(210,  60,  60,  60)), 0.85);
            var highlightPen = new Pen(new SolidColorBrush(Color.FromArgb(230,  20,  60, 200)), 1.6);
            var axisPen      = new Pen(Brushes.Black, 1.0);

            // 어드미턴스 점선 (녹색, 임피던스보다 연하게)
            var admThinPen  = new Pen(new SolidColorBrush(Color.FromArgb( 55,   0, 150,  60)), 0.3,  new DashStyle(new double[] { 3, 5 }, 0));
            var admMinorPen = new Pen(new SolidColorBrush(Color.FromArgb( 90,   0, 130,  50)), 0.45, new DashStyle(new double[] { 3, 4 }, 0));
            var admMajorPen = new Pen(new SolidColorBrush(Color.FromArgb(140,   0, 110,  40)), 0.7,  new DashStyle(new double[] { 4, 3 }, 0));

            var grid = BuildZGrid();

            IPen ZPen(GridWeight w) => w switch
            {
                GridWeight.Highlight => highlightPen,
                GridWeight.Major     => majorPen,
                GridWeight.Minor     => minorPen,
                _                    => thinPen
            };
            IPen YPen(GridWeight w) => w switch
            {
                GridWeight.Major or GridWeight.Highlight => admMajorPen,
                GridWeight.Minor                         => admMinorPen,
                _                                        => admThinPen
            };

            using (context.PushGeometryClip(new EllipseGeometry(new Rect(cx - r, cy - r, r * 2, r * 2))))
            {
                // ① 어드미턴스 그리드 (먼저, 임피던스 아래)
                foreach (var (v, w) in grid)
                {
                    var ap = YPen(w);
                    DrawAdmittanceGCircle(context, cx, cy, r, ap, v);
                    if (v > 1e-9)
                    {
                        DrawAdmittanceBCircle(context, cx, cy, r, ap,  v);
                        DrawAdmittanceBCircle(context, cx, cy, r, ap, -v);
                    }
                }

                // ② 수평 실축
                context.DrawLine(axisPen, new Point(cx - r, cy), new Point(cx + r, cy));

                // ③ 등저항 원 (R circles)
                foreach (var (v, w) in grid)
                    DrawResistanceCircle(context, cx, cy, r, ZPen(w), v);

                // ④ 등리액턴스 원 (X arcs, ±)
                foreach (var (v, w) in grid)
                {
                    if (v < 1e-9) continue;
                    var zp = ZPen(w);
                    DrawReactanceCircle(context, cx, cy, r, zp,  v);
                    DrawReactanceCircle(context, cx, cy, r, zp, -v);
                }
            }

            // ⑤ 경계원 (클리핑 해제 후 맨 위에 덮어 그림)
            context.DrawEllipse(null, new Pen(Brushes.Black, 1.5), new Point(cx, cy), r, r);

            // ⑥ R 라벨 — 실축 위, 경계원 안쪽
            var labelBrush  = new SolidColorBrush(Color.FromArgb(210, 30, 30, 60));
            var hlLabelBrush = new SolidColorBrush(Color.FromArgb(230, 20, 60, 200));
            double[] rLabels = { 0.2, 0.5, 1.0, 2.0, 5.0, 10.0, 20.0 };
            foreach (var rv in rLabels)
            {
                double lx    = cx + r * (rv - 1.0) / (rv + 1.0);
                bool   isHl  = Math.Abs(rv - 1.0) < 1e-9;
                double lyOff = isHl ? -13 : 4;
                string lbl   = rv < 10 ? rv.ToString("0.0#") : rv.ToString("0");
                DrawText(context, lbl, new Point(lx, cy + lyOff), isHl ? hlLabelBrush : labelBrush, isHl ? 11 : 9);
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
                InvalidateChartCache(); // 팬은 차트 레이아웃 변경
            }

            if (!GetChartLayout(out double cx, out double cy, out double radius)) return;

            double u = (pos.X - cx) / radius;
            double v = -(pos.Y - cy) / radius;

            // 히스테리시스: 진입은 0.98, 이탈은 1.02 — 경계 근처 깜박거림 방지
            double magSq = u * u + v * v;
            if (!_isMouseOver && magSq <= 0.98)
                _isMouseOver = true;
            else if (_isMouseOver && magSq > 1.02)
                _isMouseOver = false;

            MouseGamma     = new Point(u, v);
            MouseImpedance = _isMouseOver ? ZFromGamma(u, v) : new Point(double.NaN, double.NaN);
            _overlayIsOver    = _isMouseOver;
            _overlayGamma     = MouseGamma;
            _overlayImpedance = MouseImpedance;
            _overlayAutoMatch = AutoMatchInfo;
            InvalidateVisual();
        }

        private void OnPointerLeave(object? sender, PointerEventArgs e)
        {
            _isMouseOver      = false;
            _overlayIsOver    = false;
            MouseImpedance    = new Point(double.NaN, double.NaN);
            _overlayImpedance = MouseImpedance;
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
                _zoomFactor   = 1.0;
                _panOffset    = new Point(0, 0);
                AutoMatchInfo    = string.Empty;
                _overlayAutoMatch = string.Empty;
                _overlayIsOver   = false;
                InvalidateChartCache(); // 더블클릭: 줌/팬 리셋
                return;
            }

            if (!p.Properties.IsLeftButtonPressed)
            {
                AutoMatchInfo    = string.Empty;
                _overlayAutoMatch = string.Empty;
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
                AutoMatchInfo    = string.Empty;
                _overlayAutoMatch = string.Empty;
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

            AutoMatchInfo    = results.Any() ? string.Join(" | ", results) : "No simple match found";
            _overlayAutoMatch = AutoMatchInfo;
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
            InvalidateChartCache(); // 줌은 차트 레이아웃 변경
            e.Handled = true;
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