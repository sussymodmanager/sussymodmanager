using System;
using System.IO;

namespace SussyModManager.Core.Helpers
{
    /// <summary>Reads the PE machine type from a Windows executable.</summary>
    public static class PeArchitecture
    {
        public const ushort MachineAmd64 = 0x8664;
        public const ushort MachineI386 = 0x014C;

        public static bool TryGetMachineType(string filePath, out ushort machine)
        {
            machine = 0;
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return false;

            try
            {
                using var stream = File.OpenRead(filePath);
                using var reader = new BinaryReader(stream);
                if (reader.ReadUInt16() != 0x5A4D)
                    return false;

                stream.Seek(0x3C, SeekOrigin.Begin);
                var peOffset = reader.ReadInt32();
                if (peOffset <= 0 || peOffset >= stream.Length - 6)
                    return false;

                stream.Seek(peOffset, SeekOrigin.Begin);
                if (reader.ReadUInt32() != 0x00004550)
                    return false;

                machine = reader.ReadUInt16();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool Is64BitExecutable(string filePath) =>
            TryGetMachineType(filePath, out var machine) && machine == MachineAmd64;
    }
}
