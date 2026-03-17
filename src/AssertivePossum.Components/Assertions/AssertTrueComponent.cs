using System;
using System.Diagnostics;
using Grasshopper.Kernel;
using AssertivePossum.Goo;

namespace AssertivePossum.Components.Assertions;

public class AssertTrueComponent : GH_Component
{
    public AssertTrueComponent()
        : base("Assert True", "ATru",
            "Asserts that a value is true.",
            "Assertive Possum", "1. Assertions")
    {
    }

    public override Guid ComponentGuid => new("a1b2c3d4-0003-4000-8000-000000000003");

    protected override System.Drawing.Bitmap? Icon =>
        new System.Drawing.Bitmap(GetType().Assembly.GetManifestResourceStream("Icons.assert-true.png")!);

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Name", "N", "Test name.", GH_ParamAccess.item);
        pManager.AddBooleanParameter("Value", "V", "Boolean value to test.", GH_ParamAccess.item);
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
            bool value = false;
            if (!DA.GetData(1, ref value)) return;

            sw.Stop();
            DA.SetData(0, new TestResultGoo(new TestResult
            {
                TestName = name,
                Status = value ? TestStatus.Pass : TestStatus.Fail,
                Message = value ? null : "Expected true but got false.",
                Expected = true,
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
