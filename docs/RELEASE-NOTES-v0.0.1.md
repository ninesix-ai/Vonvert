# Release Notes — Vonvert v0.0.1

First open-source release of **Vonvert** — a real-time voice changer for Windows.

## What's included
- **Real-time voice changing** with 5 built-in presets: Normal, Deep Male, Female, Robot, Demon (Female applies on launch).
- **Low latency** — WASAPI capture/playback, ~10 ms processing window.
- **Hear Myself** — monitor your processed voice in your headphones.
- **Push-to-Talk and configurable global hotkeys** — hold-to-transmit, rebindable in Settings.
- **Expert parameter panel** — pitch, EQ, compressor, chorus and more.
- **Preset import / export** as single `.vopreset` files.
- **Live spectrum and pitch visualizers** — watch your voice change as you speak.
- **Device selection**, friendly to VB-Cable / VoiceMeeter / VAC virtual audio devices.
- **Bilingual English / Simplified Chinese UI** (auto-detected, switchable in Settings); tray icon.

## Requirements
- Windows 10/11, a microphone, and headphones/speakers.
- Recommended: a virtual audio device (e.g. VB-Cable) so chat apps/games use your new voice. See [VB-CABLE setup guide](VB-CABLE.md).

## Note: unsigned build
This is an early, **not code-signed** release. On first run Windows SmartScreen may show *"Windows protected your PC"* — that is expected for unsigned software, **not a virus alert**. Click **More info → Run anyway**.

## Install
Run `Vonvert_Setup.exe` (user-level, installs under `%LOCALAPPDATA%\Programs\Vonvert`, no admin required).

## License
Apache-2.0. Third-party components are listed in [NOTICE](../NOTICE).
