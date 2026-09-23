<!--
TRANSLATOR NOTES (do not render to users; keep this HTML comment or translate it
for internal use only):
- Target audience: general Windows users. Use short, direct sentences
  (subject-verb-object). Avoid idioms, slang and culture-specific metaphors.
- Do NOT translate: product names (Vonvert, VB-Cable, VoiceMeeter, Virtual Audio
  Cable, Discord, Zoom, Teams, OBS), company names (VB-Audio), file names
  (Vonvert.exe, app-config.json, *.vopreset), URLs, device names shown in Windows
  (CABLE Input, CABLE Output), key names (F9, F10, Ctrl, Alt), and format names
  (WASAPI, kHz, ms, dB).
- Keep UI labels exactly as the app shows them; see the button names quoted in
  this guide. Reuse ONE consistent word per term across the whole document.
- Keep the table structure, column order, and section numbers identical.
- ✓ = yes/supported, ✗ = no/not supported. These symbols stay as-is.
- Version numbers, latency figures and download URLs change over time; keep the
  "check the official page" disclaimers.
-->

# Vonvert user guide

> Language: English · [简体中文](zh/USER-GUIDE.zh.md)

This is the full user guide for **Vonvert**, a real-time voice changer for
Windows. It covers everything from a first-time install to advanced voice
tuning. If you only want the shortest path to "it works", read
**Part I → Chapter 2 (Quick start)** and skip the rest.

- Vonvert changes your voice **while you speak**, and can send that changed
  voice into any other app (chat, meeting, game, stream).
