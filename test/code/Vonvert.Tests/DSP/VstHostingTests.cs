// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// ═══════════════════════════════════════════════════════════════════
//  VST 2.x hosting — unit tests covering the path-validation security
//  guards and the serialisable VstPluginConfig model. The actual native
//  interop (loading a real DLL and calling through function pointers)
//  is exercised only on Windows with a genuine VST2 plugin; these tests
//  pin the pre-load safety layer that gates which paths the host accepts.
// ═══════════════════════════════════════════════════════════════════

namespace Vonvert.Tests.DSP;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Vonvert.Engine.DspEngine;
using Vonvert.Engine.PresetLibrary;
using Xunit;

public class VstHostingTests
{
    // ── IsSafeVstPath (VoiceProfile gate) ──────────────────────────────────

    [Theory(DisplayName = "VST-001: IsSafeVstPath rejects null/empty/whitespace")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsSafeVstPath_RejectsNullEmpty(string? path)
        => Assert.False(VoiceProfile.IsSafeVstPath(path!));

    [Theory(DisplayName = "VST-002: IsSafeVstPath rejects path-traversal sequences")]
    [InlineData(@"C:\Plugins\..\..\Windows\System32\evil.dll")]
    [InlineData("../../etc/passwd.dll")]
    public void IsSafeVstPath_RejectsTraversal(string path)
        => Assert.False(VoiceProfile.IsSafeVstPath(path));

    [Fact(DisplayName = "VST-003: IsSafeVstPath rejects non-.dll extension")]
    public void IsSafeVstPath_RejectsNonDll()
    {
        var tmp = Path.GetTempFileName();
        try
        {
            var dll = tmp + ".exe";
            File.Move(tmp, dll);
            Assert.False(VoiceProfile.IsSafeVstPath(dll));
        }
        finally { /* cleanup below */ }
        // Remove the renamed file
        var dll2 = tmp + ".exe";
        if (File.Exists(dll2)) File.Delete(dll2);
    }

    [Fact(DisplayName = "VST-004: IsSafeVstPath rejects non-existent .dll path")]
    public void IsSafeVstPath_RejectsMissingFile()
        => Assert.False(VoiceProfile.IsSafeVstPath(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".dll")));

    [Fact(DisplayName = "VST-005: IsSafeVstPath accepts a real .dll that exists")]
    public void IsSafeVstPath_AcceptsExistingDll()
    {
        // kernel32.dll always exists on Windows and is a real .dll
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System), "kernel32.dll");
        Assert.True(VoiceProfile.IsSafeVstPath(path));
    }

    // ── ValidatePluginPath (VstManager gate) ───────────────────────────────

    [Theory(DisplayName = "VST-010: ValidatePluginPath rejects null/empty/non-dll")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("C:\\test.txt")]
    [InlineData("C:\\test.exe")]
    public void ValidatePluginPath_RejectsInvalid(string? path)
        => Assert.False(VstManager.ValidatePluginPath(path!));

    [Fact(DisplayName = "VST-011: ValidatePluginPath rejects Windows directory paths")]
    public void ValidatePluginPath_RejectsWindowsDir()
    {
        // kernel32.dll is IN the windows folder — must be rejected
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System), "kernel32.dll");
        Assert.False(VstManager.ValidatePluginPath(path));
    }

    [Fact(DisplayName = "VST-012: ValidatePluginPath rejects files smaller than 1 KB")]
    public void ValidatePluginPath_RejectsTooSmall()
    {
        var tmp = Path.ChangeExtension(Path.GetTempFileName(), ".dll");
        try
        {
            File.WriteAllBytes(tmp, new byte[512]);
            Assert.False(VstManager.ValidatePluginPath(tmp));
        }
        finally { File.Delete(tmp); }
    }

    [Fact(DisplayName = "VST-013: ValidatePluginPath accepts a valid non-system .dll > 1KB")]
    public void ValidatePluginPath_AcceptsValid()
    {
        var tmp = Path.ChangeExtension(Path.GetTempFileName(), ".dll");
        try
        {
            // Write > 1 KB of data
            File.WriteAllBytes(tmp, new byte[2048]);
            Assert.True(VstManager.ValidatePluginPath(tmp));
        }
        finally { File.Delete(tmp); }
    }

    // ── VstPluginConfig serialisation ──────────────────────────────────────

    [Fact(DisplayName = "VST-020: VstPluginConfig round-trips through System.Text.Json")]
    public void VstPluginConfig_JsonRoundTrip()
    {
        var cfg = new VstPluginConfig
        {
            DllPath = @"C:\VST\MyEq.dll",
            IsEnabled = true,
            Parameters = new List<float> { 0.1f, 0.5f, 0.9f }
        };
        var json = JsonSerializer.Serialize(cfg);
        var back = JsonSerializer.Deserialize<VstPluginConfig>(json)!;
        Assert.Equal(cfg.DllPath, back.DllPath);
        Assert.Equal(cfg.IsEnabled, back.IsEnabled);
        Assert.Equal(cfg.Parameters.Count, back.Parameters.Count);
        Assert.Equal(0.5f, back.Parameters[1], 5);
    }

    // ── VoiceProfile VstPlugins list + Clone ───────────────────────────────

    [Fact(DisplayName = "VST-030: VoiceProfile.VstPlugins defaults to empty list")]
    public void VoiceProfile_VstPlugins_DefaultEmpty()
    {
        var vp = new VoiceProfile();
        Assert.NotNull(vp.VstPlugins);
        Assert.Empty(vp.VstPlugins);
    }

    [Fact(DisplayName = "VST-031: VoiceProfile.Clone duplicates VstPlugins (no aliasing)")]
    public void VoiceProfile_Clone_DuplicatesVstPlugins()
    {
        var vp = new VoiceProfile();
        vp.VstPlugins.Add(new VstPluginConfig { DllPath = "test.dll", IsEnabled = true });
        var clone = vp.Clone();
        Assert.Single(clone.VstPlugins);
        Assert.Equal("test.dll", clone.VstPlugins[0].DllPath);
        // Mutate clone — original unaffected
        clone.VstPlugins.Add(new VstPluginConfig { DllPath = "extra.dll" });
        Assert.Single(vp.VstPlugins);
    }

    // ── DspChain integration: profile with empty VstPlugins produces a chain ──

    [Fact(DisplayName = "VST-040: CreateDSPChain with zero VstPlugins builds without error")]
    public void CreateDSPChain_EmptyVstList_Builds()
    {
        var vp = new VoiceProfile { PitchEnabled = true, PitchOffset = 2f };
        var chain = vp.CreateDSPChain();
        Assert.NotNull(chain);
        // No VST effect should appear (list was empty)
        foreach (var fx in chain.Effects)
            Assert.IsNotType<VstEffect>(fx);
    }

    [Fact(DisplayName = "VST-041: CreateDSPChain skips unsafe/invalid Vst paths gracefully")]
    public void CreateDSPChain_InvalidPath_Skipped()
    {
        var vp = new VoiceProfile();
        vp.VstPlugins.Add(new VstPluginConfig { DllPath = @"C:\nonexistent\phantom.dll", IsEnabled = true });
        // Must not throw — the chain builder catches/skips invalid paths
        var chain = vp.CreateDSPChain();
        Assert.NotNull(chain);
        foreach (var fx in chain.Effects)
            Assert.IsNotType<VstEffect>(fx);
    }
}
