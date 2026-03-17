using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using AssertivePossum.Goo;

namespace AssertivePossum.Components.Assertions;

public class AssertContainsComponent : GH_Component
{
    public AssertContainsComponent()
        : base("Assert Contains", "ACnt",
            "Asserts that a list contains a specific item.",
            "Assertive Possum", "1. Assertions")
    {
    }

    public override Guid ComponentGuid => new("a1b2c3d4-0008-4000-8000-000000000008");
    public override GH_Exposure Exposure => GH_Exposure.quarternary;

    protected override System.Drawing.Bitmap? Icon =>
        new System.Drawing.Bitmap(GetType().Assembly.GetManifestResourceStream("Icons.assert-contains.png")!);

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Name", "N", "Test name.", GH_ParamAccess.item);
        pManager.AddGenericParameter("List", "L", "List to search.", GH_ParamAccess.list);
        pManager.AddGenericParameter("Item", "I", "Item to find.", GH_ParamAccess.item);
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
            object? itemObj = null;

            if (!DA.GetDataList(1, list)) return;
            if (!DA.GetData(2, ref itemObj)) return;

            var item = UnwrapGoo(itemObj);
            bool found = list.Select(UnwrapGoo).Any(el =>
                el is null && item is null ||
                el is not null && el.Equals(item));

            sw.Stop();
            DA.SetData(0, new TestResultGoo(new TestResult
            {
                TestName = name,
                Status = found ? TestStatus.Pass : TestStatus.Fail,
                Message = found ? null : $"Item <{item ?? "null"}> not found in list of {list.Count} elements.",
                Actual = item,
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
