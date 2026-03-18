using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Grasshopper.Kernel;
using AssertivePossum.Goo;

namespace AssertivePossum.Components.Reporting;

public class JsonToTestResultComponent : GH_Component
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public JsonToTestResultComponent()
        : base("JSON to Test Result", "J→TR",
            "Parses a JSON string into a test result for reporting.",
            "Assertive Possum", "3. Reporting")
    {
    }

    public override Guid ComponentGuid => new("2B3C4D5E-6F7A-4B8C-9D0E-1F2A3B4C5D6E");
    public override GH_Exposure Exposure => GH_Exposure.tertiary;

    protected override System.Drawing.Bitmap? Icon =>
        new System.Drawing.Bitmap(GetType().Assembly.GetManifestResourceStream("Icons.json-to-test-result.png")!);

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("JSON", "J", "JSON string representing a test result.", GH_ParamAccess.item);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddGenericParameter("Result", "R", "Parsed test result.", GH_ParamAccess.item);
    }

    protected override void SolveInstance(IGH_DataAccess DA)
    {
        string json = string.Empty;
        if (!DA.GetData(0, ref json) || string.IsNullOrWhiteSpace(json))
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No JSON string provided.");
            return;
        }

        try
        {
            var result = JsonSerializer.Deserialize<TestResult>(json, JsonOptions);
            if (result is null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Deserialization returned null.");
                return;
            }

            DA.SetData(0, new TestResultGoo(result));
        }
        catch (JsonException ex)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Invalid JSON: {ex.Message}");
        }
    }
}
