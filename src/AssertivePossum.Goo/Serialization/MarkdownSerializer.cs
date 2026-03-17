using System.Text;

namespace AssertivePossum.Goo.Serialization;

/// <summary>
/// Serializes a <see cref="TestReport"/> to a Markdown table.
/// </summary>
public static class MarkdownSerializer
{
    public static string Serialize(TestReport report) => Serialize(new[] { report });

    public static string Serialize(IEnumerable<TestReport> reports)
    {
        var sb = new StringBuilder();
        var reportList = reports.ToList();

        int totalPassed = reportList.Sum(r => r.Passed);
        int totalFailed = reportList.Sum(r => r.Failed);
        int totalErrors = reportList.Sum(r => r.Errors);
        int totalCount = reportList.Sum(r => r.Total);

        sb.AppendLine("# Test Results");
        sb.AppendLine();
        sb.AppendLine($"**{totalPassed}** passed, **{totalFailed}** failed, **{totalErrors}** errors ({totalCount} total)");
        sb.AppendLine();

        foreach (var report in reportList)
        {
            string status = report.AllPassed ? "PASSED" : "FAILED";
            sb.AppendLine($"## {report.SourceFile ?? "N/A"} \u2014 {status}");
            sb.AppendLine();
            sb.AppendLine($"*{report.Total} tests, {report.TotalTimeMs:F1}ms*");
            sb.AppendLine();
            sb.AppendLine("| Status | Test Name | Message | Expected | Actual | Time(ms) |");
            sb.AppendLine("|--------|-----------|---------|----------|--------|----------|");

            foreach (var result in report.Results)
            {
                var statusText = result.Status switch
                {
                    TestStatus.Pass => "PASS",
                    TestStatus.Fail => "FAIL",
                    TestStatus.Error => "ERROR",
                    _ => "?"
                };

                var message = Escape(result.Message);
                var expected = Escape(result.Expected?.ToString());
                var actual = Escape(result.Actual?.ToString());

                sb.AppendLine($"| {statusText} | {Escape(result.TestName)} | {message} | {expected} | {actual} | {result.ElapsedMs:F1} |");
            }

            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Replace("|", "\\|").Replace("\n", " ").Replace("\r", "");
    }
}
