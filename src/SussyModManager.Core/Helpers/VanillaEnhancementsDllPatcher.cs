using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace SussyModManager.Core.Helpers
{
    /// <summary>
    /// Vanilla Enhancements 1.2.x calls GitHub on every game launch. When rate-limited, its catch block
    /// returns a 2-element bool[] but CheckDevRelease reads index 2 — IndexOutOfRangeException.
    /// Patch the catch path to allocate 3 elements (zero-filled) so the mod loads offline.
    /// </summary>
    public static class VanillaEnhancementsDllPatcher
    {
        public const string ModId = "VanillaEnhancements";
        public const string DllFileName = "VanillaEnhancements.dll";

        public static bool IsVanillaEnhancementsDll(string filePath) =>
            string.Equals(Path.GetFileName(filePath), DllFileName, StringComparison.OrdinalIgnoreCase);

        public static bool NeedsPatch(string dllPath)
        {
            if (!File.Exists(dllPath))
                return false;

            try
            {
                var bytes = File.ReadAllBytes(dllPath);
                using var input = new MemoryStream(bytes);
                using var asm = AssemblyDefinition.ReadAssembly(input);
                var sizeIns = FindCatchArraySizeInstruction(asm);
                return sizeIns != null && sizeIns.OpCode == OpCodes.Ldc_I4_2;
            }
            catch
            {
                return false;
            }
        }

        public static bool TryPatch(string dllPath)
        {
            return TryPatch(dllPath, out _);
        }

        public static bool TryPatch(string dllPath, out string error)
        {
            error = null;
            if (!File.Exists(dllPath))
            {
                error = "File not found.";
                return false;
            }

            try
            {
                var bytes = File.ReadAllBytes(dllPath);
                using var input = new MemoryStream(bytes);
                using var asm = AssemblyDefinition.ReadAssembly(input);
                var sizeIns = FindCatchArraySizeInstruction(asm);
                if (sizeIns == null)
                {
                    error = "Patch site not found (already patched or unsupported build).";
                    return false;
                }

                if (sizeIns.OpCode == OpCodes.Ldc_I4_3)
                    return true;

                sizeIns.OpCode = OpCodes.Ldc_I4_3;
                sizeIns.Operand = null;

                using var output = new MemoryStream();
                asm.Write(output);
                File.WriteAllBytes(dllPath, output.ToArray());
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static void PatchFileIfNeeded(string dllPath)
        {
            if (!IsVanillaEnhancementsDll(dllPath) || !NeedsPatch(dllPath))
                return;
            TryPatch(dllPath);
        }

        public static void PatchModFolder(string modStoragePath)
        {
            if (!Directory.Exists(modStoragePath))
                return;

            foreach (var dll in Directory.GetFiles(modStoragePath, DllFileName, SearchOption.AllDirectories))
                PatchFileIfNeeded(dll);
        }

        private static Instruction FindCatchArraySizeInstruction(AssemblyDefinition asm)
        {
            var plugin = asm.MainModule.Types.FirstOrDefault(t => t.Name == "VanillaEnhancementsPlugin");
            if (plugin == null)
                return null;

            var stateMachine = plugin.NestedTypes.FirstOrDefault(t => t.Name.Contains("HttpVersionExists"));
            if (stateMachine == null)
                return null;

            var moveNext = stateMachine.Methods.FirstOrDefault(m => m.Name == "MoveNext" && m.HasBody);
            if (moveNext == null)
                return null;

            var instructions = moveNext.Body.Instructions;
            for (var i = 0; i < instructions.Count; i++)
            {
                if (instructions[i].OpCode != OpCodes.Ldstr)
                    continue;

                var text = instructions[i].Operand as string;
                if (text == null || !text.Contains("Failed to check for dev release", StringComparison.Ordinal))
                    continue;

                for (var j = i + 1; j < instructions.Count && j < i + 12; j++)
                {
                    if (instructions[j].OpCode == OpCodes.Ldc_I4_2 || instructions[j].OpCode == OpCodes.Ldc_I4_3)
                        return instructions[j];
                }
            }

            return null;
        }
    }
}
