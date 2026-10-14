// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

namespace Vonvert.Tests.Services;

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Vonvert.Engine;
using Xunit;

/// <summary>
/// LogExportService unit tests — LOG-EXP-01 ~ LOG-EXP-04.
/// Verifies the diagnostic bundle packs logs/crash, refuses to emit an empty
/// archive, overwrites an existing destination, and keeps the snapshot PII-free.
/// Runs entirely against a temp directory; never touches the real user profile.
/// </summary>
public class LogExportServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vlogtest_" + Guid.NewGuid().ToString("N"));

    public LogExportServiceTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { /* best-effort */ } }

    private string Sub(string name) => Path.Combine(_dir, name);

    [Fact(DisplayName = "LOG-EXP-01: no logs and no crash -> false, no zip produced")]
    public void Empty_ReturnsFalse()
    {
        var logs = Sub("logs"); Directory.CreateDirectory(logs);      // empty
        var dest = Sub("out.zip");
        var ok = LogExportService.ExportBundle(dest, logs, Sub("nope.txt"), "diag");
        Assert.False(ok);
        Assert.False(File.Exists(dest));
    }

    [Fact(DisplayName = "LOG-EXP-02: packs logs + crash (as crash.txt) + diagnostics.txt")]
    public void Packs_AllEntries()
    {
        var logs = Sub("logs"); Directory.CreateDirectory(logs);
        File.WriteAllText(Path.Combine(logs, "a.log"), "A");
        File.WriteAllText(Path.Combine(logs, "b.log"), "B");
        var crash = Sub("crash.txt"); File.WriteAllText(crash, "CRASH");
        var dest = Sub("out.zip");

        var ok = LogExportService.ExportBundle(dest, logs, crash, "DIAGTEXT");
        Assert.True(ok);

        using var z = ZipFile.OpenRead(dest);
        var names = z.Entries.Select(e => e.FullName).ToHashSet();
        Assert.Contains("a.log", names);
        Assert.Contains("b.log", names);
        Assert.Contains("crash.txt", names);
        Assert.Contains("diagnostics.txt", names);
    }

    [Fact(DisplayName = "LOG-EXP-03: diagnostics snapshot excludes PII (user / machine)")]
    public void Diagnostics_Excludes_PII()
    {
        var text = LogExportService.BuildDiagnosticsText("9.9.9", "8.8.8", 3);
        Assert.DoesNotContain(Environment.UserName, text);
        Assert.DoesNotContain(Environment.MachineName, text);
    }

    [Fact(DisplayName = "LOG-EXP-04: existing destination zip is overwritten, not thrown")]
    public void Overwrites_Existing_Zip()
    {
        var logs = Sub("logs"); Directory.CreateDirectory(logs);
        File.WriteAllText(Path.Combine(logs, "a.log"), "A");
        var dest = Sub("out.zip");
        File.WriteAllText(dest, "stale");

        Assert.True(LogExportService.ExportBundle(dest, logs, null, "diag"));
        Assert.True(new FileInfo(dest).Length > 0);
    }
}
