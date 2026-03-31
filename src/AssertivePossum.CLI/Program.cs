using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AssertivePossum.Goo;
using AssertivePossum.Goo.Serialization;

namespace AssertivePossum.CLI;

internal class Program
{
    private static async Task<int> Main(string[] args)
    {
        var pathArgument = new Argument<string>("path", "Path to a .gh file or folder of .gh files.");

        var serverOption = new Option<string>("--server", () => "http://localhost:6500", "Rhino.Compute server URL.");
        var apiKeyOption = new Option<string?>("--apikey", "API key for Rhino.Compute (RhinoComputeKey header).");
        var formatOption = new Option<string>("--format", () => "text", "Output format: text, junit, tap, json, markdown.");
        var outputOption = new Option<string?>("--output", "Write report to file instead of stdout.");
        var recurseOption = new Option<bool>("--recurse", () => true, "Recurse into subfolders.");
        var noRecurseOption = new Option<bool>("--no-recurse", "Do not recurse into subfolders.");
        var timeoutOption = new Option<int>("--timeout", () => 60, "Per-file solve timeout in seconds.");
        var parallelOption = new Option<int>("--parallel", () => 1, "Number of files to solve in parallel.");
        var verboseOption = new Option<bool>("--verbose", "Show individual test results even for passing files.");

        var runCommand = new Command("run", "Run Grasshopper test definitions via Rhino.Compute.")
        {
            pathArgument,
            serverOption,
            apiKeyOption,
            formatOption,
            outputOption,
            recurseOption,
            noRecurseOption,
            timeoutOption,
            parallelOption,
            verboseOption
        };

        runCommand.SetHandler(async (InvocationContext ctx) =>
        {
            string path = ctx.ParseResult.GetValueForArgument(pathArgument);
            string server = ctx.ParseResult.GetValueForOption(serverOption)!;
            string? apiKey = ctx.ParseResult.GetValueForOption(apiKeyOption);
            string format = ctx.ParseResult.GetValueForOption(formatOption)!;
            string? output = ctx.ParseResult.GetValueForOption(outputOption);
            bool recurse = ctx.ParseResult.GetValueForOption(recurseOption);
            bool noRecurse = ctx.ParseResult.GetValueForOption(noRecurseOption);
            int timeout = ctx.ParseResult.GetValueForOption(timeoutOption);
            int parallel = ctx.ParseResult.GetValueForOption(parallelOption);
            bool verbose = ctx.ParseResult.GetValueForOption(verboseOption);

            if (noRecurse) recurse = false;

            ctx.ExitCode = await RunTests(path, server, apiKey, format, output, recurse, timeout, parallel, verbose);
        });

        var rootCommand = new RootCommand("Assertive Possum - Integration testing framework for Grasshopper.")
        {
            runCommand
        };

        return await rootCommand.InvokeAsync(args);
    }

