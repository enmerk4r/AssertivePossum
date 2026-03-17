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
}
