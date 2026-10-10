// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System.Runtime.InteropServices;

namespace Vonvert.Engine.DspEngine;

/*
 * Low-level VST 2.4 interop definitions.
 *
 * Implements the minimum host-side contract required to load a VST 2.x
 * effect plugin DLL, wire up host-to-plugin communication, and drive
 * real-time audio processing through the standard effProcessReplacing
 * entry point.
 *
 * Reference: VST 2.4 SDK C, Steinberg Media Technologies GmbH.
 * All constants and structures are self-contained; no external aifx.h is needed.
 */
internal static class Vst2
{
    // --- Magic constant -------------------------------------------------------
    public const int kEffectMagic = 0x56737450; // 'VstP'

    // --- Dispatcher opcodes ---------------------------------------------------
    public const int effOpen              =  0;
    public const int effClose             =  1;
    public const int effSetProgram        =  2;
    public const int effGetProgram        =  3;
    public const int effSetProgramName    =  4;
    public const int effGetParamLabel     =  6;
    public const int effGetParamDisplay   =  7;
    public const int effGetParamName      =  8;
    public const int effGetParamCount     =  9;   // deprecated; use numParams field
    public const int effSetSampleRate     = 10;
    public const int effSetBlockSize      = 11;
    public const int effMainsChanged      = 12;
    public const int effEditGetRect       = 13;
    public const int effEditOpen          = 14;
    public const int effEditClose         = 15;
    public const int effEditTop           = 16;
    public const int effIdentify          = 22;
    public const int effGetProductString  = 23;
    public const int effVendorSpecific    = 24;
    public const int effCanDo             = 25;
    public const int effGetTailSize       = 26;
    public const int effGetPluginName     = 34;
    public const int effGetVendorString   = 33;
    public const int effGetVendorVersion  = 35;
    public const int effProcessEvents     = 25;

    // --- AudioMaster opcodes (host callback) ----------------------------------
    public const int audioMasterAutomate       =  0;
    public const int audioMasterVersion        =  2;
    public const int audioMasterCurrentId      = 13;
    public const int audioMasterWantMidi       =  6;
    public const int audioMasterGetTime        =  7;
    public const int audioMasterProcessEvents  = 13;
    public const int audioMasterGetDirectory   = 12;
    public const int audioMasterIdle           = 42;

    // --- VSTEvents / kVstMidiType ------------------------------------------------
    public const int kVstMidiType = 1;

    // --- effCanDo strings ------------------------------------------------------
    public const string CanDoReceiveEvents  = "receiveVstEvents";
    public const string CanDoMidiEvents     = "receiveVstMidiEvent";
    public const string CanDoDoubleReplacing = "doubleReplacing";
}

// =========================================================================
// AEffect — the central VST 2.x effect structure (matches SDK layout)
// =========================================================================

[StructLayout(LayoutKind.Sequential)]
internal struct AEffect
{
    public int       magic;            // must be kEffectMagic
    public IntPtr    dispatcher;       // host-to-plugin dispatch
    public IntPtr    process;          // deprecated float process (effProcessFloat)
    public IntPtr    setParameter;     // host-to-plugin: set parameter
    public IntPtr    getParameter;     // host-to-plugin: get parameter
    public int       numPrograms;
    public int       numParams;
    public int       numInputs;        // channels per audio bus
    public int       numOutputs;       // channels per audio bus
    public int       flags;
    public IntPtr    resvd1;
    public IntPtr    resvd2;
    public int       initialDelay;     // latency in samples
    public int       realQualities;
    public int       offQualities;
    public float     ioRatio;
    public IntPtr    objectPtr;        // plugin's own C++ this pointer
    public IntPtr    user;             // host-controlled user data
    public int       uniqueID;         // plugin FourCC
    public int       version;
    // --- function pointers for audio processing ---
    public IntPtr    processReplacing; // float, in-place
    public IntPtr    processDoubleReplacing; // double, in-place
}

// =========================================================================
// Unmanaged function-pointer delegate types
// =========================================================================

/// <summary>Plugin entry point — exported as "main" or "VSTPluginMain".</summary>
internal delegate IntPtr VstEntryDelegate(IntPtr hostCallback);

/// <summary>Host-to-plugin dispatcher callback.</summary>
internal delegate IntPtr VstDispatchDelegate(IntPtr effect, int opcode, int index,
    IntPtr value, IntPtr ptr, float opt);

/// <summary>Host-to-plugin: set a float parameter.</summary>
internal delegate void VstSetParameterDelegate(IntPtr effect, int index, float value);

/// <summary>Host-to-plugin: read a float parameter.</summary>
internal delegate float VstGetParameterDelegate(IntPtr effect, int index);

/// <summary>Float audio processing (replacing) — void processReplacing(float** inputs, float** outputs, int sampleFrames).</summary>
internal delegate void VstProcessReplacingDelegate(IntPtr effect, IntPtr inputs, IntPtr outputs, int sampleFrames);

/// <summary>Double audio processing (replacing).</summary>
internal delegate void VstProcessDoubleReplacingDelegate(IntPtr effect, IntPtr inputs, IntPtr outputs, int sampleFrames);

/// <summary>Plugin-to-host callback.</summary>
internal delegate IntPtr VstHostCallbackDelegate(IntPtr effect, int opcode, int index,
    IntPtr value, IntPtr ptr, float opt);

// =========================================================================
// P/Invoke helpers
// =========================================================================

internal static class Vst2Native
{
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr LoadLibrary(string lpFileName);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, uint dwFlags);

    /// <summary>Maps DLL as data file without executing DllMain.</summary>
    public const uint LOAD_LIBRARY_AS_DATAFILE = 0x00000002;

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool FreeLibrary(IntPtr hModule);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi, BestFitMapping = false)]
    public static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr GetModuleHandle(string lpModuleName);
}
