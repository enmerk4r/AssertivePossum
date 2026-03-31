using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AssertivePossum.Goo;

/// <summary>
/// Shared HTTP client for communicating with Rhino.Compute to run Grasshopper definitions.
/// </summary>
public sealed class ComputeClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _serverUrl;

    public ComputeClient(string serverUrl, TimeSpan? timeout = null, string? apiKey = null)
    {
        _serverUrl = serverUrl.TrimEnd('/');
        _http = new HttpClient
        {
            Timeout = timeout ?? TimeSpan.FromSeconds(120)
        };
        _http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        if (!string.IsNullOrWhiteSpace(apiKey))
            _http.DefaultRequestHeaders.Add("RhinoComputeKey", apiKey);
    }

    /// <summary>
    /// Sends a .gh file to Rhino.Compute for evaluation and returns the raw JSON response.
    /// </summary>
    /// <param name="ghFilePath">Absolute path to a .gh definition file.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The parsed JSON response as a <see cref="JsonElement"/>.</returns>
    public async Task<JsonElement> PostDefinitionAsync(string ghFilePath, CancellationToken cancellationToken = default)
    {
        byte[] fileBytes = await File.ReadAllBytesAsync(ghFilePath, cancellationToken);
        string base64 = Convert.ToBase64String(fileBytes);

        var requestBody = new ComputeRequest
        {
            Algo = base64,
            Pointer = null,
            Values = Array.Empty<object>()
        };

        string json = JsonSerializer.Serialize(requestBody, SerializerOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        HttpResponseMessage response = await _http.PostAsync(
            $"{_serverUrl}/grasshopper", content, cancellationToken);

        response.EnsureSuccessStatusCode();

        string responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(responseJson);
        return doc.RootElement.Clone();
    }

    /// <summary>
    /// Extracts <see cref="TestResult"/> objects from a Rhino.Compute response.
    /// The response is expected to contain output values with InnerTree data.
    /// </summary>
    public static List<TestResult> DeserializeTestResults(JsonElement response)
    {
        var results = new List<TestResult>();

        if (!response.TryGetProperty("values", out var values))
            return results;

        // Track output parameter names to detect duplicate Context Print components.
        var outputNames = new List<string>();

        foreach (var output in values.EnumerateArray())
        {
            string? paramName = null;
            if (output.TryGetProperty("ParamName", out var pn))
                paramName = pn.GetString();

            if (paramName is not null)
                outputNames.Add(paramName);

            if (!output.TryGetProperty("InnerTree", out var innerTree))
                continue;

            foreach (var branch in innerTree.EnumerateObject())
            {
                foreach (var item in branch.Value.EnumerateArray())
                {
                    if (!item.TryGetProperty("data", out var data))
                        continue;

                    string dataStr = data.GetString() ?? string.Empty;
                    var result = TryParseTestResult(dataStr);
                    if (result is not null)
                        results.Add(result);
                }
            }
        }

        // Detect duplicate Context Print output names. When two Context Print
        // components share a name, Compute merges them into a single output
        // and we silently lose one test result. Flag any result whose test name
        // matches a duplicated output name so the author knows to fix it.
        var duplicateOutputNames = outputNames
            .GroupBy(n => n, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.Ordinal);

        if (duplicateOutputNames.Count > 0)
        {
            // Build a set of bare names (strip "RH_OUT:" prefix if present).
            var bareDuplicates = new HashSet<string>(StringComparer.Ordinal);
            foreach (var name in duplicateOutputNames)
            {
                bareDuplicates.Add(name);
                if (name.StartsWith("RH_OUT:", StringComparison.Ordinal))
                    bareDuplicates.Add(name.Substring("RH_OUT:".Length));
            }

            foreach (var result in results)
            {
                if (bareDuplicates.Contains(result.TestName))
                {
                    result.Status = TestStatus.Fail;
                    result.Message = $"Duplicate Context Print output name '{result.TestName}'. "
                        + "Each Context Print component must have a unique name. "
                        + "One or more test results may have been lost.";
                }
            }
        }

        return results;
    }

    private static TestResult? TryParseTestResult(string data)
    {
        // Attempt to deserialize as JSON first
        try
        {
            // Strip surrounding quotes if present (Compute wraps strings in quotes)
            string trimmed = data.Trim();
            if (trimmed.StartsWith('"') && trimmed.EndsWith('"'))
                trimmed = JsonSerializer.Deserialize<string>(trimmed) ?? trimmed;

            if (trimmed.StartsWith('{'))
            {
                return JsonSerializer.Deserialize<TestResult>(trimmed, DeserializeOptions);
            }
        }
        catch (JsonException)
        {
            // Not valid JSON; ignore
        }

        return null;
    }

    public void Dispose()
    {
        _http.Dispose();
    }

    private class ComputeRequest
    {
        [JsonPropertyName("algo")]
        public string Algo { get; set; } = string.Empty;

        [JsonPropertyName("pointer")]
        public string? Pointer { get; set; }

        [JsonPropertyName("values")]
        public object[] Values { get; set; } = Array.Empty<object>();
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
}
