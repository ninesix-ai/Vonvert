// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// Regression guards for preset robustness:
//   PF-014..PF-016 — imported / on-disk presets are numerically validated, so a
//     single NaN or absurd value can no longer silently poison every DSP output
//     frame; PresetValueGuard resets out-of-trust parameters to their declared
//     defaults.
//   PF-017..PF-018 — deletion residue: Save's unique-path guard can leave
//     invisible "Name_1.json" shadow files, so Delete must remove every file
//     declaring the preset's name to prevent it resurrecting on the next load.

namespace Vonvert.Tests.Presets;

using System;
using System.IO;
using System.Linq;
using Vonvert.Engine.PresetLibrary;
using Xunit;

public sealed class PresetManagerRobustnessTests : IDisposable
{
    private readonly string _tempDir;

    public PresetManagerRobustnessTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "Vonvert_PresetRobustness_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true); }
        catch { }
    }

    // ── Imported / on-disk preset value hardening ───────────────

    [Fact(DisplayName = "PF-014: PresetManager — Import resets NaN/Infinity values to safe defaults")]
    public void PF014_ImportNonFiniteValues_ResetToDefaults()
    {
        // A single non-finite parameter poisons the whole DSP chain (NaN is sticky).
        // Newtonsoft accepts bare NaN / Infinity literals, so the guard must catch them.
        var mgr = new PresetManager(_tempDir);
        var path = Path.Combine(_tempDir, "nan.vopreset");
        File.WriteAllText(path,
            """{"format":"vonvert-preset","version":1,"preset":{"Name":"NaNBad","PitchOffset":NaN,"PreAmpGain":Infinity,"EqLowFreq":-Infinity}}""");

        var imported = mgr.Import(path);

        Assert.NotNull(imported);
        Assert.Equal(0f, imported.PitchOffset);      // fresh VoiceProfile() default
        Assert.Equal(1.0f, imported.PreAmpGain);     // fresh default is 1.0, not 0
        Assert.Equal(150f, imported.EqLowFreq);      // declared default

        // What got written to disk must be clean too — a reload sees the same defaults
        var reloaded = new PresetManager(_tempDir);
        var p = reloaded.Presets.FirstOrDefault(x => x.Name == "NaNBad");
        Assert.NotNull(p);
        Assert.Equal(0f, p!.PitchOffset);
    }

    [Fact(DisplayName = "PF-015: PresetManager — Import resets absurd magnitudes to safe defaults")]
    public void PF015_ImportAbsurdMagnitude_ResetToDefault()
    {
        var mgr = new PresetManager(_tempDir);
        var path = Path.Combine(_tempDir, "absurd.vopreset");
        File.WriteAllText(path,
            """{"format":"vonvert-preset","version":1,"preset":{"Name":"BigBad","PitchOffset":1e30,"EqLowFreq":-1e12,"ChorusMix":0.4}}""");

        var imported = mgr.Import(path);

        Assert.NotNull(imported);
        Assert.Equal(0f, imported.PitchOffset);      // absurd → default
        Assert.Equal(150f, imported.EqLowFreq);      // absurd → declared default
        Assert.Equal(0.4f, imported.ChorusMix);      // in-range value preserved
    }

    [Fact(DisplayName = "PF-016: PresetManager — NaN in a disk preset is sanitized on load")]
    public void PF016_LoadFromDisk_NonFiniteValuesSanitized()
    {
        // Disk files are as untrusted as imports (hand-edited / earlier poisoning).
        File.WriteAllText(Path.Combine(_tempDir, "OnDisk.json"),
            "{\"Name\":\"OnDisk\",\"PitchOffset\":NaN,\"EqLowFreq\":1e20}");

        var mgr = new PresetManager(_tempDir);
        var p = mgr.Presets.FirstOrDefault(x => x.Name == "OnDisk");
        Assert.NotNull(p);

        Assert.Equal(0f, p!.PitchOffset);    // default
        Assert.Equal(150f, p.EqLowFreq);     // default
    }

    // ── Deletion residue (shadow files from the unique-path scheme) ──

    [Fact(DisplayName = "PF-017: PresetManager — Delete removes duplicate-name shadow files (no resurrection)")]
    public void PF017_Delete_DuplicateNameShadowFiles_Removed()
    {
        // Legacy residue state: two files both declaring Name "Residue".
        // Deleting only "Residue.json" leaves "Residue_1.json" behind and the
        // preset resurrects on the next LoadFromDisk.
        var json = "{\"Name\":\"Residue\",\"PitchOffset\":2.0}";
        File.WriteAllText(Path.Combine(_tempDir, "Residue.json"), json);
        File.WriteAllText(Path.Combine(_tempDir, "Residue_1.json"), json);

        var mgr = new PresetManager(_tempDir);
        var p = mgr.Presets.FirstOrDefault(x => x.Name == "Residue");
        Assert.NotNull(p);

        mgr.Delete(p!);

        Assert.False(File.Exists(Path.Combine(_tempDir, "Residue.json")));
        Assert.False(File.Exists(Path.Combine(_tempDir, "Residue_1.json")));

        var mgr2 = new PresetManager(_tempDir);
        Assert.DoesNotContain("Residue", mgr2.Presets.Select(x => x.Name));
    }

    [Fact(DisplayName = "PF-018: PresetManager — Delete after duplicate-name Save leaves zero residue")]
    public void PF018_Delete_AfterDuplicateSave_RemovesAllMatchingFiles()
    {
        var mgr = new PresetManager(_tempDir);
        var a = new VoiceProfile { Name = "Dup" };
        var b = new VoiceProfile { Name = "Dup", PitchOffset = 9f };
        mgr.Save(a);
        mgr.Save(b);   // unique-path guard: b lands in "Dup_1.json" as an invisible shadow file

        mgr.Delete(a);

        Assert.Empty(Directory.GetFiles(_tempDir, "Dup*.json"));
        var mgr2 = new PresetManager(_tempDir);
        Assert.DoesNotContain("Dup", mgr2.Presets.Select(x => x.Name));
    }

    // ── Auto-pitch normalization fields survive export/import round-trip ──

    [Fact(DisplayName = "PF-019: VoiceProfile — AutoPitchTarget/TargetF0Hz round-trip preserved")]
    public void PF019_AutoPitchFields_RoundTrip_Preserved()
    {
        var mgr = new PresetManager(_tempDir);
        var p = new VoiceProfile
        {
            Name = "AutoRT", PitchEnabled = true, PitchOffset = 4,
            AutoPitchTarget = true, TargetF0Hz = 220f
        };
        mgr.Save(p);

        var mgr2 = new PresetManager(_tempDir);
        var got = mgr2.Presets.First(x => x.Name == "AutoRT");
        Assert.True(got.AutoPitchTarget);
        Assert.Equal(220f, got.TargetF0Hz);
    }
}
