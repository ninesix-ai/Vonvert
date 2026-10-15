// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// Guards the first-run legal-consent marker gate (LC-001..004). Runs against a
// temp dir only; never touches the real data root.

namespace Vonvert.Tests.Services;

using System;
using System.IO;
using Vonvert.App;
using Xunit;

public sealed class LegalConsentTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "vconsent_" + Guid.NewGuid().ToString("N"));
    public LegalConsentTests() => Directory.CreateDirectory(_root);
    public void Dispose() { try { Directory.Delete(_root, true); } catch { } }

    [Fact(DisplayName = "LC-001: no marker means not accepted")]
    public void LC001_NoMarker() => Assert.False(LegalConsent.IsAccepted(_root, 1));

    [Fact(DisplayName = "LC-002: recording at the current version satisfies the gate")]
    public void LC002_Record_ThenAccepted()
    {
        Assert.True(LegalConsent.Record(_root, 1));
        Assert.True(LegalConsent.IsAccepted(_root, 1));
    }

    [Fact(DisplayName = "LC-003: an older recorded version does not satisfy a bumped gate")]
    public void LC003_StaleVersion_Reprompts()
    {
        LegalConsent.Record(_root, 1);
        Assert.False(LegalConsent.IsAccepted(_root, 2));   // terms changed -> re-consent
    }

    [Fact(DisplayName = "LC-004: a corrupt marker is treated as not accepted")]
    public void LC004_CorruptMarker()
    {
        File.WriteAllText(LegalConsent.MarkerPath(_root), "not-a-number");
        Assert.False(LegalConsent.IsAccepted(_root, 1));
    }
}
