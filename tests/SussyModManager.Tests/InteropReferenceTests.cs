using SussyModManager.Core.Helpers;

namespace SussyModManager.Tests;

public class InteropReferenceTests
{
    [Fact]
    public void TrySeedInterop_CopiesWorkingInterop()
    {
        var root = Path.Combine(Path.GetTempPath(), "smm-interop-seed-" + Guid.NewGuid().ToString("N"));
        var reference = Path.Combine(root, "reference");
        var target = Path.Combine(root, "target");
        var interop = Path.Combine(reference, "BepInEx", "interop");
        Directory.CreateDirectory(interop);
        File.WriteAllBytes(
            Path.Combine(interop, "Assembly-CSharp.dll"),
            "HandleGameDataInner_d__165"u8.ToArray());

        try
        {
            Assert.True(InteropReference.TrySeedInterop(target, reference, Path.Combine(root, "cache")));
            Assert.True(InteropReference.HasWorkingInterop(target));
            Assert.True(File.Exists(Path.Combine(target, "BepInEx", "interop", "Assembly-CSharp.dll")));
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }
}
