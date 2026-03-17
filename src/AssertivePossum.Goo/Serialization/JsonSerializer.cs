using System.Text.Json;
using System.Text.Json.Serialization;

namespace AssertivePossum.Goo.Serialization;

/// <summary>
/// Serializes a <see cref="TestReport"/> to indented JSON using System.Text.Json.
/// Named TestReportJsonSerializer to avoid conflict with System.Text.Json.JsonSerializer.
/// </summary>
public static class TestReportJsonSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string Serialize(TestReport report)
    {
        var dto = ToDto(report);
        return JsonSerializer.Serialize(dto, Options);
    }

    public static string Serialize(IEnumerable<TestReport> reports)
    {
        var dtos = reports.Select(ToDto).ToArray();
        return JsonSerializer.Serialize(dtos, Options);
    }

    private static TestReportDto ToDto(TestReport report)
    {
        return new TestReportDto
        {
            SourceFile = report.SourceFile,
            Timestamp = report.Timestamp,
            TotalTimeMs = report.TotalTimeMs,
            Passed = report.Passed,
            Failed = report.Failed,
            Errors = report.Errors,
            Total = report.Total,
            AllPassed = report.AllPassed,
            Results = report.Results.Select(r => new TestResultDto
            {
                TestName = r.TestName,
                Status = r.Status,
                Message = r.Message,
                Expected = r.Expected?.ToString(),
                Actual = r.Actual?.ToString(),
                ElapsedMs = r.ElapsedMs,
                ComponentId = r.ComponentId
            }).ToList()
        };
    }

    private class TestReportDto
    {
        public string? SourceFile { get; set; }
        public DateTime Timestamp { get; set; }
        public double TotalTimeMs { get; set; }
        public int Passed { get; set; }
        public int Failed { get; set; }
        public int Errors { get; set; }
        public int Total { get; set; }
        public bool AllPassed { get; set; }
        public List<TestResultDto> Results { get; set; } = new();
    }

    private class TestResultDto
    {
        public string TestName { get; set; } = string.Empty;
        public TestStatus Status { get; set; }
        public string? Message { get; set; }
        public string? Expected { get; set; }
        public string? Actual { get; set; }
        public double ElapsedMs { get; set; }
        public string? ComponentId { get; set; }
    }
}
