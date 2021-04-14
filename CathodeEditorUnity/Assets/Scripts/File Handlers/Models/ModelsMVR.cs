using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace CATHODE.Models
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
public struct alien_mvr_header
{
    public uint Unknown0_;
    public uint EntryCount;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
    public uint[] Unknown1_; //6
};

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct alien_mvr_entry
{
    public UnityEngine.Matrix4x4 Transform;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
    public float[] Unknowns0_; // NOTE: The 0th element is -Pi on entry 61 of 'hab_airport'.
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
    public float[] Unknowns1_;
    public float UnknownValue2_;
    public float UnknownValue3_;
    public float UnknownValue4_;
    public int Unknown2_;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] Unknown2f_;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
    public uint[] Unknowns2_;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
    public UnityEngine.Vector3[] UnknownMinMax_; // NOTE: Sometimes I see 'nan's here too.
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 9)]
    public int[] Unknowns3_; // NOTE: The 9th and 11th elements seem to be incrementing indices.
    public uint REDSIndex; // Index 45
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
    public int[] Unknowns5_;
    public uint NodeID; // Index 52
    public uint UnknownValue0;
    public uint UnknownIndex;
    public uint UnknownValue1;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public float[] Unknowns4_;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public int[] Unknowns6_;
};

public struct alien_mvr
{
    public alien_mvr_header Header;
    public List<alien_mvr_entry> Entries;
};