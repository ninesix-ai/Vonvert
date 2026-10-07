# AGENTS.md — Vonvert (public repository)

> For AI coding assistants (Trae, Qoder, Cursor, …) and for anyone onboarding onto this
> repository. Read this before making changes. Everything committed here is public the
> moment it is pushed — treat every file, comment, and commit message that way.

## Project

Vonvert is a real-time voice changer for Windows: it captures the microphone, runs the
signal through a low-latency WASAPI + DSP chain, and sends the processed voice to any
output device (typically a virtual audio cable). WPF / .NET 10. This repository is the
open-source product itself and the single source for releases (current: v0.2.0).
License: Apache-2.0 (see `LICENSE` and `NOTICE`).

## Layout

| Path | Content |
| --- | --- |
| `Vonvert.App/` | WPF application: views, controls, localization, hotkeys, tray |
| `Vonvert.Engine/` | Audio engine, DSP chain, presets, soundboard — keep it UI-free |
| `test/code/Vonvert.Tests/` | xUnit tests; FlaUI desktop E2E live under `UIAutomation/` |
| `docs/` | User guide, device guides, release notes (English; Chinese mirrors under `docs/zh/`) |
| `installer/setup.oss.nsi` | NSIS installer script |
| `script/verify/` | Release content-compliance scanner and its policy file |
| `.github/workflows/build.yml` | CI: build, tests, compliance scan, installer, release on tags |

## Build / test / verify

Requirements: Windows 10/11, .NET 10 SDK, Python 3.10+ (helper scripts use the standard
library only).

```bat
build.bat        :: publish a self-contained exe to Vonvert.App\bin\publish\Vonvert.exe
::   options: --clean  --sign (needs VONVERT_PFX_PASSWORD)  --package (NSIS)  --no-pause
::   build.bat only launches build.py; put real logic in the Python script

dotnet build Vonvert.OSS.sln -c Release
dotnet test  Vonvert.OSS.sln -c Release --no-build --filter "FullyQualifiedName!~UIAutomation"

python script/verify/release_compliance_scan.py --path .   :: must exit 0
python check_locale.py                                     :: localization keys vs usages
```

- The `UIAutomation` E2E tests launch the real exe and need a desktop + audio setup; run
  them locally. CI filters them out.
- Enable the compliance hook once per clone: `git config core.hooksPath .githooks`.
- After any code change, build (and run the affected tests) before calling the task done;
  fix all errors and warnings.

## Releases

1. Bump `<Version>` in `Directory.Build.props` — it also drives the installer version.
2. Add `docs/RELEASE-NOTES-vX.Y.Z.md`; it becomes the GitHub release body.
3. Push a `vX.Y.Z` tag. CI builds, tests, runs the compliance scan, builds the NSIS
   installer, and publishes the release — a failed scan blocks publishing.
4. The in-app update checker reads the Releases list by `v*` tags, so the tag push is the
   release gesture.

## Release compliance gate (hard requirement)

`script/verify/release_compliance_scan.py` (policy:
`script/verify/release-compliance-rules.json`) runs in CI and as a pre-commit hook, and
fails on content that must not appear in a public tree, including:

- paid-edition gating markers and paid-capability names;
- internal process markers: planning-document pointers, finding IDs, version-repair notes;
- provenance hints suggesting code was brought in from elsewhere;
- non-English (CJK) comments in `.cs` / `.xaml` — source comments stay English
  (localization data under `Translations/` and user docs are exempt by design);
- copyright headers not held by `ninesix-ai studio`, and encoding violations
  (UTF-8; BOM only where tooling requires it).

If a finding is a false positive, add a narrow allowlist entry (`file` + `rule` + `pattern`)
with a written `reason` — never delete test data or weaken functionality just to silence
the scan.

## Conventions

- Source comments and commit messages in English; conventional-commit style
  (`feat(scope): …`, `fix(scope): …`, `chore: …`).
- New source files carry `SPDX-License-Identifier: Apache-2.0` and
  `Copyright (c) 2026 ninesix-ai studio` headers.
- UI strings go through the localization keys and `Translations/*.json`; run
  `check_locale.py` after touching them.
- Keep the engine free of UI references; UI code calls into the engine, not the reverse.
- Scripts are Python; `.bat` files only launch them.

## Common pitfalls

- XAML tags must close with the exact matching name
  (`<GeometryDrawing.Geometry>` closes with `</GeometryDrawing.Geometry>`).
- `FindResource()` can return null at runtime — guard non-nullable assignments
  (e.g. `?? Brushes.Xxx`).
- Never put `DynamicResource` inside a binding expression: BAML compiles, then the app
  throws `XamlParseException` at runtime.
- `<Run Text="{Binding …}">` needs an explicit `Mode=OneWay`; the default is TwoWay and a
  write to a read-only property crashes `InitializeComponent` at startup.
- Changing a cross-file API (method signature, renamed type) means updating every caller
  in the same change.
- Close any running Vonvert.exe before rebuilding: it locks DLLs under `bin/` and causes
  MSB3026 / MSB3027 failures.
- Windows Smart App Control may block a freshly built unsigned exe by hash
  (WinError 4551); that is an environment policy on the build machine, not a repo defect.