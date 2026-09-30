# Release Notes — Vonvert v0.2.0

<!-- This file doubles as the GitHub Release body, where relative links are not resolved -->
<!-- against the repository path, so every cross-reference below is an absolute URL. -->

> Language: [English](https://github.com/ninesix-ai/Vonvert/blob/main/docs/RELEASE-NOTES-v0.2.0.md) · [简体中文](https://github.com/ninesix-ai/Vonvert/blob/main/docs/zh/RELEASE-NOTES-v0.2.0.md)

The second feature release after v0.1.0, spanning 11 commits. This version adds no new DSP effects; instead it takes the v0.1.0 feature set **to more languages, explains it better, and signs it more completely**: ten new UI languages shipped, a major user-guide expansion, and a code-signing fix that covers every payload.

## What's new

### Localization (the biggest increment)
- The UI language list grows from 2 (English, Chinese) to **12**: German, French, Spanish, Brazilian Portuguese, Russian, Italian, Polish, Turkish, Japanese and Korean are new. The language picker is driven by the single source of truth `SupportedLanguages` and stays strictly aligned with the shipped translation files.
- **Personas, soundboard and dialog text are fully localized**: preset group headings, pad names and categories, the setup wizard and the spotlight tour all refresh on language switch.
- Fixed the "My role" card and several imperatively-assigned labels **keeping the old language after a switch** — those texts were not hooked into the language-refresh chain; it was never a missing translation.
- International audio terms (Reverb, EQ, …) stay in their original form by policy; only CJK locales transcribe them.

### User guide expansion
- The guide is restructured into **4 parts and 15 chapters** with a table of contents, adding the chapters it never had: Auto pitch (register normalization), DSP effect explainers, Troubleshooting and Reference.
- Every chapter was re-checked against the source; claims that contradicted the code were corrected.
- The Chinese guide was re-mirrored to the new English structure, chapter for chapter.
- Privacy and compliance cross-links now point at the real heading anchors.

### Build & release
- **Full-payload code signing**: `python build.py --sign` now signs `Vonvert.exe` **and every `Vonvert*.dll`**, including the managed entry assembly. Previously only the apphost was signed, so a machine policy could still block the process while loading an unsigned `Vonvert.dll` — the first real-world problem this release fixes.
- Pushing a `v*` tag now makes CI attach the NSIS installer to the matching GitHub Release automatically; the release body is taken from this directory's `RELEASE-NOTES-<tag>.md` when it exists.
- The installer's SHA-256 checksum is published with these notes so you can verify the download.

## Fixed
- The "My role" card and imperatively-set labels no longer keep the previous language after a language switch.
- Signing/launch chain: silent start failures caused by an application-control policy refusing the unsigned managed assembly are gone once every payload is signed (see above).

## Upgrade note (important)
The in-app "new version available" badge **only appears on a MAJOR version jump**. 0.1.x → 0.2.0 is a minor upgrade, so old clients will show no prompt at all. Get the new version from the [Releases page](https://github.com/ninesix-ai/Vonvert/releases) or the download link in the README.

## Requirements
- Windows 10/11, one microphone, headphones or speakers.
- A virtual audio device is recommended so chat apps and games receive the changed voice. See the [VB-CABLE guide](https://github.com/ninesix-ai/Vonvert/blob/main/docs/VB-CABLE.md).

## Note: unsigned build
The installer published on GitHub Releases is still **not code-signed** (local builds can self-sign with `python build.py --sign`). On first run Windows SmartScreen may show "Windows protected your PC" — expected for unsigned software, **not a virus warning**. Click **More info → Run anyway**.

## Install
Run `Vonvert_Setup.exe` (user-level install, no administrator required).

## File checksum (SHA-256)
This build is unsigned; verify the download's integrity by comparing its hash:

| File | Size | SHA-256 |
|---|---|---|
| `Vonvert_Setup.exe` | 53,440,630 bytes (51.0 MB) | `4ccf8e299b82fa6716145b2208d7c06d2d90dd6457778f3f1354f6683a704089` |

Compute it yourself in PowerShell:

```powershell
Get-FileHash .\Vonvert_Setup.exe -Algorithm SHA256
```

The output must match the table exactly. If it does not, do not install — re-download from the [Releases page](https://github.com/ninesix-ai/Vonvert/releases/tag/v0.2.0).

> The hash above is the digest GitHub records for the attached installer, built by CI from the `v0.2.0` tag. It proves the file was not tampered with or truncated and is **not a substitute for code signing** (see the unsigned-build note above).
>
> A self-contained .NET publish is not byte-reproducible, so an installer you build yourself from the same tag will differ in size and hash — that is expected and is not a tampering signal. Compare against the table only for the file downloaded from this Release.

## License
Apache-2.0. Third-party components are listed in [NOTICE](https://github.com/ninesix-ai/Vonvert/blob/main/NOTICE).

---

*This file is the canonical English release notes. Chinese: [docs/zh/RELEASE-NOTES-v0.2.0.md](https://github.com/ninesix-ai/Vonvert/blob/main/docs/zh/RELEASE-NOTES-v0.2.0.md). Other languages arrive with the website and in-app localization pipeline; until then the [GitHub Release page](https://github.com/ninesix-ai/Vonvert/releases/tag/v0.2.0) carries this English text as its body.*
