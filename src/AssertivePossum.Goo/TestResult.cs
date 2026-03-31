using System.Text.Json.Serialization;

namespace AssertivePossum.Goo;

/// <summary>
/// Plain data object holding the outcome of a single test.
/// </summary>
public class TestResult
{
    public string TestName { get; set; } = string.Empty;
    public TestStatus Status { get; set; }
    public string? Message { get; set; }

    [JsonIgnore]
    public object? Expected { get; set; }

    [JsonIgnore]
    public object? Actual { get; set; }

    [JsonPropertyName("expected")]
    public string? ExpectedSerialized => Expected?.ToString();

    [JsonPropertyName("actual")]
    public string? ActualSerialized => Actual?.ToString();

    public double ElapsedMs { get; set; }
    public string? ComponentId { get; set; }
}
