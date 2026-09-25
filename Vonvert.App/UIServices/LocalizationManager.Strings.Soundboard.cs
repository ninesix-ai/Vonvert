// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

namespace Vonvert.App.UIServices;

public partial class LocalizationManager
{
    // ── Soundboard view ──
    public string NavSoundboard         => G();
    public string SoundboardTitle       => G();
    public string SoundboardEngineHint  => G();
    public string SoundboardVolume      => G();
    public string ImportSound           => G();
    public string RemoveSound           => G();
    public string AssignHotkey          => G();
    public string ClearHotkey           => G();
    public string SoundboardHotkeyPrompt => G();
    public string SoundboardPressKey    => G();
    public string SoundboardImportFailed => G();

    // ── Audition / live mode switch ──
    public string SoundboardModeAudition   => G();
    public string SoundboardModeLive       => G();
    public string SoundboardLiveModeNotice => G();

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
