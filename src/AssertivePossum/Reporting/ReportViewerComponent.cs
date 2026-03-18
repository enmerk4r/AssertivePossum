using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using AssertivePossum.Goo;

namespace AssertivePossum.Components.Reporting;

public class ReportViewerComponent : GH_Component
{
    public ReportViewerComponent()
        : base("Report Viewer", "View",
            "Displays a visual summary of a test report on the canvas.",
            "Assertive Possum", "3. Reporting")
    {
    }

    public override Guid ComponentGuid => new("F1A2B3C4-D5E6-4F78-9A0B-C1D2E3F4A5B6");
    public override GH_Exposure Exposure => GH_Exposure.secondary;

    protected override Bitmap? Icon =>
        new Bitmap(GetType().Assembly.GetManifestResourceStream("Icons.report-viewer.png")!);

    private TestReport? _currentReport;

    public override void CreateAttributes()
    {
        m_attributes = new ReportViewerAttributes(this);
    }

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddGenericParameter("Report", "R", "Test report to display.", GH_ParamAccess.item);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddIntegerParameter("Passed", "P", "Number of passed tests.", GH_ParamAccess.item);
        pManager.AddIntegerParameter("Failed", "F", "Number of failed tests.", GH_ParamAccess.item);
        pManager.AddIntegerParameter("Errors", "E", "Number of errored tests.", GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess DA)
    {
        TestReportGoo? reportGoo = null;
        if (!DA.GetData(0, ref reportGoo) || reportGoo?.Value is null)
        {
            _currentReport = null;
            return;
        }

        _currentReport = reportGoo.Value;
        DA.SetData(0, _currentReport.Passed);
        DA.SetData(1, _currentReport.Failed);
        DA.SetData(2, _currentReport.Errors);
    }

    internal TestReport? CurrentReport => _currentReport;

    private class ReportViewerAttributes : GH_ComponentAttributes
    {
        private const float DonutSize = 80f;
        private const float DonutThickness = 18f;
        private const float Padding = 10f;
        private const float BoxWidth = 36f;
        private const float OutputSpacing = 20f;
        private const float GripRadius = 4f;
        private const float CornerRadius = 3f;
        private const float HighlightHeight = 8f;

        // GH standard colors (normal palette, unselected)
        private static readonly Color FillColor = Color.FromArgb(255, 200, 200, 200);
        private static readonly Color EdgeColor = Color.FromArgb(255, 50, 50, 50);
        private static readonly Color TextColor = Color.FromArgb(255, 0, 0, 0);
        private static readonly Color HighlightColor = Color.FromArgb(60, 255, 255, 255);

        public ReportViewerAttributes(ReportViewerComponent owner) : base(owner) { }

        protected override void Layout()
        {
            float pivotX = Pivot.X;
            float pivotY = Pivot.Y;

            float donutCenterX = pivotX + Padding + DonutSize / 2f;
            float donutCenterY = pivotY + Padding + DonutSize / 2f;
            float innerRadius = DonutSize / 2f - DonutThickness;

            // Box starts where the inner circle ends on the right side
            float boxLeft = donutCenterX + innerRadius + 2f;
            float boxHeight = OutputSpacing * 2 + 20f;
            float boxTop = donutCenterY - boxHeight / 2f;
            float boxRight = boxLeft + BoxWidth;

            float totalWidth = boxRight + Padding - pivotX;
            float totalHeight = Padding + DonutSize + Padding;

            Bounds = new RectangleF(pivotX, pivotY, totalWidth, totalHeight);

            float midY = pivotY + totalHeight / 2f;

            // Input grip at left edge of donut
            var input = Owner.Params.Input[0];
            input.Attributes.Pivot = new PointF(pivotX + Padding, midY);
            input.Attributes.Bounds = new RectangleF(pivotX + Padding - 3, midY - 10, 6, 20);

            // Output grips below the box on the right edge
            float outputX = boxRight;
            float outputStartY = midY - OutputSpacing;
            for (int i = 0; i < Owner.Params.Output.Count; i++)
            {
                var output = Owner.Params.Output[i];
                float oy = outputStartY + i * OutputSpacing;
                output.Attributes.Pivot = new PointF(outputX, oy);
                float labelLeft = boxLeft + 12f;
                output.Attributes.Bounds = new RectangleF(labelLeft, oy - 10, boxRight - labelLeft + 3, 20);
            }
        }

        protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
        {
            if (channel == GH_CanvasChannel.Wires)
            {
                Owner.Params.Input[0].Attributes.RenderToCanvas(canvas, channel);
                foreach (var param in Owner.Params.Output)
                    param.Attributes.RenderToCanvas(canvas, channel);
                return;
            }

            if (channel != GH_CanvasChannel.Objects)
            {
                base.Render(canvas, graphics, channel);
                return;
            }

            graphics.SmoothingMode = SmoothingMode.AntiAlias;

            var bounds = Bounds;
            float donutCenterX = bounds.X + Padding + DonutSize / 2f;
            float donutCenterY = bounds.Y + Padding + DonutSize / 2f;
            float outerRadius = DonutSize / 2f;
            float innerRadius = outerRadius - DonutThickness;
            float midY = bounds.Y + bounds.Height / 2f;

            // Box geometry
            float boxLeft = donutCenterX + innerRadius + 2f;
            float boxHeight = OutputSpacing * 2 + 20f;
            float boxTop = donutCenterY - boxHeight / 2f;

            var boxRect = new RectangleF(boxLeft, boxTop, BoxWidth, boxHeight);

            // 1. Draw input grip
            using (var gripBrush = new SolidBrush(Color.White))
            using (var gripPen = new Pen(Color.Black, 2f))
            {
                var gripPt = Owner.Params.Input[0].Attributes.Pivot;
                var gripRect = new RectangleF(gripPt.X - GripRadius, gripPt.Y - GripRadius, GripRadius * 2, GripRadius * 2);
                graphics.FillEllipse(gripBrush, gripRect);
                graphics.DrawEllipse(gripPen, gripRect);
            }

            // 2. Draw output grips (behind the capsule)
            using (var gripBrush = new SolidBrush(Color.White))
            using (var gripPen = new Pen(Color.Black, 2f))
            {
                foreach (var param in Owner.Params.Output)
                {
                    var oPt = param.Attributes.Pivot;
                    var oRect = new RectangleF(oPt.X - GripRadius, oPt.Y - GripRadius, GripRadius * 2, GripRadius * 2);
                    graphics.FillEllipse(gripBrush, oRect);
                    graphics.DrawEllipse(gripPen, oRect);
                }
            }

            // 3. Draw box body (GH-style capsule, on top of output grips)
            using (var boxPath = RoundedRect(boxRect, CornerRadius))
            {
                using (var fillBrush = new SolidBrush(FillColor))
                    graphics.FillPath(fillBrush, boxPath);

                var highlightRect = new RectangleF(boxRect.X, boxRect.Y, boxRect.Width, HighlightHeight);
                using (var clipPath = RoundedRect(boxRect, CornerRadius))
                {
                    var oldClip = graphics.Clip;
                    graphics.SetClip(clipPath, CombineMode.Intersect);
                    using (var highlightBrush = new SolidBrush(HighlightColor))
                        graphics.FillRectangle(highlightBrush, highlightRect);
                    graphics.Clip = oldClip;
                }

                using (var edgePen = new Pen(EdgeColor, 1f))
                    graphics.DrawPath(edgePen, boxPath);
            }

            // 4. Draw P, F, E labels (shifted right to avoid donut clipping)
            string[] labels = { "P", "F", "E" };
            var labelFont = GH_FontServer.Small;
            using var labelBrush = new SolidBrush(TextColor);
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            for (int i = 0; i < Owner.Params.Output.Count; i++)
            {
                var oPt = Owner.Params.Output[i].Attributes.Pivot;
                float labelLeft = boxRect.X + 12f;
                var labelRect = new RectangleF(labelLeft, oPt.Y - 8f, boxRect.Right - labelLeft, 16f);
                graphics.DrawString(labels[i], labelFont, labelBrush, labelRect, sf);
            }

            // Draw donut (on top of the box)
            var comp = (ReportViewerComponent)Owner;
            var report = comp.CurrentReport;

            if (report is null || report.Total == 0)
            {
                DrawDonutSegment(graphics, donutCenterX, donutCenterY, outerRadius, innerRadius,
                    0f, 360f, Color.FromArgb(255, 200, 200, 200));
            }
            else
            {
                float total = report.Total;
                float startAngle = -90f;

                if (report.Passed > 0)
                {
                    float sweep = report.Passed / total * 360f;
                    DrawDonutSegment(graphics, donutCenterX, donutCenterY, outerRadius, innerRadius,
                        startAngle, sweep, Color.FromArgb(255, 76, 175, 80));
                    startAngle += sweep;
                }

                if (report.Failed > 0)
                {
                    float sweep = report.Failed / total * 360f;
                    DrawDonutSegment(graphics, donutCenterX, donutCenterY, outerRadius, innerRadius,
                        startAngle, sweep, Color.FromArgb(255, 229, 57, 53));
                    startAngle += sweep;
                }

                if (report.Errors > 0)
                {
                    float sweep = report.Errors / total * 360f;
                    DrawDonutSegment(graphics, donutCenterX, donutCenterY, outerRadius, innerRadius,
                        startAngle, sweep, Color.FromArgb(255, 255, 193, 7));
                }
            }

            // Donut outline (always drawn)
            using (var outlinePen = new Pen(EdgeColor, 1f))
            {
                var outerRect = new RectangleF(donutCenterX - outerRadius, donutCenterY - outerRadius, outerRadius * 2, outerRadius * 2);
                var innerRect = new RectangleF(donutCenterX - innerRadius, donutCenterY - innerRadius, innerRadius * 2, innerRadius * 2);
                graphics.DrawEllipse(outlinePen, outerRect);
                graphics.DrawEllipse(outlinePen, innerRect);
            }
        }

        private static void DrawDonutSegment(Graphics g, float cx, float cy,
            float outerR, float innerR, float startAngle, float sweepAngle, Color color)
        {
            using var path = new GraphicsPath();

            var outerRect = new RectangleF(cx - outerR, cy - outerR, outerR * 2, outerR * 2);
            var innerRect = new RectangleF(cx - innerR, cy - innerR, innerR * 2, innerR * 2);

            path.AddArc(outerRect, startAngle, sweepAngle);
            path.AddArc(innerRect, startAngle + sweepAngle, -sweepAngle);
            path.CloseFigure();

            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(color);
            g.FillPath(brush, path);
        }

        private static GraphicsPath RoundedRect(RectangleF rect, float radius)
        {
            float d = radius * 2f;
            var path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
