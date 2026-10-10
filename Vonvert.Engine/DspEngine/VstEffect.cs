// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System.Runtime.InteropServices;
using Vonvert.Engine.AudioEngine;

namespace Vonvert.Engine.DspEngine;

/*
 * IAudioEffect adapter that wraps a native VST 2.x effect plugin.
 *
 * Lifecycle:
 *   1. Construct with the absolute path to a VST2 .dll.
 *   2. The constructor loads the DLL, resolves the entry point, creates the
 *      AEffect instance, and completes the VST initialisation handshake
 *      (effOpen, effSetSampleRate, effSetBlockSize, effMainsChanged).
 *   3. Process() is called on the DSP thread — it converts the mono buffer
 *      to stereo, calls processReplacing, then extracts the left channel
 *      back into the original mono span.
 *   4. Dispose() tears down the plugin (effMainsChanged(0), effClose,
 *      FreeLibrary).
 *
 * Thread-safety:
 *   - Process() runs on the audio thread and must not allocate.
 *   - Parameter changes (SetParameter) may be called from the UI thread;
 *     most VST plugins handle this internally with lock-free atomics.
 */
public sealed class VstEffect : IAudioEffect, IDisposable
{
    public string Name      { get; }
    public bool   IsEnabled { get; set; }

    /// <summary>Absolute path to the VST2 plugin DLL.</summary>
    public string PluginPath { get; }

    /// <summary>Vendor name reported by the plugin.</summary>
    public string Vendor  { get; private set; } = "Unknown";

    /// <summary>Product / effect name reported by the plugin.</summary>
    public string Product { get; private set; } = "Unknown";

    /// <summary>Number of automatable parameters exposed by the plugin.</summary>
    public int ParameterCount => _isLoaded && _effectPtr != IntPtr.Zero
        ? Marshal.ReadInt32(_effectPtr + 20) : 0;

    /// <summary>Plugin latency in samples (initialDelay field).</summary>
    public int LatencySamples => _isLoaded && _effectPtr != IntPtr.Zero
        ? Marshal.ReadInt32(_effectPtr + 36) : 0;

    /// <summary>True when the native plugin is loaded and ready.</summary>
    public bool IsLoaded => _isLoaded;

    // --- Native handles -------------------------------------------------------
    private IntPtr _handle;        // HMODULE from LoadLibrary
    private IntPtr _effectPtr;     // AEffect* returned by the entry point
    private bool   _isLoaded;
    private bool   _disposed;

    // --- Cached delegates (must stay alive for plugin lifetime) ----------------
    private VstDispatchDelegate?            _dispatcher;
    private VstSetParameterDelegate?        _setParameter;
    private VstGetParameterDelegate?        _getParameter;
    private VstProcessReplacingDelegate?    _processReplacing;
    private VstHostCallbackDelegate         _hostCallback; // rooted — never GC'd

    // --- Reusable unmanaged buffers (allocated once, used on DSP thread) -------
    private IntPtr _inChannelPtr;    // float*[2]  — input channel pointer array
    private IntPtr _outChannelPtr;   // float*[2]  — output channel pointer array
    private IntPtr _stereoBuf;       // float[N*2] — interleaved stereo scratch
    private int    _stereoCapacity;  // current allocation in frames

    private const int ENGINE_RATE = AudioConstants.EngineRate;

    // =====================================================================
    // Construction / Loading
    // =====================================================================

    public VstEffect(string dllPath)
    {
        PluginPath = dllPath ?? throw new ArgumentNullException(nameof(dllPath));
        Name       = Path.GetFileNameWithoutExtension(dllPath);
        _hostCallback = HostCallback; // rooted reference to prevent GC
        LoadPlugin(dllPath);
    }

