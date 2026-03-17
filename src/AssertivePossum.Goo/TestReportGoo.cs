using GH_IO.Serialization;
using Grasshopper.Kernel.Types;

namespace AssertivePossum.Goo;

/// <summary>
/// Grasshopper wrapper (Goo) for <see cref="TestReport"/>.
/// </summary>
public class TestReportGoo : GH_Goo<TestReport>
{
    public TestReportGoo()
    {
        Value = new TestReport();
    }

    public TestReportGoo(TestReport report)
    {
        Value = report ?? new TestReport();
    }

    public override bool IsValid => Value is not null && Value.Results.Count > 0;

    public override string TypeName => "Test Report";

    public override string TypeDescription => "A collection of test results from a single test run.";

    public override IGH_Goo Duplicate()
    {
        var copy = new TestReport
        {
            SourceFile = Value.SourceFile,
            Timestamp = Value.Timestamp,
            TotalTimeMs = Value.TotalTimeMs,
            Results = Value.Results.Select(r => new TestResult
            {
                TestName = r.TestName,
                Status = r.Status,
                Message = r.Message,
                Expected = r.Expected,
                Actual = r.Actual,
                ElapsedMs = r.ElapsedMs,
                ComponentId = r.ComponentId
            }).ToList()
        };

        return new TestReportGoo(copy);
    }

    public override string ToString()
    {
        if (Value is null) return "Null TestReport";

        var status = Value.AllPassed ? "ALL PASSED" : "FAILURES";
        return $"TestReport [{status}] {Value.Passed}P / {Value.Failed}F / {Value.Errors}E ({Value.Total} total, {Value.TotalTimeMs:F1}ms)";
    }

    public override bool CastTo<T>(ref T target)
    {
        if (typeof(T) == typeof(string))
        {
            target = (T)(object)ToString();
            return true;
        }

        return base.CastTo(ref target);
    }

    public override bool Write(GH_IWriter writer)
    {
        writer.SetString("SourceFile", Value.SourceFile ?? string.Empty);
        writer.SetDate("Timestamp", Value.Timestamp);
        writer.SetDouble("TotalTimeMs", Value.TotalTimeMs);
        writer.SetInt32("ResultCount", Value.Results.Count);

        for (int i = 0; i < Value.Results.Count; i++)
        {
            var chunk = writer.CreateChunk("Result", i);
            var goo = new TestResultGoo(Value.Results[i]);
            goo.Write(chunk);
        }

        return base.Write(writer);
    }

    public override bool Read(GH_IReader reader)
    {
        Value = new TestReport
        {
            SourceFile = reader.GetString("SourceFile"),
            Timestamp = reader.GetDate("Timestamp"),
            TotalTimeMs = reader.GetDouble("TotalTimeMs")
        };

        int count = reader.GetInt32("ResultCount");
        for (int i = 0; i < count; i++)
        {
            var chunk = reader.FindChunk("Result", i);
            if (chunk is null) continue;

            var goo = new TestResultGoo();
            goo.Read(chunk);
            Value.Results.Add(goo.Value);
        }

        return base.Read(reader);
    }
}
