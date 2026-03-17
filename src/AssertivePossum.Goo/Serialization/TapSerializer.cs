using System.Text;

namespace AssertivePossum.Goo.Serialization;

/// <summary>
/// Serializes a <see cref="TestReport"/> to TAP (Test Anything Protocol) version 13.
/// </summary>
public static class TapSerializer
{
    public static string Serialize(TestReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("TAP version 13");
        sb.AppendLine($"1..{report.Total}");

        for (int i = 0; i < report.Results.Count; i++)
        {
            int number = i + 1;
            var result = report.Results[i];

            if (result.Status == TestStatus.Pass)
            {
                sb.AppendLine($"ok {number} - {result.TestName}");
            }
            else
            {
                sb.AppendLine($"not ok {number} - {result.TestName}");
                sb.AppendLine("  ---");
                sb.AppendLine($"  message: {result.Message}");

                if (result.Status == TestStatus.Fail)
                {
                    if (result.Expected is not null)
                        sb.AppendLine($"  expected: {result.Expected}");
                    if (result.Actual is not null)
                        sb.AppendLine($"  actual: {result.Actual}");
                }

                sb.AppendLine($"  severity: {(result.Status == TestStatus.Error ? "error" : "fail")}");
                sb.AppendLine("  ---");
            }
        }

        return sb.ToString().TrimEnd();
    }
}
