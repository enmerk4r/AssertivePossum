using System;
using System.Collections.Generic;
using System.Diagnostics;
using Grasshopper.Kernel;
using AssertivePossum.Goo;

namespace AssertivePossum.Components.Assertions;

public class AssertListLengthComponent : GH_Component
{
    public AssertListLengthComponent()
        : base("Assert List Length", "ALen",
            "Asserts that a list has exactly the expected number of items.",
            "Assertive Possum", "1. Assertions")
    {
    }

    public override Guid ComponentGuid => new("a1b2c3d4-0007-4000-8000-000000000007");
    public override GH_Exposure Exposure => GH_Exposure.quarternary;

    public override void CreateAttributes() => m_attributes = new AssertComponentAttributes(this);

    protected override System.Drawing.Bitmap? Icon =>
        new System.Drawing.Bitmap(GetType().Assembly.GetManifestResourceStream("Icons.assert-list-length.png")!);

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Name", "N", "Test name.", GH_ParamAccess.item);
        pManager.AddGenericParameter("List", "L", "List to measure.", GH_ParamAccess.list);
        pManager.AddIntegerParameter("Length", "Le", "Expected list length.", GH_ParamAccess.item);
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
            var list = new List<object>();
            int expectedLength = 0;

            if (!DA.GetDataList(1, list)) return;
            if (!DA.GetData(2, ref expectedLength)) return;

            sw.Stop();
            int actualLength = list.Count;
            bool pass = actualLength == expectedLength;

            DA.SetData(0, new TestResultGoo(new TestResult
            {
                TestName = name,
                Status = pass ? TestStatus.Pass : TestStatus.Fail,
                Message = pass ? null : $"Expected list length {expectedLength} but got {actualLength}.",
                Expected = expectedLength,
                Actual = actualLength,
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
