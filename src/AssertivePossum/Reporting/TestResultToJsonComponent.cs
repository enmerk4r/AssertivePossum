using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Grasshopper.Kernel;
using AssertivePossum.Goo;

namespace AssertivePossum.Components.Reporting;

public class TestResultToJsonComponent : GH_Component
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public TestResultToJsonComponent()
        : base("Test Result to JSON", "TR→J",
            "Converts a test result to a JSON string for Rhino.Compute interop.",
            "Assertive Possum", "3. Reporting")
    {
    }

    public override Guid ComponentGuid => new("1A2B3C4D-5E6F-4A7B-8C9D-0E1F2A3B4C5D");
    public override GH_Exposure Exposure => GH_Exposure.tertiary;

    protected override System.Drawing.Bitmap? Icon =>
        new System.Drawing.Bitmap(GetType().Assembly.GetManifestResourceStream("Icons.test-result-to-json.png")!);

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddGenericParameter("Result", "R", "Test result to serialize.", GH_ParamAccess.item);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddTextParameter("JSON", "J", "JSON string representation of the test result.", GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess DA)
    {
        TestResultGoo? resultGoo = null;
        if (!DA.GetData(0, ref resultGoo) || resultGoo?.Value is null)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No valid test result provided.");
            return;
        }

        DA.SetData(0, JsonSerializer.Serialize(resultGoo.Value, JsonOptions));
    }
}
