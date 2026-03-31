using System;
using System.Diagnostics;
using Grasshopper.Kernel;
using Rhino.Geometry;
using AssertivePossum.Goo;

namespace AssertivePossum.Components.Assertions;

public class AssertGeometryValidComponent : GH_Component
{
    public AssertGeometryValidComponent()
        : base("Assert Geometry Valid", "AGeo",
            "Asserts that a geometry object is non-null and valid.",
            "Assertive Possum", "1. Assertions")
    {
    }

    public override Guid ComponentGuid => new("a1b2c3d4-000a-4000-8000-00000000000a");
    public override GH_Exposure Exposure => GH_Exposure.quinary;

    public override void CreateAttributes() => m_attributes = new AssertComponentAttributes(this);

    protected override System.Drawing.Bitmap? Icon =>
        new System.Drawing.Bitmap(GetType().Assembly.GetManifestResourceStream("Icons.assert-geometry-valid.png")!);

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Name", "N", "Test name.", GH_ParamAccess.item);
        pManager.AddGeometryParameter("Geometry", "G", "Geometry to validate.", GH_ParamAccess.item);
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
            GeometryBase? geometry = null;
            if (!DA.GetData(1, ref geometry))
            {
                sw.Stop();
                DA.SetData(0, new TestResultGoo(new TestResult
                {
                    TestName = name,
                    Status = TestStatus.Fail,
                    Message = "Geometry input is null.",
                    ElapsedMs = sw.Elapsed.TotalMilliseconds,
                    ComponentId = InstanceGuid.ToString()
                }));
                return;
            }

            if (geometry is null)
            {
                sw.Stop();
                DA.SetData(0, new TestResultGoo(new TestResult
                {
                    TestName = name,
                    Status = TestStatus.Fail,
                    Message = "Geometry is null.",
                    ElapsedMs = sw.Elapsed.TotalMilliseconds,
                    ComponentId = InstanceGuid.ToString()
                }));
                return;
            }

            sw.Stop();
            bool pass = geometry.IsValid;
            string? message = null;
            if (!pass)
            {
                string reason = string.Empty;
                geometry.IsValidWithLog(out reason);
                message = $"Geometry is invalid: {reason}";
            }

            DA.SetData(0, new TestResultGoo(new TestResult
            {
                TestName = name,
                Status = pass ? TestStatus.Pass : TestStatus.Fail,
                Message = message,
                Actual = geometry.GetType().Name,
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
