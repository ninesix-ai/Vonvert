# Vonvert

> Language: [English](README.md) · [简体中文](README_zh.md)

> **Download [v0.0.1](https://github.com/ninesix-ai/Vonvert/releases/tag/v0.0.1)** — Windows real-time voice changer (unsigned preview; see the [release notes](docs/RELEASE-NOTES-v0.0.1.md) for SmartScreen steps).

**Real-time voice changer for Windows** — transform your voice while you speak, with low-latency WASAPI audio and five out-of-the-box voice presets.

Vonvert captures your microphone, runs the signal through a real-time DSP chain (pitch shift, EQ, compressor, chorus, …) and sends the processed voice to any output — typically a virtual audio cable so chat apps / games use your new voice as their mic.

## Features

- **Real-time voice changing** — five built-in voice presets (Normal, Deep Male, Female, Robot, Demon); Female is applied automatically on launch
- **Low latency** — WASAPI capture/playback, ~10 ms processing window
- **Hear Myself** — optional ear-monitor loopback so you can hear your own processed voice in your headphones (Settings → Hear Myself)
- **Live visualizers** — real-time spectrum and pitch display while you speak
- **Device selection** — pick your input microphone and output device; VB-Cable friendly
- **Minimal, focused UI** — bilingual English / 简体中文 interface (auto-detected, switchable in Settings), tray icon, no configuration hustle

## Requirements

- Windows 10/11
- A microphone and headphones/speakers
- Optional but recommended: a **virtual audio device** such as [VB-Cable](https://vb-audio.com/Cable/) so the processed voice becomes a selectable microphone in your chat app or game. VoiceMeeter and VAC also work. See the [virtual audio device setup guide](docs/VB-CABLE.md) and the [device comparison guide](docs/VIRTUAL-AUDIO-DEVICES.md).

## Getting started

> New to Vonvert? Read the full **[user guide](docs/USER-GUIDE.md)** for step-by-step setup, presets, routing, hotkeys and troubleshooting.

1. Install a virtual audio device — [VB-Cable](https://vb-audio.com/Cable/) is the recommended default (VoiceMeeter / VAC also work); see the [setup guide](docs/VB-CABLE.md).
2. Launch `Vonvert.exe`.
3. Open **Settings** (left sidebar):
   - **Input Microphone** → your real microphone (e.g. headset mic)
   - **Output** → `CABLE Input (VB-Audio Virtual Cable)`
   - Toggle **Hear Myself** to monitor your own voice in your headphones.
4. In your chat app / game, set the microphone to `CABLE Output (VB-Audio Virtual Cable)`.
5. Speak — the built-in Female voice applies immediately (voice change is switched ON by default); switch between the five presets from the voice picker.

## Building from source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download) (Windows).

```bash
# Build and publish a self-contained exe:
# output: Vonvert.App/bin/publish/Vonvert.exe
build.bat            # or: python build.py

# Clean build
build.bat --clean    # or: python build.py --clean

# Build only (no publish)
dotnet build Vonvert.OSS.sln -c Release
```

The script also supports code signing for local testing: `build.bat --sign` (or
`python build.py --sign`) — requires a code-signing certificate and the
`VONVERT_PFX_PASSWORD` environment variable.

### Installer (optional, NSIS)

CI builds a user-level setup package (`installer/setup.oss.nsi`, installed under
`%LOCALAPPDATA%\Programs\Vonvert` — no admin required). To build it locally you
need [NSIS](https://nsis.sourceforge.io) on PATH:

```bash
makensis /DBUILD_DIR=..\Vonvert.App\bin\publish installer\setup.oss.nsi
# output: installer/Output/Vonvert_Setup.exe
```

## Running the tests

```bash
dotnet test Vonvert.OSS.sln -c Release
```

The suite covers the pitch-shifting engine (the core of the voice changing
feature), the ring-modulation and soft-clip distortion effects, the built-in
preset pack (loading and DSP-chain wiring), the end-to-end female-preset
processing path, and the audio pipeline contracts.

## Project layout

```
Vonvert.App/       WPF user interface (window, controls, localization)
Vonvert.Engine/    Audio engine: WASAPI capture/output, DSP chain, presets,
                   real-time analyzers
test/              xUnit test suite
```

## License

Apache License 2.0 — see [LICENSE](LICENSE). Third-party components and their
licenses are listed in [NOTICE](NOTICE).