    private void LoadPlugin(string dllPath)
    {
        // 1. Load DLL
        _handle = Vst2Native.LoadLibrary(dllPath);
        if (_handle == IntPtr.Zero)
            throw new DllNotFoundException($"Failed to load VST plugin: {dllPath} (error {Marshal.GetLastWin32Error()})");

        // 2. Resolve entry point (VSTPluginMain or main)
        IntPtr entryAddr = Vst2Native.GetProcAddress(_handle, "VSTPluginMain");
        if (entryAddr == IntPtr.Zero)
            entryAddr = Vst2Native.GetProcAddress(_handle, "main");
        if (entryAddr == IntPtr.Zero)
        {
            Vst2Native.FreeLibrary(_handle);
            _handle = IntPtr.Zero;
            throw new EntryPointNotFoundException($"No VST entry point found in: {dllPath}");
        }

        var entry = Marshal.GetDelegateForFunctionPointer<VstEntryDelegate>(entryAddr);

        // 3. Create AEffect instance
        _effectPtr = entry(Marshal.GetFunctionPointerForDelegate(_hostCallback));
        if (_effectPtr == IntPtr.Zero)
        {
            Vst2Native.FreeLibrary(_handle);
            _handle = IntPtr.Zero;
            throw new InvalidOperationException($"Plugin entry point returned null: {dllPath}");
        }

        // 4. Validate magic
        int magic = Marshal.ReadInt32(_effectPtr);
        if (magic != Vst2.kEffectMagic)
        {
            Vst2Native.FreeLibrary(_handle);
            _handle = IntPtr.Zero;
            throw new InvalidOperationException($"Invalid VST magic number in: {dllPath}");
        }

        // 5. Cache function pointers as delegates
        _dispatcher       = Marshal.GetDelegateForFunctionPointer<VstDispatchDelegate>(ReadPtr(4));
        _setParameter     = Marshal.GetDelegateForFunctionPointer<VstSetParameterDelegate>(ReadPtr(12));
        _getParameter     = Marshal.GetDelegateForFunctionPointer<VstGetParameterDelegate>(ReadPtr(16));
        var procReplacing = ReadPtr(80); // processReplacing offset in AEffect
        if (procReplacing != IntPtr.Zero)
            _processReplacing = Marshal.GetDelegateForFunctionPointer<VstProcessReplacingDelegate>(procReplacing);

        if (_dispatcher == null || _processReplacing == null)
        {
            Dispatch(Vst2.effClose, 0, IntPtr.Zero, IntPtr.Zero, 0f);
            Vst2Native.FreeLibrary(_handle);
            _handle = IntPtr.Zero;
            throw new InvalidOperationException($"Plugin missing required callbacks: {dllPath}");
        }

        // 6. VST initialisation handshake
        Dispatch(Vst2.effOpen, 0, IntPtr.Zero, IntPtr.Zero, 0f);
        Dispatch(Vst2.effSetSampleRate, 0, IntPtr.Zero, IntPtr.Zero, ENGINE_RATE);
        Dispatch(Vst2.effSetBlockSize, 0, new IntPtr(4096), IntPtr.Zero, 0f);
        Dispatch(Vst2.effMainsChanged, 0, IntPtr.Zero, IntPtr.Zero, 1f); // mains on

        // 7. Read plugin metadata
        Product = DispatchString(Vst2.effGetProductString, 256) ?? Name;
        Vendor  = DispatchString(Vst2.effGetVendorString, 64)  ?? "Unknown";

        // 8. Allocate reusable processing buffers
        AllocateStereoBuffers(4096);

        _isLoaded = true;
    }

    // =====================================================================
    // IAudioEffect
    // =====================================================================

    public void Process(Span<float> buffer)
    {
        if (!_isLoaded || !IsEnabled || _processReplacing == null || buffer.Length == 0)
            return;

        int frames = buffer.Length;

        // Grow stereo buffers if needed (DSP thread — no lock required, single writer).
        if (frames > _stereoCapacity)
            AllocateStereoBuffers(frames * 2);

        unsafe
        {
            float* stereo = (float*)_stereoBuf;

            // Mono to stereo duplicate
            for (int i = 0; i < frames; i++)
            {
                stereo[i * 2]     = buffer[i];
                stereo[i * 2 + 1] = buffer[i];
            }

            // Set up channel pointer arrays (float** -> float*[])
            float** inChans  = (float**)_inChannelPtr;
            float** outChans = (float**)_outChannelPtr;
            inChans[0]  = stereo;
            inChans[1]  = stereo;  // mono duplicate: both channels point to same data
            outChans[0] = stereo;
            outChans[1] = stereo + 1;

            _processReplacing(_effectPtr, _inChannelPtr, _outChannelPtr, frames);

            // Extract left channel back to mono
            for (int i = 0; i < frames; i++)
                buffer[i] = stereo[i * 2];
        }
    }

    public void Reset()
    {
        if (!_isLoaded) return;
        // Suspend and resume to flush internal state
        Dispatch(Vst2.effMainsChanged, 0, IntPtr.Zero, IntPtr.Zero, 0f);
        Dispatch(Vst2.effMainsChanged, 0, IntPtr.Zero, IntPtr.Zero, 1f);
    }

    // =====================================================================
    // Parameter access
    // =====================================================================

    /// <summary>Set a plugin parameter by index (0-based, normalised 0..1).</summary>
    public void SetParameter(int index, float value)
    {
        if (!_isLoaded || _setParameter == null) return;
        _setParameter(_effectPtr, index, value);
    }

    /// <summary>Read a plugin parameter by index (normalised 0..1).</summary>
    public float GetParameter(int index)
    {
        if (!_isLoaded || _getParameter == null) return 0f;
        return _getParameter(_effectPtr, index);
    }

    /// <summary>Read the display string for a parameter (e.g. "3.5 kHz").</summary>
    public string GetParameterDisplay(int index)
    {
        if (!_isLoaded) return "";
        var buf = Marshal.AllocHGlobal(32);
        try
        {
            Dispatch(Vst2.effGetParamDisplay, index, IntPtr.Zero, buf, 0f);
            return Marshal.PtrToStringAnsi(buf) ?? "";
        }
        finally { Marshal.FreeHGlobal(buf); }
    }

