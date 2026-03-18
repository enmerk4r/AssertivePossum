using System;
using System.Diagnostics;
using Grasshopper.Kernel;
using AssertivePossum.Goo;

namespace AssertivePossum.Components.Assertions;

public class AssertElapsedTimeComponent : GH_Component
{
    public AssertElapsedTimeComponent()
        : base("Assert Elapsed Time", "ATim",
            "Asserts that the solution elapsed time does not exceed a threshold.",
            "Assertive Possum", "1. Assertions")
    {
    }

    public override Guid ComponentGuid => new("a1b2c3d4-000b-4000-8000-00000000000b");
    public override GH_Exposure Exposure => GH_Exposure.quarternary;

    protected override System.Drawing.Bitmap? Icon =>
        new System.Drawing.Bitmap(GetType().Assembly.GetManifestResourceStream("Icons.assert-elapsed-time.png")!);

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Name", "N", "Test name.", GH_ParamAccess.item);
        pManager.AddGenericParameter("Value", "V", "Any value to create a dependency on upstream components.", GH_ParamAccess.item);
        pManager[1].Optional = true;
        pManager.AddNumberParameter("MaxMs", "Ms", "Maximum allowed elapsed time in milliseconds.", GH_ParamAccess.item);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddGenericParameter("Result", "R", "Test result.", GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess DA)
    {
        string name = string.Empty;
        DA.GetData(0, ref name);

        var sw = Stopwatch.StartNew();
        try
        {
            // Read optional trigger input (we don't use the value)
            object? trigger = null;
            DA.GetData(1, ref trigger);

            double maxMs = 0;
            if (!DA.GetData(2, ref maxMs)) return;

            // Measure time from the document's solution start to now
            var now = DateTime.UtcNow;
            var doc = OnPingDocument();
            double elapsedMs;
            if (doc != null)
            {
                var solutionStart = doc.Properties.Date;
                // Use reflection or fallback: GH_Document tracks solution start internally.
                // The most reliable approach is to use the ProcessorTime from SolutionStart event,
                // but since we have the document, we measure from its recorded start.
                elapsedMs = (now - GetSolutionStart(doc)).TotalMilliseconds;
            }
            else
            {
                elapsedMs = 0;
            }

            sw.Stop();
            bool pass = elapsedMs <= maxMs;

            DA.SetData(0, new TestResultGoo(new TestResult
            {
                TestName = name,
                Status = pass ? TestStatus.Pass : TestStatus.Fail,
                Message = pass ? null : $"Elapsed {elapsedMs:F1} ms exceeds maximum {maxMs:F1} ms.",
                Expected = maxMs,
                Actual = elapsedMs,
                ElapsedMs = sw.Elapsed.TotalMilliseconds,
                ComponentId = InstanceGuid.ToString()
            }));
        }
        catch (Exception ex)
        {
            sw.Stop();
            DA.SetData(0, new TestResultGoo(new TestResult
            {
                TestName = name,
                Status = TestStatus.Error,
                Message = ex.Message,
                ElapsedMs = sw.Elapsed.TotalMilliseconds,
                ComponentId = InstanceGuid.ToString()
            }));
        }
    }

    private static DateTime GetSolutionStart(GH_Document doc)
    {
        // GH_Document.SolutionStart is a DateTime available during solution.
        // Access via the Properties.Date as a reasonable proxy, or use reflection
        // to get the internal solution-start timestamp.
        try
        {
            var prop = typeof(GH_Document).GetProperty("SolutionStart",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);
            if (prop != null)
            {
                var val = prop.GetValue(doc);
                if (val is DateTime dt) return dt;
            }
        }
        catch
        {
            // Swallow reflection failures
        }

        // Fallback: use document date
        return doc.Properties.Date;
    }
}
