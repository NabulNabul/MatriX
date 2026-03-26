using System;
using Avalonia.Controls;
using Avalonia.Media;

namespace Matrix.Controls
{
    public partial class SmithChartControl : UserControl
    {
        public SmithChartControl()
        {
            InitializeComponent();
            // 창 크기가 변경될 때마다 다시 그리도록 설정합니다.
            SizeChanged += (s, e) => InvalidateVisual();
        }

        public override void Render(DrawingContext drawingContext)
        {
            base.Render(drawingContext);

            double width = Bounds.Width;
            double height = Bounds.Height;

            if (width == 0 || height == 0) return;

            // 차트가 그려질 중심점과 반지름 계산
            double margin = 20;
            double radius = (Math.Min(width, height) / 2) - margin;
            if (radius <= 0) return;

            double cx = width / 2;
            double cy = height / 2;

            var standardPen = new Pen(Brushes.LightGray, 1.0);
            var axisPen = new Pen(Brushes.Black, 1.5);

            // Reactance 원이 주 차트(r=0) 바깥으로 나가지 않도록 클리핑 영역 설정
            // RoundedRect에서 CornerRadius를 radius로 설정하면 원형 클리핑이 됩니다.
            using (drawingContext.PushClip(new Avalonia.RoundedRect(new Avalonia.Rect(cx - radius, cy - radius, radius * 2, radius * 2), radius)))
            {
                // 1. 실수 축 (Real Axis) 그리기
                drawingContext.DrawLine(axisPen, new Avalonia.Point(cx - radius, cy), new Avalonia.Point(cx + radius, cy));

                // 2. Constant Resistance (일정 저항) 원 그리기
                double[] rValues = { 0, 0.2, 0.5, 1, 2, 5 };
                foreach (double r in rValues)
                {
                    double centerU = r / (r + 1.0);
                    double circleRadius = 1.0 / (r + 1.0);

                    double drawCx = cx + centerU * radius;
                    double drawR = circleRadius * radius;

                    drawingContext.DrawEllipse(null, standardPen, new Avalonia.Point(drawCx, cy), drawR, drawR);
                }

                // 3. Constant Reactance (일정 리액턴스) 원 그리기
                double[] xValues = { 0.2, 0.5, 1, 2, 5 };
                foreach (double x in xValues)
                {
                    double drawR = (1.0 / x) * radius;
                    // 상단 (+x) 원
                    drawingContext.DrawEllipse(null, standardPen, new Avalonia.Point(cx + radius, cy - drawR), drawR, drawR);
                    // 하단 (-x) 원
                    drawingContext.DrawEllipse(null, standardPen, new Avalonia.Point(cx + radius, cy + drawR), drawR, drawR);
                }
            }

            // 최외곽 테두리를 선명하게 한 번 더 그려줍니다.
            drawingContext.DrawEllipse(null, axisPen, new Avalonia.Point(cx, cy), radius, radius);
        }
    }
}
