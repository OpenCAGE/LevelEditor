using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace TestProject.File_Handlers.Models
{
    class ModelsMVR
    {
        public static alien_mvr Load(string FullFilePath)
        {
            alien_mvr Result = new alien_mvr();
            BinaryReader Stream = new BinaryReader(File.OpenRead(FullFilePath));

            Result.Header = Utilities.Consume<alien_mvr_header>(ref Stream);
            Result.Entries = Utilities.ConsumeArray<alien_mvr_entry>(ref Stream, (int)Result.Header.EntryCount);

            return Result;
        }
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
struct alien_mvr_header
{
    public uint Unknown0_;
    public uint EntryCount;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
    public uint[] Unknown1_; //6
};

[StructLayout(LayoutKind.Sequential, Pack = 1)]
struct alien_mvr_entry
{
    public UnityEngine.Matrix4x4 Transform;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
    public float[] Unknown0__; // NOTE: The 0th element is -Pi on entry 61 of 'hab_airport'.
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
    public float[] Unknown1__;
    public float UnknownValue2_;
    public float UnknownValue3_;
    public float UnknownValue4_;
    public int Unknown2_;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] Unknown2f_;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
    public uint[] Unknown2__;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
    public UnityEngine.Vector3[] UnknownMinMax_; // NOTE: Sometimes I see 'nan's here too.
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
    public int[] Unknown3__; // NOTE: The 9th and 11th elements seem to be incrementing indices.
    public UnityEngine.Vector3 UnknownV3_;
    public uint Unknown3_;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float UnknownValues0_;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public int Unknown4__;
};

struct alien_mvr
{
    public alien_mvr_header Header;
    public List<alien_mvr_entry> Entries;
};