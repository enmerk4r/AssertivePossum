using System;
using System.Diagnostics;
using Grasshopper.Kernel;
using AssertivePossum.Goo;

namespace AssertivePossum.Components.Assertions;

public class AssertInRangeComponent : GH_Component
{
    public AssertInRangeComponent()
        : base("Assert In Range", "ARng",
            "Asserts that a value falls within an inclusive range [Min, Max].",
            "Assertive Possum", "1. Assertions")
    {
    }

    public override Guid ComponentGuid => new("a1b2c3d4-0009-4000-8000-000000000009");
    public override GH_Exposure Exposure => GH_Exposure.quarternary;

    protected override System.Drawing.Bitmap? Icon =>
        new System.Drawing.Bitmap(GetType().Assembly.GetManifestResourceStream("Icons.assert-in-range.png")!);

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Name", "N", "Test name.", GH_ParamAccess.item);
        pManager.AddNumberParameter("Value", "V", "Value to test.", GH_ParamAccess.item);
        pManager.AddNumberParameter("Min", "Mi", "Minimum allowed value (inclusive).", GH_ParamAccess.item);
        pManager.AddNumberParameter("Max", "Mx", "Maximum allowed value (inclusive).", GH_ParamAccess.item);
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
            double value = 0;
            double min = 0;
            double max = 0;

            if (!DA.GetData(1, ref value)) return;
            if (!DA.GetData(2, ref min)) return;
            if (!DA.GetData(3, ref max)) return;

            sw.Stop();
            bool pass = value >= min && value <= max;

            DA.SetData(0, new TestResultGoo(new TestResult
            {
                TestName = name,
                Status = pass ? TestStatus.Pass : TestStatus.Fail,
                Message = pass ? null : $"Value {value} is outside range [{min}, {max}].",
                Expected = $"[{min}, {max}]",
                Actual = value,
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
}
