using System.Text.Json;
using System.Text.Json.Serialization;
using GH_IO.Serialization;
using Grasshopper.Kernel.Types;

namespace AssertivePossum.Goo;

/// <summary>
/// Grasshopper wrapper (Goo) for <see cref="TestResult"/>.
/// </summary>
public class TestResultGoo : GH_Goo<TestResult>
{
    public TestResultGoo()
    {
        Value = new TestResult();
    }

    public TestResultGoo(TestResult result)
    {
        Value = result ?? new TestResult();
    }

    public override bool IsValid => Value is not null && !string.IsNullOrEmpty(Value.TestName);

    public override string TypeName => "Test Result";

    public override string TypeDescription => "The result of a single test assertion.";

    public override IGH_Goo Duplicate()
    {
        return new TestResultGoo(new TestResult
        {
            TestName = Value.TestName,
            Status = Value.Status,
            Message = Value.Message,
            Expected = Value.Expected,
            Actual = Value.Actual,
            ElapsedMs = Value.ElapsedMs,
            ComponentId = Value.ComponentId
        });
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public override string ToString()
    {
        if (Value is null) return "{}";
        return JsonSerializer.Serialize(Value, JsonOptions);
    }

    public override bool CastTo<T>(ref T target)
    {
        if (typeof(T) == typeof(bool))
        {
            target = (T)(object)(Value.Status == TestStatus.Pass);
            return true;
        }

        if (typeof(T) == typeof(GH_String))
        {
            target = (T)(object)new GH_String(ToString());
            return true;
        }

        if (typeof(T) == typeof(string))
        {
            target = (T)(object)ToString();
            return true;
        }

        return base.CastTo(ref target);
    }

    public override bool Write(GH_IWriter writer)
    {
        writer.SetString("TestName", Value.TestName);
        writer.SetInt32("Status", (int)Value.Status);
        writer.SetString("Message", Value.Message ?? string.Empty);
        writer.SetString("Expected", Value.Expected?.ToString() ?? string.Empty);
        writer.SetString("Actual", Value.Actual?.ToString() ?? string.Empty);
        writer.SetDouble("ElapsedMs", Value.ElapsedMs);
        writer.SetString("ComponentId", Value.ComponentId ?? string.Empty);
        return base.Write(writer);
    }

    public override bool Read(GH_IReader reader)
    {
        Value = new TestResult
        {
            TestName = reader.GetString("TestName"),
            Status = (TestStatus)reader.GetInt32("Status"),
            Message = reader.GetString("Message"),
            Expected = reader.GetString("Expected"),
            Actual = reader.GetString("Actual"),
            ElapsedMs = reader.GetDouble("ElapsedMs"),
            ComponentId = reader.GetString("ComponentId")
        };
        return base.Read(reader);
    }
}
