using System;
using System.Diagnostics;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using AssertivePossum.Goo;

namespace AssertivePossum.Components.Assertions;

public class AssertNotEqualComponent : GH_Component
{
    public AssertNotEqualComponent()
        : base("Assert Not Equal", "ANEq",
            "Asserts that two values are not equal.",
            "Assertive Possum", "1. Assertions")
    {
    }

    public override Guid ComponentGuid => new("a1b2c3d4-0002-4000-8000-000000000002");

    protected override System.Drawing.Bitmap? Icon =>
        new System.Drawing.Bitmap(GetType().Assembly.GetManifestResourceStream("Icons.assert-not-equal.png")!);

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Name", "N", "Test name.", GH_ParamAccess.item);
        pManager.AddGenericParameter("Actual", "A", "Actual value.", GH_ParamAccess.item);
        pManager.AddGenericParameter("Expected", "E", "Expected value (should differ).", GH_ParamAccess.item);
        pManager.AddNumberParameter("Tolerance", "T", "Absolute tolerance for numeric comparisons.", GH_ParamAccess.item, 1e-6);
        pManager[3].Optional = true;
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
            object? actualObj = null;
            object? expectedObj = null;
            double tolerance = 1e-6;

            if (!DA.GetData(1, ref actualObj)) return;
            if (!DA.GetData(2, ref expectedObj)) return;
            DA.GetData(3, ref tolerance);

            var actual = UnwrapGoo(actualObj);
            var expected = UnwrapGoo(expectedObj);

            sw.Stop();
            bool areEqual = AssertEqualComponent.AreEqual(actual, expected, tolerance, out _);
            bool pass = !areEqual;

            DA.SetData(0, new TestResultGoo(new TestResult
            {
                TestName = name,
                Status = pass ? TestStatus.Pass : TestStatus.Fail,
                Message = pass ? null : $"Values are equal (<{actual}>) but were expected to differ.",
                Expected = expected,
                Actual = actual,
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

    private static object? UnwrapGoo(object? obj)
    {
        if (obj is IGH_Goo goo)
            return goo.ScriptVariable();
        return obj;
    }
}
