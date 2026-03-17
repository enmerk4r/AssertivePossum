using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using AssertivePossum.Goo;

namespace AssertivePossum.Components.Runner;

public class GHRunnerComponent : GH_TaskCapableComponent<GHRunnerComponent.SolveResults>
{
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
        pManager.AddBooleanParameter("Run", "R", "Set to true to execute tests.", GH_ParamAccess.item, false);
    }

    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
        pManager.AddGenericParameter("Reports", "Rp", "One TestReport per .gh file.", GH_ParamAccess.list);
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
        bool run = false;

        if (!DA.GetData(0, ref source)) return;
        DA.GetData(1, ref server);
        DA.GetData(2, ref run);

        if (!run)
        {
            Message = "Idle";
            return;
        }

        if (InPreSolve)
        {
            // Enumerate files
            var files = EnumerateGhFiles(source!);
            if (files.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No .gh files found at the specified path.");
                return;
            }

            Message = $"Running {files.Count} files...";

            var task = Task.Run(async () =>
            {
                var results = new SolveResults();
                using var client = new ComputeClient(server);

                foreach (string file in files)
                {
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
                }

                results.AllPassed = results.Reports.All(r => r.AllPassed);
                results.Summary = BuildSummary(results.Reports);
                return results;
            });

            TaskList.Add(task);
            return;
        }

        if (!GetSolveResults(DA, out var solveResults))
        {
            // Error occurred during solve
            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Failed to retrieve solve results.");
            return;
        }

        // Output results
        var reportGoos = solveResults.Reports.Select(r => new TestReportGoo(r)).ToList();
        DA.SetDataList(0, reportGoos);
        DA.SetData(1, solveResults.Summary);
        DA.SetData(2, solveResults.AllPassed);

        Message = solveResults.AllPassed ? "ALL PASSED" : "FAILURES";
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