    private static async Task<int> RunTests(
        string path, string server, string? apiKey, string format, string? output,
        bool recurse, int timeout, int parallel, bool verbose)
    {
        string version = GetVersionString();
        Console.WriteLine(version);
        Console.WriteLine($"Server: {server}");

        // Enumerate .gh files
        List<string> files;
        try
        {
            files = EnumerateGhFiles(path, recurse);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 2;
        }

        if (files.Count == 0)
        {
            Console.Error.WriteLine("Error: No .gh files found at the specified path.");
            return 2;
        }

        Console.WriteLine($"Running {files.Count} file{(files.Count == 1 ? "" : "s")}...");
        Console.WriteLine();

        var reports = new List<TestReport>();
        var totalStopwatch = Stopwatch.StartNew();

        using var client = new ComputeClient(server, TimeSpan.FromSeconds(timeout), apiKey);

        try
        {
            if (parallel > 1)
            {
                reports = await RunParallel(client, files, parallel);
            }
            else
            {
                reports = await RunSequential(client, files);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 2;
        }

        totalStopwatch.Stop();
        double totalMs = totalStopwatch.Elapsed.TotalMilliseconds;

        // Always print human-readable results to console
        PrintTextOutput(reports, verbose, totalMs);

        // If a structured format was requested, serialize and write to file or stdout
        if (!format.Equals("text", StringComparison.OrdinalIgnoreCase))
        {
            string serialized = format.ToLowerInvariant() switch
            {
                "junit" => JUnitSerializer.Serialize(reports),
                "tap" => SerializeTap(reports),
                "json" => TestReportJsonSerializer.Serialize(reports),
                "markdown" => MarkdownSerializer.Serialize(reports),
                _ => throw new InvalidOperationException($"Unknown format: {format}")
            };

            if (!string.IsNullOrWhiteSpace(output))
            {
                string? dir = Path.GetDirectoryName(output);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                await File.WriteAllTextAsync(output, serialized);
                Console.WriteLine();
                Console.WriteLine($"Test report written to {output}");
            }
            else
            {
                Console.WriteLine(serialized);
            }
        }

        bool allPassed = reports.All(r => r.AllPassed);
        bool hasErrors = reports.Any(r => r.Errors > 0);

        if (allPassed) return 0;
        if (hasErrors && reports.All(r => r.Failed == 0)) return 2;
        return 1;
    }

    private static async Task<List<TestReport>> RunSequential(ComputeClient client, List<string> files)
    {
        var reports = new List<TestReport>();

        foreach (string file in files)
        {
            var report = await SolveFile(client, file);
            reports.Add(report);
        }

        return reports;
    }

    private static async Task<List<TestReport>> RunParallel(ComputeClient client, List<string> files, int maxParallel)
    {
        var semaphore = new SemaphoreSlim(maxParallel);
        var tasks = files.Select(async file =>
        {
            await semaphore.WaitAsync();
            try
            {
                return await SolveFile(client, file);
            }
            finally
            {
                semaphore.Release();
            }
        }).ToList();

        var results = await Task.WhenAll(tasks);
        return results.ToList();
    }

    private static async Task<TestReport> SolveFile(ComputeClient client, string filePath)
    {
        var sw = Stopwatch.StartNew();
        var report = new TestReport
        {
            SourceFile = Path.GetFileName(filePath),
            Timestamp = DateTime.UtcNow
        };

        try
        {
            var response = await client.PostDefinitionAsync(filePath);
            var testResults = ComputeClient.DeserializeTestResults(response);

            if (testResults.Count > 0)
            {
                TestReport.FailDuplicateNames(testResults);
                report.Results.AddRange(testResults);
            }
            else
            {
                report.Results.Add(new TestResult
                {
                    TestName = $"{report.SourceFile} \u2014 No Assertions",
                    Status = TestStatus.Error,
                    Message = "No TestResult objects found in definition outputs."
                });
            }
        }
        catch (HttpRequestException ex)
        {
            report.Results.Add(new TestResult
            {
                TestName = $"{report.SourceFile} \u2014 Connection Error",
                Status = TestStatus.Error,
                Message = ex.Message
            });
        }
        catch (TaskCanceledException)
        {
            report.Results.Add(new TestResult
            {
                TestName = $"{report.SourceFile} \u2014 Timeout",
                Status = TestStatus.Error,
                Message = "The solve request timed out."
            });
        }
        catch (Exception ex)
        {
            report.Results.Add(new TestResult
            {
                TestName = $"{report.SourceFile} \u2014 Solve Error",
                Status = TestStatus.Error,
                Message = ex.Message
            });
        }

        sw.Stop();
        report.TotalTimeMs = sw.Elapsed.TotalMilliseconds;
        return report;
    }

    private static void PrintTextOutput(List<TestReport> reports, bool verbose, double totalMs)
    {
        int totalPassed = reports.Sum(r => r.Passed);
        int totalFailed = reports.Sum(r => r.Failed);
        int totalErrors = reports.Sum(r => r.Errors);
        int totalCount = reports.Sum(r => r.Total);
        int filesPassed = reports.Count(r => r.AllPassed);
        int filesFailed = reports.Count - filesPassed;

        // Find the longest filename for alignment
        int maxNameLen = reports.Max(r => (r.SourceFile ?? "").Length);

        foreach (var report in reports)
        {
            string name = report.SourceFile ?? "Unknown";
            string status = report.AllPassed ? "\u001b[32mPASS\u001b[0m" : "\u001b[31mFAIL\u001b[0m";
            int dots = Math.Max(2, maxNameLen - name.Length + 20);
            string dotStr = new string('.', dots);

            var parts = new List<string>();
            parts.Add($"{report.Total} test{(report.Total == 1 ? "" : "s")}");
            if (report.Passed > 0) parts.Add($"\u001b[32m{report.Passed} pass\u001b[0m");
            if (report.Failed > 0) parts.Add($"\u001b[31m{report.Failed} fail\u001b[0m");
            if (report.Errors > 0) parts.Add($"\u001b[33m{report.Errors} err\u001b[0m");
            parts.Add($"{report.TotalTimeMs:F0}ms");

            Console.WriteLine($"  {name} {dotStr} {status} ({string.Join(" | ", parts)})");

            // Show failures/errors always; show passes only if verbose
            foreach (var result in report.Results)
            {
                if (result.Status == TestStatus.Fail)
                {
                    string detail = result.Expected is not null
                        ? $"Expected: {result.Expected}  Actual: {result.Actual}"
                        : result.Message ?? "";
                    Console.WriteLine($"    \u001b[31m\u2717 {result.TestName}    {detail}\u001b[0m");
                }
                else if (result.Status == TestStatus.Error)
                {
                    Console.WriteLine($"    \u001b[31m\u2717 {result.TestName}    {result.Message}\u001b[0m");
                }
                else if (verbose)
                {
                    Console.WriteLine($"    \u001b[32m\u2713 {result.TestName}\u001b[0m");
                }
            }
        }

        // Build plain-text versions of summary lines to measure width
        string rPassPlain = totalPassed > 0 ? $"{totalPassed} pass" : "0 pass";
        string rFailPlain = totalFailed > 0 ? $"{totalFailed} fail" : "0 fail";
        string rErrPlain = totalErrors > 0 ? $"{totalErrors} err" : "0 err";
        string fPassPlain = filesPassed > 0 ? $"{filesPassed} pass" : "0 pass";
        string fFailPlain = filesFailed > 0 ? $"{filesFailed} fail" : "0 fail";

        string resultsLine = $"Results: {totalCount} tests | {rPassPlain} | {rFailPlain} | {rErrPlain}";
        string timeLine = $"Time:    {totalMs:F0}ms";
        string filesLine = $"Files:   {reports.Count} total | {fPassPlain} | {fFailPlain}";

        bool allPassed = totalFailed == 0 && totalErrors == 0;
        string verdictLine = allPassed ? "[ ALL TESTS PASS ]" : "[ DID NOT PASS ]";

        int separatorLen = new[] { resultsLine.Length, timeLine.Length, filesLine.Length, verdictLine.Length }.Max();
        string separator = new string('\u2500', separatorLen);

        // Colored versions
        string rPass = totalPassed > 0 ? $"\u001b[32m{totalPassed} pass\u001b[0m" : $"\u001b[90m0 pass\u001b[0m";
        string rFail = totalFailed > 0 ? $"\u001b[31m{totalFailed} fail\u001b[0m" : $"\u001b[90m0 fail\u001b[0m";
        string rErr = totalErrors > 0 ? $"\u001b[33m{totalErrors} err\u001b[0m" : $"\u001b[90m0 err\u001b[0m";
        string fPass = filesPassed > 0 ? $"\u001b[32m{filesPassed} pass\u001b[0m" : $"\u001b[90m0 pass\u001b[0m";
        string fFail = filesFailed > 0 ? $"\u001b[31m{filesFailed} fail\u001b[0m" : $"\u001b[90m0 fail\u001b[0m";

        Console.WriteLine();
        Console.WriteLine(separator);
        Console.WriteLine($"Results: {totalCount} tests | {rPass} | {rFail} | {rErr}");
        Console.WriteLine($"Time:    {totalMs:F0}ms");
        Console.WriteLine($"Files:   {reports.Count} total | {fPass} | {fFail}");
        Console.WriteLine(separator);
        if (allPassed)
            Console.WriteLine($"\u001b[32m{verdictLine}\u001b[0m");
        else
            Console.WriteLine($"\u001b[31m{verdictLine}\u001b[0m");
    }

    private static string SerializeTap(List<TestReport> reports)
    {
        var sb = new StringBuilder();
        foreach (var report in reports)
        {
            if (sb.Length > 0) sb.AppendLine().AppendLine();
            sb.Append(TapSerializer.Serialize(report));
        }
        return sb.ToString();
    }

    private static List<string> EnumerateGhFiles(string path, bool recurse)
    {
        if (File.Exists(path) && path.EndsWith(".gh", StringComparison.OrdinalIgnoreCase))
            return new List<string> { Path.GetFullPath(path) };

        if (Directory.Exists(path))
        {
            var option = recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            return Directory.GetFiles(path, "*.gh", option)
                .OrderBy(f => f)
                .ToList();
        }

        throw new FileNotFoundException($"Path not found: {path}");
    }

    private static string GetVersionString()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        string ver = version is not null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0";
        return $"Assertive Possum v{ver}";
    }
}
