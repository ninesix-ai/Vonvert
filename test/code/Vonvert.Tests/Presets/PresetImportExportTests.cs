// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// Covers the .vopreset share/repro path that the new Voices-tab Import/Export
// toolbar exposes: exporting a preset must carry its DSP fields AND its library
// metadata so a second, independent library reproduces it exactly; a legacy
// plain-VoiceProfile file must still import; and importing a name that already
// exists must not silently overwrite the resident preset.

namespace Vonvert.Tests.Presets;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Vonvert.Engine.PresetLibrary;
using Xunit;

public sealed class PresetImportExportTests : IDisposable
{
    private readonly string _srcDir;
    private readonly string _dstDir;

    public PresetImportExportTests()
    {
        _srcDir = MakeDir("Vonvert_PresetIO_src_");
        _dstDir = MakeDir("Vonvert_PresetIO_dst_");
    }

    private static string MakeDir(string prefix)
    {
        var d = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        return d;
    }

    public void Dispose()
    {
        TryDelete(_srcDir);
        TryDelete(_dstDir);
    }

    private static void TryDelete(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
        catch { }
    }

    [Fact(DisplayName = "PIO-001: Export → Import into a fresh library reproduces DSP fields and metadata")]
    public void PIO001_ExportImport_RoundTripsProfileAndMetadata()
    {
        var src = new PresetManager(_srcDir);
        var p = new VoiceProfile
        {
            Name = "Shared", PitchEnabled = true, PitchOffset = 5,
            RobotEnabled = true, DistortEnabled = true, DistortSaturation = 0.7f,
        };
        src.Save(p);
        src.Index.UpdateMetadata("Shared", m =>
        {
            m.Notes = "made for repro";
            m.Tags = new List<string> { "pro", "stage" };
            m.Category = "FX";
        });

        var file = Path.Combine(_srcDir, "shared.vopreset");
        src.Export(p, file);
        Assert.True(File.Exists(file));

        // Import into an independent library (empty of "Shared").
        var dst = new PresetManager(_dstDir);
        var got = dst.Import(file);

        Assert.NotNull(got);
        Assert.Equal(5, got!.PitchOffset);
        Assert.True(got.PitchEnabled);
        Assert.True(got.RobotEnabled);
        Assert.True(got.DistortEnabled);
        Assert.Equal(0.7f, got.DistortSaturation, 3);

        var meta = dst.Index.GetMetadata("Shared");
        Assert.NotNull(meta);
        Assert.Equal("made for repro", meta!.Notes);
        Assert.Contains("pro", meta.Tags);
        Assert.Equal("FX", meta.Category);
    }

    [Fact(DisplayName = "PIO-002: Import accepts a legacy plain VoiceProfile JSON (no envelope)")]
    public void PIO002_LegacyPlainJson_Imports()
    {
        var dst = new PresetManager(_dstDir);
        var file = Path.Combine(_dstDir, "legacy.json");
        File.WriteAllText(file, """{"Name":"Legacy","PitchEnabled":true,"PitchOffset":-3}""");

        var got = dst.Import(file);

        Assert.NotNull(got);
        Assert.Equal("Legacy", got!.Name);
        Assert.Equal(-3, got.PitchOffset);
    }

    [Fact(DisplayName = "PIO-003: Importing a name that already exists is renamed, never overwrites")]
    public void PIO003_NameCollision_AppendsImportedSuffix()
    {
        var dst = new PresetManager(_dstDir);
        dst.Save(new VoiceProfile { Name = "Mine", PitchOffset = 1 });

        // A separate export whose preset shares the name "Mine" but differs.
        var src = new PresetManager(_srcDir);
        var other = new VoiceProfile { Name = "Mine", PitchOffset = 8 };
        src.Save(other);
        var file = Path.Combine(_srcDir, "mine.vopreset");
        src.Export(other, file);

        var got = dst.Import(file);

        Assert.NotNull(got);
        Assert.Equal("Mine (imported)", got!.Name);
        Assert.Equal(8, got.PitchOffset);
        // The resident preset is untouched.
        Assert.Equal(1, dst.Presets.First(x => x.Name == "Mine").PitchOffset);
    }

    [Fact(DisplayName = "PIO-004: Exported file is a vonvert-preset envelope with camelCase keys")]
    public void PIO004_Export_WritesVersionedEnvelope()
    {
        var src = new PresetManager(_srcDir);
        var p = new VoiceProfile { Name = "Env", PitchOffset = 2 };
        src.Save(p);
        var file = Path.Combine(_srcDir, "env.vopreset");

        src.Export(p, file);

        var root = JObject.Parse(File.ReadAllText(file));
        Assert.Equal("vonvert-preset", (string?)root["format"]);
        Assert.Equal(1, (int?)root["version"]);
        Assert.NotNull(root["preset"]);
    }
}
