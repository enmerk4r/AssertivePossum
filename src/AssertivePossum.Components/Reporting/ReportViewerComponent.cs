using System;
using System.Drawing;
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
        // No outputs; this component is for display only.
    }

    protected override void SolveInstance(IGH_DataAccess DA)
    {
        TestReportGoo? reportGoo = null;
        if (!DA.GetData(0, ref reportGoo) || reportGoo?.Value is null)
        {
            _currentReport = null;
            Message = "No report";
            return;
        }

        _currentReport = reportGoo.Value;

        string status = _currentReport.AllPassed ? "ALL PASSED" : "FAILURES";
        Message = $"{status}: {_currentReport.Passed}P / {_currentReport.Failed}F / {_currentReport.Errors}E ({_currentReport.Total} total)";
    }

    internal TestReport? CurrentReport => _currentReport;

    private class ReportViewerAttributes : GH_ComponentAttributes
    {
        public ReportViewerAttributes(ReportViewerComponent owner) : base(owner) { }

        protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
        {
            base.Render(canvas, graphics, channel);

            if (channel != GH_CanvasChannel.Objects) return;

            var comp = (ReportViewerComponent)Owner;
            var report = comp.CurrentReport;
            if (report is null) return;

            // Draw a colored status bar below the component
            Color barColor;
            if (report.Failed > 0)
                barColor = Color.FromArgb(200, 220, 60, 60);   // Red for failures
            else if (report.Errors > 0)
                barColor = Color.FromArgb(200, 220, 180, 40);  // Yellow for errors only
            else
                barColor = Color.FromArgb(200, 60, 180, 60);   // Green for all pass

            var bounds = Bounds;
            var barRect = new RectangleF(bounds.X, bounds.Bottom - 4, bounds.Width, 4);

            using var brush = new SolidBrush(barColor);
            graphics.FillRectangle(brush, barRect);
        }
    }
}
