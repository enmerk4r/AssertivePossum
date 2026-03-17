using System;
using System.Diagnostics;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using AssertivePossum.Goo;

namespace AssertivePossum.Components.Assertions;

public class AssertNotNullComponent : GH_Component
{
    public AssertNotNullComponent()
        : base("Assert Not Null", "ANNl",
            "Asserts that a value is not null.",
            "Assertive Possum", "1. Assertions")
    {
    }

    public override Guid ComponentGuid => new("a1b2c3d4-0006-4000-8000-000000000006");

    protected override System.Drawing.Bitmap? Icon =>
        new System.Drawing.Bitmap(GetType().Assembly.GetManifestResourceStream("Icons.assert-not-null.png")!);

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Name", "N", "Test name.", GH_ParamAccess.item);
        pManager.AddGenericParameter("Value", "V", "Value to test for non-null.", GH_ParamAccess.item);
        pManager[1].Optional = true;
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
            object? value = null;
            bool hasData = DA.GetData(1, ref value);

            bool isNull = !hasData || value is null || AssertNullComponent.IsGooWrappingNull(value);
            bool pass = !isNull;

            sw.Stop();
            DA.SetData(0, new TestResultGoo(new TestResult
            {
                TestName = name,
                Status = pass ? TestStatus.Pass : TestStatus.Fail,
                Message = pass ? null : "Expected a non-null value but got null.",
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
