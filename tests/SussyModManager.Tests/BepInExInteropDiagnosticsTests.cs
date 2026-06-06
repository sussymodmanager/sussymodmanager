using SussyModManager.Core.Helpers;

namespace SussyModManager.Tests;

public class BepInExInteropDiagnosticsTests
{
    [Fact]
    public void GetPreLaunchReactorIssue_MissingInterop_ReturnsNull()
    {
        var dir = Path.Combine(Path.GetTempPath(), "smm-interop-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            Assert.Null(BepInExInteropDiagnostics.GetPreLaunchReactorIssue(dir, true));
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    [Fact]
    public void GetPreLaunchReactorIssue_InteropWithoutExpectedStateMachine_ReturnsMessage()
    {
        var dir = Path.Combine(Path.GetTempPath(), "smm-interop-" + Guid.NewGuid().ToString("N"));
        var interop = Path.Combine(dir, "BepInEx", "interop");
        Directory.CreateDirectory(interop);
        var asm = Path.Combine(interop, "Assembly-CSharp.dll");
        File.WriteAllBytes(asm, "HandleGameDataInner_d__167"u8.ToArray());
        try
        {
            var issue = BepInExInteropDiagnostics.GetPreLaunchReactorIssue(dir, true);
            Assert.NotNull(issue);
            Assert.Contains("Reactor", issue, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }
}
