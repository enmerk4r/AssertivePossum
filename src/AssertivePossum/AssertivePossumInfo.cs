using System;
using System.Drawing;
using System.Reflection;
using Grasshopper;
using Grasshopper.Kernel;

namespace AssertivePossum.Components;

public class AssertivePossumInfo : GH_AssemblyInfo
{
    public override string Name => "Assertive Possum";

    public override string Description => "Integration testing framework for Grasshopper";

    public override Guid Id => new("A1B2C3D4-E5F6-4A8B-9C0D-1E2F3A4B5C6D");

    public override string Version => Assembly.GetExecutingAssembly()
        .GetName().Version?.ToString() ?? "1.0.0";

    public override Bitmap? Icon =>
        new Bitmap(typeof(AssertivePossumInfo).Assembly.GetManifestResourceStream("Icons.logo.png")!);

    public override string AuthorName => "Assertive Possum Contributors";

    public override string AuthorContact => "https://github.com/AssertivePossum";
}

public class AssertivePossumPriority : GH_AssemblyPriority
{
    public override GH_LoadingInstruction PriorityLoad()
    {
        Instances.ComponentServer.AddCategoryIcon("Assertive Possum",
            new Bitmap(typeof(AssertivePossumPriority).Assembly.GetManifestResourceStream("Icons.logo.png")!));
        return GH_LoadingInstruction.Proceed;
    }
}
