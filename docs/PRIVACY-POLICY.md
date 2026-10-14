# Privacy Policy

Effective date: September 2026 · Contact: privacy@vonvert.app

Vonvert is a real-time voice-changing application for Windows. **All audio is processed
100% locally on your device. Microphone audio is never uploaded or transmitted.**

## What Vonvert does not do

- It does not send your microphone or processed audio anywhere.
- It does not collect hardware fingerprints, telemetry, analytics identifiers, or accounts.
- It does not embed watermarks in your audio.

## The only outbound network request

Vonvert periodically sends a single HTTP GET to GitHub's public Releases API to check for
major-version announcements. That request asks only for public release metadata and carries
no audio, no name, no device identifier and no account information. If the request fails,
the app simply stays silent.

## Your data stays on your machine

- **Logs** are written only to your local data folder (the `logs` directory under your data
  root) and are never transmitted.
- **Diagnostics export** (Settings > Data location > Export diagnostic logs) bundles logs and
  version info into a zip that *you* choose to create and save. Nothing is uploaded; the
  version snapshot deliberately omits your user name, machine name and absolute paths.
- **Recordings and presets** are stored under your data folder. You can move the entire data
  folder (Settings > Data location) and fully erase it when you uninstall.

## Your choices

Sharing any log, recording or preset is always a deliberate action you take. Vonvert does not
collect or upload these on its own.

## Changes and contact

This policy may be updated. Questions: privacy@vonvert.app or the project's GitHub Issues.