- Audio is processed **on your own computer**. See [Chapter 11 · Privacy](#privacy).

---

# Part I — Getting started

## Chapter 1 · Know and install Vonvert

### 1.1 What Vonvert is

#### 1.1.1 What it does
- Captures your **microphone** in real time.
- Runs the voice through a **DSP chain** (pitch, EQ, reverb, chorus, compressor,
  robot, distortion, and more).
- Outputs the changed voice to a device you choose — usually a **virtual audio
  cable**, so other apps treat it as a normal microphone.

#### 1.1.2 The signal path
```
Your microphone
      |
      v
  Vonvert  (real-time DSP: pitch / EQ / reverb / ...)
      |
      +--> CABLE Input ---[ virtual cable ]--- CABLE Output
                                                     |
                                                     v
                                       Your chat app / game (as its microphone)
      |
      +--> (optional) Hear Myself  --> your headphones  (local monitor)
```
The virtual cable is optional but strongly recommended — see [Chapter 4](#chapter-4--audio-routing-getting-the-voice-into-other-apps).

#### 1.1.3 Key terms
| Term | Meaning |
|---|---|
| Preset (voice) | A ready-made bundle of effect settings, e.g. **Female**, **Demon**. |
| Dry signal | Your original, unprocessed microphone voice. |
| Wet / processed | The voice after the effects are applied. |
| Virtual audio device (cable) | A software "fake" microphone + speaker that moves audio between apps (VB-Cable, VoiceMeeter, VAC). |
| Cable Input / Cable Output | The speaker end and the microphone end of a virtual cable. |
| Latency | The delay from speaking to hearing/output. Lower is better. |
| VU meter | The input/output level bars in the bottom bar. |

### 1.2 Conventions used in this guide

#### 1.2.1 Edition and license
This guide documents the **open-source (OSS) edition** of Vonvert on GitHub.
It is licensed under the **Apache License 2.0**. Some features described in
other guides (trial, licensing, extra effects) belong to separate editions and
are not part of this build.

#### 1.2.2 Symbols
- ✓ supported · ✗ not supported · – not applicable.

#### 1.2.3 Names kept as-is
Product, device, file, key and URL names are never translated.

### 1.3 Prepare and install

#### 1.3.1 System requirements
- Windows 10 or Windows 11.
- A microphone and headphones/speakers (headphones reduce echo — see [Chapter 10](#chapter-10--troubleshooting)).
- Optional but recommended: a virtual audio device such as VB-Cable.

#### 1.3.2 Hardware
Plug in your microphone and headphones **before** launching, then click
**↻ Refresh Devices** in Settings if Vonvert does not list them.

#### 1.3.3 Download and install
- **Portable:** run `Vonvert.exe` — no install needed.
- **Installer (optional):** a user-level setup package installs under
  `%LOCALAPPDATA%\Programs\Vonvert` and does **not** require administrator rights.
- Building from source, tests and packaging are for developers — see the
  repository `README.md`.

#### 1.3.4 (Optional) Virtual audio device
To let other apps hear your changed voice, install a virtual audio device once.
This guide does **not** repeat the install steps — see:
- **[VB-CABLE setup guide](VB-CABLE.md)** — the recommended default, step by step.
- **[Virtual audio devices comparison](VIRTUAL-AUDIO-DEVICES.md)** — compare all options.

---

## Chapter 2 · Quick start (5 minutes)

### 2.1 First launch and the window
The window has three areas:
- **Left icon rail** — switch between **Voice Change**, **Settings**, **About**.
- **Center content** — the voice picker and the live visualizers.
- **Bottom bar** — brand + status, the big **Voice Change** on/off button, and the
  input/output **VU** meters with a **Latency** read-out.

The header (top-right) has the usual **Minimize / Maximize / Close** buttons.
Clicking **Close** minimizes Vonvert to the **system tray** instead of quitting
(right-click the tray icon → **Quit** to exit fully).

### 2.2 Three steps to hear it
1. **Pick devices** — Settings → set **Input Microphone** to your real mic, and
   **Output** to `CABLE Input (VB-Audio Virtual Cable)`.
2. **Turn the voice on** — click the **Voice Change** button in the bottom bar.
   - It shows **Running (Effects ON)** when the effect is applied.
   - When off it shows **Standby (Direct Monitor)** — audio still passes through,
     just unchanged.
3. **Pick a voice and talk** — click a preset tile (the **Female** voice is
   applied automatically on launch). Talk into your mic; the **IN/OUT** bars move.

> Tip shown in the app: *"Pick a voice, then talk into your mic — the effect is
> on out of the box."*

### 2.3 Self-check
- The **IN** bar should move when you speak → your mic is captured.
- The **OUT** bar should move → audio is being produced.
- If others still hear your raw voice, see [Chapter 4](#chapter-4--audio-routing-getting-the-voice-into-other-apps).

---

# Part II — Everyday use

## Chapter 3 · Voice presets

### 3.1 Built-in presets
Vonvert ships with five voices. Click a tile to apply it instantly; a toast
confirms *"Preset applied: …"*.

| Preset (EN) | 中文 | Icon | Character |
|---|---|---|---|
| Normal | 原声 | 🎙️ | Clean pass-through (gate + compressor only) |
| Deep Male | 低沉男声 | 🎺 | Lower pitch, warmer low end |
| Female | 女声 | 🌸 | Higher pitch, brighter, de-essed (applied on launch) |
| Robot | 机器人 | 🤖 | Ring-modulated robotic tone |
| Demon | 恶魔 | 😈 | Very low pitch with distortion and reverb |

> Built-in presets are **read-only** — they cannot be deleted or overwritten.

### 3.2 Select and switch
- **Click** a preset tile to apply it.
- Press **F11** (default) to cycle to the **next preset** without touching the mouse.
- To understand what each preset changes inside, see [Chapter 9](#chapter-9--understanding-the-voices-dsp-effects).

### 3.3 Import, export and delete presets
The voice picker has two buttons under the tiles: **Import** and **Export**.
- **Export** writes the currently selected preset to a `Vonvert preset (*.vopreset)`
  file, so you can share it or keep a backup.
- **Import** loads a shared `.vopreset` file and adds it to your picker.
- To **delete** a preset you added, **right-click its tile** → **Delete** and
  confirm. Built-in presets cannot be deleted.
- If the file is not a valid preset, you get *"Import failed — not a valid
  preset file"*.

---

## Chapter 4 · Audio routing (getting the voice into other apps)

### 4.1 The principle
Vonvert writes the changed voice to an **output** device. For another app to
hear it, that output must be a **virtual cable's Input**, and the app's
microphone must be the same cable's **Output**.

### 4.2 Choosing a device
- **VB-Cable (recommended)** — one cable, simplest. See
  **[VB-CABLE setup guide](VB-CABLE.md)**.
- **VoiceMeeter** — a full virtual mixer, good when you also mix music or run
  several mics. See **[comparison guide](VIRTUAL-AUDIO-DEVICES.md)**.
- **VAC / others** — many independent cables, pro routing. Also in the
  **[comparison guide](VIRTUAL-AUDIO-DEVICES.md)**.

> Vonvert auto-detects any active output whose name contains **CABLE** or
> **VoiceMeeter**; otherwise pick it manually in **Settings → Output**.

### 4.3 Per-app settings
In each app, set its **microphone / input device** to `CABLE Output`:

| App | Where to set it |
|---|---|
| Discord | Settings → Voice & Video → **Input Device** |
| Zoom | Settings → Audio → **Microphone** |
| Teams | Settings → Devices → **Microphone** |
| OBS | Source → Audio Input Capture → **Device** |
| Games | In-game Audio/Voice settings → **Input / Microphone** |

> The exact menu names can differ by app version.

---

## Chapter 5 · Monitoring, compare and live meters

### 5.1 Hear Myself (monitor your own voice)
**Settings → Hear Myself** toggles a local monitor so you can hear your own
**changed** voice in your headphones while you speak.
- **On** = you hear the processed voice locally.
- **Off** (default) = only the output cable carries it; you hear nothing locally.
- If you get echo or feedback, turn it **off** or use headphones.

> **Hear Myself** does not change *what* is produced — it only decides whether
> that audio is also sent back to your headphones. It works together with the
> A/B control below.

### 5.2 A/B compare (DRY / A/B / WET)
The segmented control at the top-right of the voice page changes **what the
output contains**:

| Button | You hear | Use it to |
|---|---|---|
| **DRY** (原声) | Your original, unprocessed voice | Judge how much the effect changed you |
| **A/B** (Normal) | The full processed voice (what others hear) | Normal use |
| **WET** (全效果) | Only what the effects add (processed − dry) | Hear a single effect's "coloration" clearly |

- **DRY + Hear Myself** = hear your raw voice.
- **A/B + Hear Myself** = hear your changed voice.
- Switching back to **A/B** returns to the normal effect output.

> **WET** is a subtraction (processed result minus the dry signal), so it sounds
> thin — that is expected; it is for comparison, not for daily use.

### 5.3 Live visualizers
While the engine runs, the voice page shows:
- **SPECTRUM** — a real-time frequency spectrum, with the detected **note** and
  **cents** deviation shown on the right.
- **PITCH** — a short pitch curve over time.
- **IN / OUT** VU bars and a **Latency: … ms** read-out in the bottom bar.

These are read-only feedback; nothing to configure.

---

## Chapter 6 · Hotkeys and push-to-talk

### 6.1 Default hotkeys
Global hotkeys work even when Vonvert is not focused.

| Action | Default key | In-app label |
|---|---|---|
| Turn voice On / Off | **F9** | On / Off |
| Mute (silence output) | **F10** | Mute |
| Next preset | **F11** | Next Preset |
| Push to Talk | **F12** | Push to Talk |

### 6.2 Rebinding, conflicts and reset
In **Settings → HOTKEYS**: click a shortcut button, then press a new key
(a modifier combination or an F-key). If the key is already used, you see
*"Conflict: already bound to …"*. Use **↻ Reset to defaults** to restore F9–F12.

### 6.3 Push-to-Talk mode
**Settings → Push-to-Talk mode** chooses what the PTT key does:
- **Hold to talk** — normally muted; sound is sent **only while you hold** the key.
- **Hold to mute** — normally speaking; you go silent **only while you hold**.

The caption reads *"When {key} is held:"* and follows your current PTT binding.

---

## Chapter 7 · Settings and personalization

Open **Settings** from the left rail. All choices persist across restarts.

### 7.1 Audio devices
- **Input Microphone** — your real mic.
- **Output (VB-Cable recommended)** — where the changed voice goes.
- **↻ Refresh Devices** — re-scan after plugging things in.
- The status line shows **Virtual audio device detected** or a clickable
  **"Set up a virtual audio device →"** link.

### 7.2 Interface language
**Language** switches the whole UI between **English** and **中文** instantly.
On first run Vonvert follows your Windows display language (zh-\* → Chinese,
otherwise English). Your choice is saved.

### 7.3 Data location
**Data location** is where your **presets, settings and logs** are stored.
- **Change…** lets you pick a different folder.
- You are asked whether to **copy** your existing data to the new folder.
- A change **takes effect after you restart Vonvert**.
- By default this lives under `%APPDATA%\Vonvert`.

### 7.4 Tray and background
Closing the window sends Vonvert to the **system tray**; the engine keeps
running. Double-click the tray icon (or right-click → **Open**) to restore, and
right-click → **Quit** to exit.

### 7.5 About, version and updates
The **About** page shows version, runtime and dependency details, with a
**Copy all** button for support. When a new **major** version is announced, a
badge appears in the bottom bar; clicking it opens the release page
(see [Privacy and data](#privacy) for what this does over the network).

---

# Part III — Advanced voices

## Chapter 8 · Auto pitch (register normalization)

**Auto pitch** (top of the voice page) *"Normalizes pitch to a target register
using live F0 detection."* In plain terms, it keeps your output in a consistent
vocal range instead of drifting, which makes large pitch shifts (e.g. **Female**
or **Demon**) sound steadier. Turn it on when your voice jumps around; leave it
off if you want to keep every natural inflection.

---

## Chapter 9 · Understanding the voices (DSP effects)

Vonvert applies effects through **presets** — there is no per-effect slider
panel. Knowing what each effect does helps you choose or share presets.

### 9.1 The effects
| Effect | What it does |
|---|---|
| **Pitch** | Shifts the voice up/down in semitones. |
| **EQ** | Boosts or cuts bass / mids / treble. |
| **Reverb** | Adds room/space tail. |
| **Chorus** | Doubles and thickens the voice. |
| **Compressor** | Evens out loud and quiet levels. |
| **Gate** | Silences background when you are not speaking. |
| **Noise Reduction** | Removes steady background hiss/hum. |
| **Delay** | Adds echoes. |
| **De-esser** | Tames harsh "s" sounds. |
| **Robot** | Ring-modulated metallic voice. |
| **Drive / Distortion** | Adds grit and saturation. |

### 9.2 What each built-in preset uses
| Preset | Effect recipe |
|---|---|
| **Normal** | Gate + Compressor only (clean) |
| **Deep Male** | Pitch −4 · EQ (low +3, high −2) · Compressor |
| **Female** | Pitch +4 · EQ (cut low, boost presence/air) · Compressor · Chorus · De-esser |
| **Robot** | Robot · Pitch −2 · light Reverb |
| **Demon** | Pitch −9 · Distortion · Reverb · Gate |

### 9.3 Tuning workflow
Because presets are whole bundles, "tune" by **choosing** a preset, then use
**A/B compare** ([§5.2](#52-ab-compare-dry--ab--wet)) to hear exactly what a
preset adds: flip between **DRY** and **A/B** to judge the change, or **WET** to
isolate the coloration. Export a preset you like ([§3.3](#33-import-export-and-delete-presets))
to back it up or share it.

---

# Part IV — Support and reference

## Chapter 10 · Troubleshooting

| Symptom | What to check |
|---|---|
| No sound at all | Vonvert **Output** is set to `CABLE Input`; the **OUT** VU bar moves when you speak. |
| Others hear your raw voice | The app's **microphone** is `CABLE Output`, not your real mic ([§4.3](#43-per-app-settings)). |
| A/B stuck on "DRY" | The segmented control is on **DRY**; switch it back to **A/B**. |
| Voice change does nothing | The bottom **Voice Change** button shows **Running (Effects ON)**; if it says **Standby (Direct Monitor)**, click it. |
| Echo / feedback | Turn **off** *Hear Myself*, or use headphones. |
| High / choppy latency | Lower the buffer in your system audio settings; close heavy apps; check the **Latency** read-out. |
| Device missing in list | Click **↻ Refresh Devices**; check Windows **Settings → System → Sound**. |
| "No microphone detected" | Plug in a mic, then click **Refresh Devices**. |
| `CABLE Input/Output` missing | The virtual cable driver did not install — see [VB-CABLE setup guide](VB-CABLE.md) (run setup **as administrator** and reboot). |

---

## Chapter 11 · Reference

### 11.1 License and third-party components
- Vonvert (OSS) is licensed under the **Apache License 2.0** — see `LICENSE` and
  `NOTICE` in the repository.
- Virtual audio devices (VB-Cable, VoiceMeeter, VAC) are **separate third-party
  products** with their own licenses, not covered by Vonvert's license.

<a id="privacy"></a>
### 11.2 Privacy and data
- **Audio stays on your machine.** Vonvert processes your voice locally; it does
  **not** upload your voice.
- **What is stored locally:** your settings, chosen devices, language and any
  imported presets live in the **data location** (default `%APPDATA%\Vonvert`,
  changeable in [§7.3](#73-data-location)).
- **Network:** the only automatic network activity is a small, best-effort
  **update check** to the GitHub releases page to notify you of a new **major**
  version. It is periodic, failures are ignored silently, and no voice data is
  sent. Clicking an update badge, the tray links, or the in-app help links opens
  normal web pages in your browser.

### 11.3 Related documents
- **[README](../README.md)** — project overview, build and test.
- **[VB-CABLE setup guide](VB-CABLE.md)** — install the recommended virtual cable.
- **[Virtual audio devices comparison](VIRTUAL-AUDIO-DEVICES.md)** — choose a device.
- **简体中文用户指南** — [USER-GUIDE.zh.md](zh/USER-GUIDE.zh.md).
