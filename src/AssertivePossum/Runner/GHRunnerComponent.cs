using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Rhino;
using AssertivePossum.Goo;

namespace AssertivePossum.Components.Runner;

public class GHRunnerComponent : GH_Component
{
    private readonly object _runLock = new();
    private Task? _activeRunTask;
    private SolveResults? _latestResults;
    private Exception? _latestError;
    private string? _activeRunKey;
    private string? _completedRunKey;
    private string _statusMessage = "Idle";
    private int _runVersion;

    public GHRunnerComponent()
        : base("Test Runner", "Run",
            "Solves Grasshopper definitions via Rhino.Compute and collects test results.",
            "Assertive Possum", "2. Run Tests")
    {
    }

    public override Guid ComponentGuid => new("A7B8C9D0-E1F2-4A3B-5C6D-7E8F9A0B1C2D");

    protected override Bitmap? Icon =>
        new Bitmap(GetType().Assembly.GetManifestResourceStream("Icons.runner.png")!);

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
        pManager.AddTextParameter("Source", "S", "Path to a .gh file or folder of .gh files.", GH_ParamAccess.item);
        pManager.AddTextParameter("Server", "Sv", "Rhino.Compute server URL.", GH_ParamAccess.item, "http://localhost:6500");
        pManager.AddTextParameter("API Key", "K", "API key for Rhino.Compute (RhinoComputeKey header).", GH_ParamAccess.item);
        pManager[2].Optional = true;
        pManager.AddBooleanParameter("Run", "R", "Set to true to execute tests.", GH_ParamAccess.item, false);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddGenericParameter("Reports", "R", "One TestReport per .gh file.", GH_ParamAccess.list);
        pManager.AddTextParameter("Summary", "Su", "Human-readable summary.", GH_ParamAccess.item);
        pManager.AddBooleanParameter("AllPassed", "AP", "True only if every test in every file passed.", GH_ParamAccess.item);
    }

    public class SolveResults
    {
        public List<TestReport> Reports { get; set; } = new();
        public string Summary { get; set; } = string.Empty;
        public bool AllPassed { get; set; }
    }

    protected override void SolveInstance(IGH_DataAccess DA)
    {
        string? source = null;
        string server = "http://localhost:6500";
        string? apiKey = null;
        bool run = false;

        if (!DA.GetData(0, ref source)) return;
        DA.GetData(1, ref server);
        DA.GetData(2, ref apiKey);
        DA.GetData(3, ref run);

        if (!run)
        {
            ResetRunnerState();
            Message = "Idle";
            return;
        }

        var files = EnumerateGhFiles(source!);
        if (files.Count == 0)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No .gh files found at the specified path.");
            return;
        }

        string runKey = $"{Path.GetFullPath(source!)}|{server}|{apiKey}";
        bool shouldStartRun = false;
        int runVersion = 0;
        SolveResults? solveResults;
        Exception? solveError;
        string statusMessage;
        bool hasCompletedResults;
        bool hasCompletedError;

        lock (_runLock)
        {
            bool isRunningThisRequest = _activeRunTask is not null && _activeRunKey == runKey;
            hasCompletedResults = _latestResults is not null && _completedRunKey == runKey;
            hasCompletedError = _latestError is not null && _completedRunKey == runKey;

            if (!isRunningThisRequest && !hasCompletedResults && !hasCompletedError)
            {
                shouldStartRun = true;
                _runVersion++;
                runVersion = _runVersion;
                _activeRunKey = runKey;
                _completedRunKey = null;
                _latestResults = null;
                _latestError = null;
                _statusMessage = $"0/{files.Count} [RUNNING]";
            }

            solveResults = _latestResults;
            solveError = _latestError;
            statusMessage = _statusMessage;
        }

        if (shouldStartRun)
        {
            StartRun(runVersion, runKey, server, apiKey, files);
            statusMessage = $"0/{files.Count} [RUNNING]";
        }

        Message = statusMessage;
        Attributes?.ExpireLayout();

        if (solveError is not null && hasCompletedError)
        {
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, solveError.Message);
            return;
        }

        if (solveResults is null || !hasCompletedResults)
        {
            DA.SetDataList(0, Enumerable.Empty<TestReportGoo>());
            DA.SetData(1, "Running...");
            DA.SetData(2, false);
            return;
        }

        var reportGoos = solveResults.Reports.Select(r => new TestReportGoo(r)).ToList();
        DA.SetDataList(0, reportGoos);
        DA.SetData(1, solveResults.Summary);
        DA.SetData(2, solveResults.AllPassed);
        Message = solveResults.AllPassed
            ? "ALL PASSED"
            : $"{solveResults.Reports.Sum(r => r.Failed + r.Errors)} FAILURES";
        Attributes?.ExpireLayout();
    }

    private void StartRun(int runVersion, string runKey, string server, string? apiKey, List<string> files)
    {
        var task = Task.Run(() => RunTestsAsync(runVersion, runKey, server, apiKey, files));
        lock (_runLock)
        {
            if (runVersion == _runVersion && _activeRunKey == runKey)
            {
                _activeRunTask = task;
            }
        }
    }

    private async Task RunTestsAsync(int runVersion, string runKey, string server, string? apiKey, List<string> files)
    {
        var results = new SolveResults();
        using var client = new ComputeClient(server, apiKey: apiKey);
        int total = files.Count;

        try
        {
            for (int i = 0; i < files.Count; i++)
            {
                string file = files[i];
                var response = await client.PostDefinitionAsync(file);
                var testResults = ComputeClient.DeserializeTestResults(response);

                var report = new TestReport
                {
                    SourceFile = Path.GetFileName(file),
                    Timestamp = DateTime.UtcNow,
                    TotalTimeMs = 0
                };

                if (testResults.Count > 0)
                {
                    TestReport.FailDuplicateNames(testResults);
                    report.Results.AddRange(testResults);
                    report.TotalTimeMs = testResults.Sum(r => r.ElapsedMs);
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

                results.Reports.Add(report);
                UpdateProgress(runVersion, runKey, $"{i + 1}/{total} [{(report.AllPassed ? "PASS" : "FAIL")}]");
            }

            results.AllPassed = results.Reports.All(r => r.AllPassed);
            results.Summary = BuildSummary(results.Reports);
            CompleteRun(runVersion, runKey, results);
        }
        catch (Exception ex)
        {
            FailRun(runVersion, runKey, ex);
        }
    }

    private void UpdateProgress(int runVersion, string runKey, string statusMessage)
    {
        bool updated = false;

        lock (_runLock)
        {
            if (runVersion == _runVersion && _activeRunKey == runKey)
            {
                _statusMessage = statusMessage;
                updated = true;
            }
        }

        if (updated)
        {
            ScheduleRefresh();
        }
    }

    private void CompleteRun(int runVersion, string runKey, SolveResults results)
    {
        bool completed = false;

        lock (_runLock)
        {
            if (runVersion == _runVersion && _activeRunKey == runKey)
            {
                _activeRunTask = null;
                _latestResults = results;
                _latestError = null;
                _completedRunKey = runKey;
                _statusMessage = results.AllPassed
                    ? "ALL PASSED"
                    : $"{results.Reports.Sum(r => r.Failed + r.Errors)} FAILURES";
                completed = true;
            }
        }

        if (completed)
        {
            ScheduleRefresh();
        }
    }

    private void FailRun(int runVersion, string runKey, Exception exception)
    {
        bool failed = false;

        lock (_runLock)
        {
            if (runVersion == _runVersion && _activeRunKey == runKey)
            {
                _activeRunTask = null;
                _latestResults = null;
                _latestError = exception;
                _completedRunKey = runKey;
                _statusMessage = "ERROR";
                failed = true;
            }
        }

        if (failed)
        {
            ScheduleRefresh();
        }
    }

    private void ResetRunnerState()
    {
        lock (_runLock)
        {
            _runVersion++;
            _activeRunTask = null;
            _latestResults = null;
            _latestError = null;
            _activeRunKey = null;
            _completedRunKey = null;
            _statusMessage = "Idle";
        }
    }

    private void ScheduleRefresh()
    {
        RhinoApp.InvokeOnUiThread(() =>
        {
            Attributes?.ExpireLayout();
            OnDisplayExpired(true);

            var document = OnPingDocument();
            document?.ScheduleSolution(1, _ => ExpireSolution(false));
        });
    }

    private static List<string> EnumerateGhFiles(string source)
    {
        if (File.Exists(source) && source.EndsWith(".gh", StringComparison.OrdinalIgnoreCase))
            return new List<string> { Path.GetFullPath(source) };

        if (Directory.Exists(source))
        {
            return Directory.GetFiles(source, "*.gh", SearchOption.AllDirectories)
                .OrderBy(f => f)
                .ToList();
        }

        return new List<string>();
    }

    private static string BuildSummary(List<TestReport> reports)
    {
        int totalPassed = reports.Sum(r => r.Passed);
        int totalFailed = reports.Sum(r => r.Failed);
        int totalErrors = reports.Sum(r => r.Errors);
        int totalCount = reports.Sum(r => r.Total);
        int filesPassed = reports.Count(r => r.AllPassed);
        int filesFailed = reports.Count - filesPassed;

        var sb = new StringBuilder();
        sb.AppendLine($"Results: {totalPassed} passed, {totalFailed} failed, {totalErrors} errors ({totalCount} total)");
        sb.AppendLine($"Files:   {filesPassed} passed, {filesFailed} failed ({reports.Count} total)");
        return sb.ToString().TrimEnd();
    }
}
