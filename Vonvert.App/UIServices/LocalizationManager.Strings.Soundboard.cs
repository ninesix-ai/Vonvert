// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

namespace Vonvert.App.UIServices;

public partial class LocalizationManager
{
    // ── Soundboard view ──
    public string NavSoundboard         => G();
    public string SoundboardTitle       => G();
    public string SoundboardStatusLive             => G();
    public string SoundboardStatusLiveEngineStopped => G();
    public string SoundboardStatusAudition         => G();
    public string SoundboardVolume      => G();
    public string ImportSound           => G();
    public string RemoveSound           => G();
    public string AssignHotkey          => G();
    public string ClearHotkey           => G();
    public string SoundboardHotkeyPrompt => G();
    public string SoundboardPressKey    => G();
    public string SoundboardImportFailed => G();

    // ── Audition / live mode switch (status bar text keys come from
    //    SoundboardStatusPolicy, which is the single source of that mapping) ──
    public string SoundboardModeAudition   => G();
    public string SoundboardModeLive       => G();
    public string SoundboardSwitchToAudition => G();

    // ── Soundboard categories ──
    public string CatDrums   => G();
    public string CatTones   => G();
    public string CatSFX     => G();
    public string CatMemes   => G();
    public string CatMusic   => G();
    public string CatRetro   => G();
    public string CatAmbient => G();
    public string CatImported => G();
}
