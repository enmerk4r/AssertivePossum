using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using Grasshopper.Kernel;
using AssertivePossum.Goo;
using AssertivePossum.Goo.Serialization;

namespace AssertivePossum.Components.Reporting;

public class ReportToJUnitComponent : GH_Component
{
    public ReportToJUnitComponent()
        : base("Report to JUnit", "JUnt",
            "Serializes test reports to JUnit XML format.",
            "Assertive Possum", "3. Reporting")
    {
    }

    public override Guid ComponentGuid => new("B2C3D4E5-F6A7-4B89-0C1D-2E3F4A5B6C7D");

    protected override Bitmap? Icon =>
        new Bitmap(GetType().Assembly.GetManifestResourceStream("Icons.report-to-junit.png")!);

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddGenericParameter("Report", "R", "Test report(s) to serialize.", GH_ParamAccess.list);
        pManager.AddTextParameter("Path", "P", "Output file path.", GH_ParamAccess.item);
        pManager.AddBooleanParameter("Write", "W", "Set to true to write the file.", GH_ParamAccess.item, false);

        pManager[1].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddTextParameter("XML", "X", "The JUnit XML string.", GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess DA)
    {
        var reportGoos = new List<TestReportGoo>();
        if (!DA.GetDataList(0, reportGoos)) return;

        var reports = new List<TestReport>();
        foreach (var goo in reportGoos)
        {
            if (goo?.Value is not null)
                reports.Add(goo.Value);
        }

        if (reports.Count == 0)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No valid reports provided.");
            return;
        }

        string xml = JUnitSerializer.Serialize(reports);
        DA.SetData(0, xml);

        string? path = null;
        DA.GetData(1, ref path);

        bool write = false;
        DA.GetData(2, ref write);

        if (write && !string.IsNullOrWhiteSpace(path))
        {
            try
            {
                string? dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllText(path, xml);
                Message = $"Written to {Path.GetFileName(path)}";
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Failed to write file: {ex.Message}");
            }
        }
        else
        {
            Message = null;
        }
    }
}
