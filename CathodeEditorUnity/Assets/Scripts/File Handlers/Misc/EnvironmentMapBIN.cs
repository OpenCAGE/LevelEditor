using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace CATHODE.Misc
{
    class EnvironmentMapBIN
    {
        public static alien_environment_map_bin Load(string FullFilePath)
        {
            alien_environment_map_bin Result = new alien_environment_map_bin();
            BinaryReader Stream = new BinaryReader(File.OpenRead(FullFilePath));

            Result.Header = Utilities.Consume<alien_environment_map_bin_header>(ref Stream);
            Result.Entries = Utilities.ConsumeArray<alien_environment_map_bin_entry>(ref Stream, Result.Header.EntryCount);

            return Result;
        }
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct alien_environment_map_bin_entry
{
    public int UnknownValue; // TODO: Very small or '-1'. Enum?
    public uint UnknownIndex;
};

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct alien_environment_map_bin_header
{
    public fourcc FourCC;
    public uint Unknown0_;
    public int EntryCount;
    public uint Unknown1_;
};

public struct alien_environment_map_bin
{
    public alien_environment_map_bin_header Header;
    public List<alien_environment_map_bin_entry> Entries;
};