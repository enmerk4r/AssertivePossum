namespace AssertivePossum.Goo;

/// <summary>
/// Aggregates the results of an entire test run.
/// </summary>
public class TestReport
{
    public string? SourceFile { get; set; }
    public DateTime Timestamp { get; set; }
    public double TotalTimeMs { get; set; }
    public List<TestResult> Results { get; set; } = new();

    public int Passed => Results.Count(r => r.Status == TestStatus.Pass);
    public int Failed => Results.Count(r => r.Status == TestStatus.Fail);
    public int Errors => Results.Count(r => r.Status == TestStatus.Error);
    public int Total => Results.Count;
    public bool AllPassed => Results.All(r => r.Status == TestStatus.Pass);

    /// <summary>
    /// Finds test results that share the same <see cref="TestResult.TestName"/>
    /// and marks every duplicate instance as <see cref="TestStatus.Fail"/>.
    /// </summary>
    public static void FailDuplicateNames(List<TestResult> results)
    {
        var duplicateNames = results
            .GroupBy(r => r.TestName, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.Ordinal);

        if (duplicateNames.Count == 0) return;

        foreach (var result in results)
        {
            if (duplicateNames.Contains(result.TestName))
            {
                result.Status = TestStatus.Fail;
                result.Message = $"Duplicate test name '{result.TestName}'. Each assertion in a file must have a unique name.";
            }
        }
    }
}
