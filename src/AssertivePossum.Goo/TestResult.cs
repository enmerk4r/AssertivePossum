namespace AssertivePossum.Goo;

/// <summary>
/// Plain data object holding the outcome of a single test.
/// </summary>
public class TestResult
{
    public string TestName { get; set; } = string.Empty;
    public TestStatus Status { get; set; }
    public string? Message { get; set; }
    public object? Expected { get; set; }
    public object? Actual { get; set; }
    public double ElapsedMs { get; set; }
    public string? ComponentId { get; set; }
}
