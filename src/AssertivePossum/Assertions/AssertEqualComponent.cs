using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using AssertivePossum.Goo;

namespace AssertivePossum.Components.Assertions;

public class AssertEqualComponent : GH_Component
{
    public AssertEqualComponent()
        : base("Assert Equal", "AEq",
            "Asserts that two values are equal within an optional tolerance.",
            "Assertive Possum", "1. Assertions")
    {
    }

    public override Guid ComponentGuid => new("a1b2c3d4-0001-4000-8000-000000000001");
    public override GH_Exposure Exposure => GH_Exposure.secondary;

    public override void CreateAttributes() => m_attributes = new AssertComponentAttributes(this);

    protected override System.Drawing.Bitmap? Icon =>
        new System.Drawing.Bitmap(GetType().Assembly.GetManifestResourceStream("Icons.assert-equal.png")!);

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Name", "N", "Test name.", GH_ParamAccess.item);
        pManager.AddGenericParameter("Actual", "A", "Actual value.", GH_ParamAccess.item);
        pManager.AddGenericParameter("Expected", "E", "Expected value.", GH_ParamAccess.item);
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
            bool pass = AreEqual(actual, expected, tolerance, out string message);

            DA.SetData(0, new TestResultGoo(new TestResult
            {
                TestName = name,
                Status = pass ? TestStatus.Pass : TestStatus.Fail,
                Message = pass ? null : message,
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

    internal static bool AreEqual(object? actual, object? expected, double tolerance, out string message)
    {
        if (actual is null && expected is null)
        {
            message = string.Empty;
            return true;
        }

        if (actual is null || expected is null)
        {
            message = $"Expected <{expected ?? "null"}> but got <{actual ?? "null"}>.";
            return false;
        }

        // Numeric comparison
        if (IsNumeric(actual) && IsNumeric(expected))
        {
            double a = Convert.ToDouble(actual);
            double e = Convert.ToDouble(expected);
            if (Math.Abs(a - e) <= tolerance)
            {
                message = string.Empty;
                return true;
            }
            message = $"Expected <{e}> but got <{a}> (tolerance {tolerance}).";
            return false;
        }

        // Point3d
        if (actual is Point3d ptA && expected is Point3d ptE)
        {
            bool pass = Math.Abs(ptA.X - ptE.X) <= tolerance
                     && Math.Abs(ptA.Y - ptE.Y) <= tolerance
                     && Math.Abs(ptA.Z - ptE.Z) <= tolerance;
            message = pass ? string.Empty : $"Expected <{ptE}> but got <{ptA}> (tolerance {tolerance}).";
            return pass;
        }

        // Vector3d
        if (actual is Vector3d vA && expected is Vector3d vE)
        {
            bool pass = Math.Abs(vA.X - vE.X) <= tolerance
                     && Math.Abs(vA.Y - vE.Y) <= tolerance
                     && Math.Abs(vA.Z - vE.Z) <= tolerance;
            message = pass ? string.Empty : $"Expected <{vE}> but got <{vA}> (tolerance {tolerance}).";
            return pass;
        }

        // String
        if (actual is string sA && expected is string sE)
        {
            if (string.Equals(sA, sE, StringComparison.Ordinal))
            {
                message = string.Empty;
                return true;
            }
            message = $"Expected <\"{sE}\"> but got <\"{sA}\"> (ordinal comparison).";
            return false;
        }

        // Lists
        if (actual is IList<object> listA && expected is IList<object> listE)
        {
            if (listA.Count != listE.Count)
            {
                message = $"List lengths differ: expected {listE.Count} but got {listA.Count}.";
                return false;
            }
            for (int i = 0; i < listA.Count; i++)
            {
                if (!AreEqual(listA[i], listE[i], tolerance, out string inner))
                {
                    message = $"Element [{i}]: {inner}";
                    return false;
                }
            }
            message = string.Empty;
            return true;
        }

        // Fallback
        if (actual.Equals(expected))
        {
            message = string.Empty;
            return true;
        }

        message = $"Expected <{expected}> but got <{actual}>.";
        return false;
    }

    private static bool IsNumeric(object? obj)
    {
        return obj is byte or sbyte or short or ushort or int or uint
            or long or ulong or float or double or decimal;
    }
}
