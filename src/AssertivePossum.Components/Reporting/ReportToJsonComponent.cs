using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using Grasshopper.Kernel;
using AssertivePossum.Goo;
using AssertivePossum.Goo.Serialization;

namespace AssertivePossum.Components.Reporting;

public class ReportToJsonComponent : GH_Component
{
    public ReportToJsonComponent()
        : base("Report to JSON", "JSON",
            "Serializes test reports to JSON format.",
            "Assertive Possum", "3. Reporting")
    {
    }

    public override Guid ComponentGuid => new("D4E5F6A7-B8C9-4D01-2E3F-4A5B6C7D8E9F");

    protected override Bitmap? Icon =>
        new Bitmap(GetType().Assembly.GetManifestResourceStream("Icons.report-to-json.png")!);

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddGenericParameter("Report", "R", "Test report(s) to serialize.", GH_ParamAccess.list);
        pManager.AddTextParameter("Path", "P", "Output file path.", GH_ParamAccess.item);
        pManager.AddBooleanParameter("Write", "W", "Set to true to write the file.", GH_ParamAccess.item, false);

        pManager[1].Optional = true;
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddTextParameter("JSON", "J", "The JSON output string.", GH_ParamAccess.item);
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

        string json = TestReportJsonSerializer.Serialize(reports);
        DA.SetData(0, json);

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

                File.WriteAllText(path, json);
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