    /// <summary>Read the name of a parameter (e.g. "Frequency").</summary>
    public string GetParameterName(int index)
    {
        if (!_isLoaded) return "";
        var buf = Marshal.AllocHGlobal(16);
        try
        {
            Dispatch(Vst2.effGetParamName, index, IntPtr.Zero, buf, 0f);
            return Marshal.PtrToStringAnsi(buf) ?? $"Param {index}";
        }
        finally { Marshal.FreeHGlobal(buf); }
    }

    /// <summary>Query the plugin's canDo capability string. Returns 1 (yes), 0 (no), or -1 (unknown).</summary>
    public int CanDo(string capability)
    {
        if (!_isLoaded) return -1;
        var ptr = Marshal.StringToHGlobalAnsi(capability);
        try
        {
            return (int)Dispatch(Vst2.effCanDo, 0, IntPtr.Zero, ptr, 0f);
        }
        finally { Marshal.FreeHGlobal(ptr); }
    }

    // =====================================================================
    // Dispose
    // =====================================================================

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_isLoaded && _effectPtr != IntPtr.Zero)
        {
            Dispatch(Vst2.effMainsChanged, 0, IntPtr.Zero, IntPtr.Zero, 0f); // mains off
            Dispatch(Vst2.effClose, 0, IntPtr.Zero, IntPtr.Zero, 0f);
        }

        FreeUnmanagedBuffers();

        if (_handle != IntPtr.Zero)
        {
            Vst2Native.FreeLibrary(_handle);
            _handle = IntPtr.Zero;
        }

        _isLoaded = false;
        _effectPtr = IntPtr.Zero;

        GC.SuppressFinalize(this);
    }

    // Finalizer as safety net for unmanaged resources.
    ~VstEffect()
    {
        // Note: cannot call Dispatch or FreeLibrary from the finalizer thread
        // because the COM apartment state may be invalid. We CAN free the
        // AllocHGlobal buffers though.
        FreeUnmanagedBuffers();
    }

    private void FreeUnmanagedBuffers()
    {
        if (_stereoBuf     != IntPtr.Zero) { Marshal.FreeHGlobal(_stereoBuf);    _stereoBuf     = IntPtr.Zero; }
        if (_inChannelPtr  != IntPtr.Zero) { Marshal.FreeHGlobal(_inChannelPtr); _inChannelPtr  = IntPtr.Zero; }
        if (_outChannelPtr != IntPtr.Zero) { Marshal.FreeHGlobal(_outChannelPtr);_outChannelPtr = IntPtr.Zero; }
    }

    // =====================================================================
    // Internal helpers
    // =====================================================================

    private IntPtr Dispatch(int opcode, int index, IntPtr value, IntPtr ptr, float opt)
    {
        if (_dispatcher == null || _effectPtr == IntPtr.Zero) return IntPtr.Zero;
        try { return _dispatcher(_effectPtr, opcode, index, value, ptr, opt); }
        catch (Exception ex)
        {
            AppLog.Warning(ex, "[VST] Dispatch exception opcode={Opcode}", opcode);
            return IntPtr.Zero;
        }
    }

    private string? DispatchString(int opcode, int bufSize)
    {
        var buf = Marshal.AllocHGlobal(bufSize);
        try
        {
            // Zero-fill
            for (int i = 0; i < bufSize; i++) Marshal.WriteByte(buf, i, 0);
            Dispatch(opcode, 0, IntPtr.Zero, buf, 0f);
            return Marshal.PtrToStringAnsi(buf);
        }
        finally { Marshal.FreeHGlobal(buf); }
    }

    /// <summary>Read an IntPtr field from the AEffect struct at the given byte offset.</summary>
    private IntPtr ReadPtr(int offset) => Marshal.ReadIntPtr(_effectPtr + offset);

    /// <summary>Allocate (or grow) the reusable stereo scratch buffers.</summary>
    private void AllocateStereoBuffers(int frameCapacity)
    {
        if (_stereoBuf     != IntPtr.Zero) Marshal.FreeHGlobal(_stereoBuf);
        if (_inChannelPtr  != IntPtr.Zero) Marshal.FreeHGlobal(_inChannelPtr);
        if (_outChannelPtr != IntPtr.Zero) Marshal.FreeHGlobal(_outChannelPtr);

        _stereoCapacity = frameCapacity;
        _stereoBuf      = Marshal.AllocHGlobal(frameCapacity * 2 * sizeof(float));
        _inChannelPtr   = Marshal.AllocHGlobal(2 * IntPtr.Size);
        _outChannelPtr  = Marshal.AllocHGlobal(2 * IntPtr.Size);
    }

    /// <summary>Host callback — invoked by the plugin to query host information.</summary>
    private IntPtr HostCallback(IntPtr effect, int opcode, int index, IntPtr value, IntPtr ptr, float opt)
    {
        return opcode switch
        {
            Vst2.audioMasterVersion   => new IntPtr(2400),  // VST version 2.4
            Vst2.audioMasterCurrentId => new IntPtr(1),     // unique instance ID
            Vst2.audioMasterAutomate  => IntPtr.Zero,        // parameter automation — not used
            _                         => IntPtr.Zero
        };
    }
}
