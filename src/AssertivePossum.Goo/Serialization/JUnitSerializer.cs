using System.Xml.Linq;

namespace AssertivePossum.Goo.Serialization;

/// <summary>
/// Serializes one or more <see cref="TestReport"/> instances into JUnit XML format.
/// </summary>
public static class JUnitSerializer
{
    public static string Serialize(IEnumerable<TestReport> reports)
    {
        var testsuites = new XElement("testsuites");

        foreach (var report in reports)
        {
            var suite = new XElement("testsuite",
                new XAttribute("name", report.SourceFile ?? "Unknown"),
                new XAttribute("tests", report.Total),
                new XAttribute("failures", report.Failed),
                new XAttribute("errors", report.Errors),
                new XAttribute("time", (report.TotalTimeMs / 1000.0).ToString("F3")),
                new XAttribute("timestamp", report.Timestamp.ToString("o"))
            );

            foreach (var result in report.Results)
            {
                var testcase = new XElement("testcase",
                    new XAttribute("name", result.TestName),
                    new XAttribute("time", (result.ElapsedMs / 1000.0).ToString("F3"))
                );

                if (!string.IsNullOrEmpty(result.ComponentId))
                    testcase.Add(new XAttribute("classname", result.ComponentId));

                switch (result.Status)
                {
                    case TestStatus.Fail:
                        var failure = new XElement("failure",
                            new XAttribute("message", result.Message ?? string.Empty));
                        if (result.Expected is not null || result.Actual is not null)
                            failure.Value = $"Expected: {result.Expected}\nActual: {result.Actual}";
                        testcase.Add(failure);
                        break;

                    case TestStatus.Error:
                        var error = new XElement("error",
                            new XAttribute("message", result.Message ?? string.Empty));
                        testcase.Add(error);
                        break;
                }

                suite.Add(testcase);
            }

            testsuites.Add(suite);
        }

        return new XDocument(new XDeclaration("1.0", "utf-8", null), testsuites).ToString();
    }

    public static string Serialize(TestReport report) => Serialize(new[] { report });
}
