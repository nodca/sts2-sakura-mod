using System.Runtime.CompilerServices;
using System.Diagnostics.CodeAnalysis;
using MegaCrit.Sts2.Core.TestSupport;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
[assembly: SuppressMessage("xUnit", "xUnit1031", Justification = "Game test helpers are synchronous and test parallelism is disabled.")]

internal static class TestAssemblySetup
{
    [ModuleInitializer]
    internal static void Initialize() => TestMode.TurnOnInternal();
}
