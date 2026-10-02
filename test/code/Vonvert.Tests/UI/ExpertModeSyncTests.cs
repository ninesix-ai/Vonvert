// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio
namespace Vonvert.Tests.UI;

using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

// The Simple/Professional ComboBox (ExpertModeCombo) and the expert panel
// (ExpertParamsControl) keep two independent state stores: the combo drives the
// panel via SelectionChanged, but the role system used to call ExpertPanel.SetMode
// directly without touching the combo. With a persisted Professional complexity the
// panel showed every group while the combo still read "Simple" — and the first user
// toggle was swallowed by SetMode's same-state no-op guard. These guards pin the
// sync: ApplyRoleToPanel must re-point the combo so label and content always agree.
public sealed class ExpertModeSyncTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Vonvert.OSS.sln")))
            dir = dir.Parent;
        return dir!.FullName;
    }

    private static string RoleSystemSource =>
        File.ReadAllText(Path.Combine(RepoRoot, "Vonvert.App", "MainWindow.RoleSystem.cs"));

    /// <summary>Extract the body of ApplyRoleToPanel so assertions scope to it only.</summary>
    private static string ApplyRoleToPanelBody()
    {
        var match = Regex.Match(RoleSystemSource,
            @"void\s+ApplyRoleToPanel\s*\(\s*\)\s*\{(?<body>.*?)\n    \}",
            RegexOptions.Singleline);
        Assert.True(match.Success, "ApplyRoleToPanel not found in MainWindow.RoleSystem.cs - update this guard");
        return match.Groups["body"].Value;
    }

    [Fact(DisplayName = "SYNC-001: ApplyRoleToPanel syncs ExpertModeCombo so the visible label matches the panel mode")]
    public void ApplyRoleToPanel_SyncsComboSelection()
    {
        var body = ApplyRoleToPanelBody();
        Assert.Matches(@"ExpertModeCombo\s*\.\s*SelectedIndex\s*=", body);
    }

    [Fact(DisplayName = "SYNC-002: ApplyRoleToPanel still applies the resolved mode to the panel")]
    public void ApplyRoleToPanel_StillAppliesModeToPanel()
    {
        var body = ApplyRoleToPanelBody();
        Assert.Matches(@"ExpertPanel\s*\?\.\s*SetMode\s*\(\s*professional\s*\)", body);
    }
}
