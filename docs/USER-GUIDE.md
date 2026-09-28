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
**Part I → Chapter 3 (Quick start)** and skip the rest.

- Vonvert changes your voice **while you speak**, and can send that changed
  voice into any other app (chat, meeting, game, stream).
- Audio is processed **on your own computer**. See [Chapter 15 · Privacy](#privacy).

## Contents

**Part I — Getting started**
- [1. Know and install Vonvert](#chapter-1--know-and-install-vonvert)
- [2. First launch: setup wizard and roles](#chapter-2--first-launch-setup-wizard-and-roles)
- [3. Quick start (5 minutes)](#chapter-3--quick-start-5-minutes)

**Part II — Everyday use**
- [4. Voice presets](#chapter-4--voice-presets)
- [5. Audio routing](#chapter-5--audio-routing-getting-the-voice-into-other-apps)
- [6. Monitoring, compare and live meters](#chapter-6--monitoring-compare-and-live-meters)
- [7. Hotkeys and push-to-talk](#chapter-7--hotkeys-and-push-to-talk)
- [8. Soundboard](#chapter-8--soundboard)
- [9. Recording and export](#chapter-9--recording-and-export)
- [10. Settings and personalization](#chapter-10--settings-and-personalization)

**Part III — Advanced voices**
- [11. Expert parameter panel](#chapter-11--expert-parameter-panel)
- [12. Auto pitch (register normalization)](#chapter-12--auto-pitch-register-normalization)
- [13. Understanding the voices (DSP effects)](#chapter-13--understanding-the-voices-dsp-effects)

**Part IV — Support and reference**
- [14. Troubleshooting](#chapter-14--troubleshooting)
- [15. Reference](#chapter-15--reference)

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
The virtual cable is optional but strongly recommended — see [Chapter 5](#chapter-5--audio-routing-getting-the-voice-into-other-apps).

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
- A microphone and headphones/speakers (headphones reduce echo — see [Chapter 14](#chapter-14--troubleshooting)).
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

## Chapter 2 · First launch: setup wizard and roles

The first time Vonvert starts it shows a short setup wizard, then a spotlight
tour. Both run **once**: Vonvert writes a marker file named `onboarding_done`
into its [data location](#103-data-location), and skips the wizard whenever
that file exists.

### 2.1 The setup wizard

Four steps, with **Skip** available at any point:

| Step | Title | What you do |
|---|---|---|
| 1 | **Welcome to Vonvert** | Nothing — an intro screen. Press **Next**. |
| 2 | **How will you use Vonvert?** | Pick a role (see §2.3), or **No role (show everything)**. |
| 3 | **Choose your devices** | Select your microphone and the output (e.g. VB-Cable). |
| 4 | **You're all set** | Press **Get started** to close the wizard. |

The footer shows progress as **Step 1 of 4**, and **Back** returns one step.

### 2.2 The spotlight tour

Immediately after the wizard, a **Quick tour** dims the window and highlights
four areas in turn. Read each caption, click **Next**, finish with **Finish**:

1. *"Click any voice to change yours — hover to preview."*
2. *"This power button turns voice changing on or off."*
3. *"Selecting a voice opens this panel to fine-tune pitch, EQ and more."*
4. *"Choose your microphone and output (e.g. VB-Cable) here."*

### 2.3 Roles

A role tailors which controls you see and which voices are suggested. Eight
roles are offered, grouped under **Fun & gaming**, **Content creation** and
**Professional audio**:

| Role | Group | Pitched as | Default detail level |
|---|---|---|---|
| **Beginner** | Fun & gaming | Just here to try things out. | Minimal |
| **Gamer** | Fun & gaming | Fun voices for games and Discord. | Standard |
| **Streamer** | Content creation | Live streaming and on-air voice changes. | Standard |
| **Creator** | Content creation | Recording and content production. | Standard |
| **Voice actor** | Content creation | Character and dubbing work. | Advanced |
| **Social voice** | Fun & gaming | Playful voices for chat apps. | Minimal |
| **Audio engineer** | Professional audio | Full control over every parameter. | Professional |
| **AI enthusiast** | Professional audio | Experimenting with AI voice tools. | Professional |

The four detail levels are **Minimal**, **Standard**, **Advanced** and
**Professional**. Your role's default is only a starting point — change it any
time in **Settings → My role** ([§10.6](#106-my-role)).

### 2.4 Running the wizard again

There is **no "replay setup" button** in Settings. To see the wizard and tour
again, quit Vonvert and delete the marker file:

```
%APPDATA%\ninesix-ai\Vonvert\onboarding_done
```

Restart Vonvert and the wizard appears. (If you moved your data location, the
file is in that folder instead.)

---

## Chapter 3 · Quick start (5 minutes)

### 3.1 First launch and the window
The window has three areas:
- **Left icon rail** — switch between **Voice Change**, **Settings**, **About**.
- **Center content** — the voice picker and the live visualizers.
- **Bottom bar** — brand + status, the big **Voice Change** on/off button, and the
  input/output **VU** meters with a **Latency** read-out.

The header (top-right) has the usual **Minimize / Maximize / Close** buttons.
Clicking **Close** minimizes Vonvert to the **system tray** instead of quitting
(right-click the tray icon → **Quit** to exit fully).

### 3.2 Three steps to hear it
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

### 3.3 Self-check
- The **IN** bar should move when you speak → your mic is captured.
- The **OUT** bar should move → audio is being produced.
- If others still hear your raw voice, see [Chapter 5](#chapter-5--audio-routing-getting-the-voice-into-other-apps).

---

# Part II — Everyday use

## Chapter 4 · Voice presets

### 4.1 Built-in presets
Vonvert ships with eight voices. Click a tile to apply it instantly; a toast
confirms *"Preset applied: …"*.

| Preset (EN) | 中文 | Icon | Character |
|---|---|---|---|
| Normal | 原声 | 🎙️ | Clean pass-through (gate + compressor only) |
| Deep Male | 低沉男声 | 🎺 | Lower pitch, warmer low end |
| Female | 女声 | 🌸 | Higher pitch, brighter, de-essed (applied on launch) |
| Robot | 机器人 | 🤖 | Ring-modulated robotic tone |
| Demon | 恶魔 | 😈 | Very low pitch with distortion and reverb |
| Android | 仿生人 | 🦾 | Metallic ring-modulated voice |
| Radio Ghost | 电台残响 | 📻 | Band-limited radio voice with tail |
| Tape Wobble | 磁带摇曳 | 📼 | Pitch-drifting lo-fi tape character |

> Built-in presets are **read-only** — they cannot be deleted or overwritten.

### 4.2 Select and switch
- **Click** a preset tile to apply it.
- Press **F11** (default) to cycle to the **next preset** without touching the mouse.
- To understand what each preset changes inside, see [Chapter 13](#chapter-13--understanding-the-voices-dsp-effects).

### 4.3 Import, export and delete presets
The voice picker has two buttons under the tiles: **Import** and **Export**.
- **Export** writes the currently selected preset to a `Vonvert preset (*.vopreset)`
  file, so you can share it or keep a backup.
- **Import** loads a shared `.vopreset` file and adds it to your picker.
- To **delete** a preset you added, **right-click its tile** → **Delete** and
  confirm. Built-in presets cannot be deleted.
- If the file is not a valid preset, you get *"Import failed — not a valid
  preset file"*.

---

## Chapter 5 · Audio routing (getting the voice into other apps)

### 5.1 The principle
Vonvert writes the changed voice to an **output** device. For another app to
hear it, that output must be a **virtual cable's Input**, and the app's
microphone must be the same cable's **Output**.

### 5.2 Choosing a device
- **VB-Cable (recommended)** — one cable, simplest. See
  **[VB-CABLE setup guide](VB-CABLE.md)**.
- **VoiceMeeter** — a full virtual mixer, good when you also mix music or run
  several mics. See **[comparison guide](VIRTUAL-AUDIO-DEVICES.md)**.
- **VAC / others** — many independent cables, pro routing. Also in the
  **[comparison guide](VIRTUAL-AUDIO-DEVICES.md)**.

> Vonvert auto-detects any active output whose name contains **CABLE** or
> **VoiceMeeter**; otherwise pick it manually in **Settings → Output**.

### 5.3 Per-app settings
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

## Chapter 6 · Monitoring, compare and live meters

### 6.1 Hear Myself (monitor your own voice)
**Settings → Hear Myself** toggles a local monitor so you can hear your own
**changed** voice in your headphones while you speak.
- **On** = you hear the processed voice locally.
- **Off** (default) = only the output cable carries it; you hear nothing locally.
- If you get echo or feedback, turn it **off** or use headphones.

> **Hear Myself** does not change *what* is produced — it only decides whether
> that audio is also sent back to your headphones. It works together with the
> A/B control below.

### 6.2 A/B compare (DRY / A/B / WET)
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

### 6.3 Live visualizers
While the engine runs, the voice page shows:
- **SPECTRUM** — a real-time frequency spectrum, with the detected **note** and
  **cents** deviation shown on the right.
- **PITCH** — a short pitch curve over time.
- **IN / OUT** VU bars and a **Latency: … ms** read-out in the bottom bar.

These are read-only feedback; nothing to configure.

---

## Chapter 7 · Hotkeys and push-to-talk

### 7.1 Default hotkeys
Global hotkeys work even when Vonvert is not focused.

| Action | Default key | In-app label |
|---|---|---|
| Turn voice On / Off | **F9** | On / Off |
| Mute (silence output) | **F10** | Mute |
| Next preset | **F11** | Next Preset |
| Push to Talk | **F12** | Push to Talk |

### 7.2 Rebinding, conflicts and reset
In **Settings → HOTKEYS**: click a shortcut button, then press a new key
(a modifier combination or an F-key). If the key is already used, you see
*"Conflict: already bound to …"*. Use **↻ Reset to defaults** to restore F9–F12.

### 7.3 Push-to-Talk mode
**Settings → Push-to-Talk mode** chooses what the PTT key does:
- **Hold to talk** — normally muted; sound is sent **only while you hold** the key.
- **Hold to mute** — normally speaking; you go silent **only while you hold**.

The caption reads *"When {key} is held:"* and follows your current PTT binding.

---

## Chapter 8 · Soundboard

The **Soundboard** tab plays one-shot sounds — hits, stings, memes — on top of
your voice. It holds **50 built-in sounds** generated by the engine itself; no
third-party audio files are shipped.

### 8.1 The pads

Filter by category with the chips on the left; the categories and their sound
counts are **Drums** (9), **Tones** (5), **SFX** (13), **Memes** (5),
**Music** (6), **Retro** (5) and **Ambient** (7).

Each pad shows an icon, its name, its length, and a hotkey badge. Lengths are
written the way you hear them: under a second as whole milliseconds (`50ms`),
one to ten seconds with a single decimal (`1.2s`), ten seconds and up as whole
seconds (`30s`).

Click a pad to play it.

### 8.2 Audition and Live

Two chips at the top-right decide **who can hear a pad**:

| Mode | Who hears it |
|---|---|
| **Audition** | Only you, on your own speakers. |
| **Live** | You, **and** anyone on the other side of your call or stream. |

In Live mode the sound is mixed into the same output Vonvert sends to your
virtual audio device, so chat apps and games forward it along with your voice.
A permanent bar under the chips always states which mode you are in:

- *Audition: pads play only on your own speakers; others cannot hear them.*
- *Live: pads are mixed into the output sent to your virtual audio device, so
  others hear them in calls and streams.*
- *Live is selected, but the voice engine is stopped — nobody hears anything
  right now.*

That third line wins whenever the engine is off: Live mode needs a running
engine, because the broadcast path is the engine's output. **Audition uses its
own playback channel and works even while the engine is stopped**, so you can
preview sounds before going on air.

When you are in Live mode the bar also offers **Switch to Audition** — a
one-click escape if the broadcast was accidental.

> Pads are mixed **after** the voice effects chain, so they are never
> pitch-shifted. A kick drum stays a kick drum whatever voice you are using.

**Volume** (top-right slider) scales every pad; it is separate from your
microphone gain.

### 8.3 Your own sounds

Press **Import** to add audio files. Supported: `.wav`, `.mp3`, `.ogg`, `.m4a`.
Imported sounds appear under the **Imported** category. To remove one, click the
**✕** at the corner of its pad and confirm (**Remove**).

### 8.4 Hotkeys per pad

Click a pad's hotkey badge (**Assign hotkey**), then press a key combination —
the caption reads *"Press a key combination to bind this sound."* The binding
works globally, like the other [hotkeys](#chapter-7--hotkeys-and-push-to-talk),
so you can fire a sound from another app. **Right-click** the pad to clear it
(**Clear hotkey**).

---

## Chapter 9 · Recording and export

The **Recording** tab captures audio to your computer so you can keep or share
it later.

### 9.1 What gets recorded

Choose **Mode:** before you start:

| Mode | Captured audio |
|---|---|
| **Original** | Your microphone, untouched. |
| **Processed** | Your voice after Vonvert's effects — what others hear. |

### 9.2 Recording

Press **● Start Recording**. The first time, Vonvert asks for confirmation:

> **Recording notice**
> Recording saves audio on your computer. Only record audio you have the right
> to save. Continue?

While recording, the button becomes **■ Stop** and **Cancel** discards the take
in progress. Press **■ Stop** to finish; the take is saved and a
*Recording saved* notice appears.

Recordings are stored inside Vonvert's
[data location](#103-data-location), not beside the program.

### 9.3 The history list

Saved takes appear under **Recording History**, each with its length and size.
You can:

- **Search recordings…** by file name.
- Filter by date — **Today**, **7 days**, **30 days**.
- Filter by length — **< 1 min**, **1–5 min**, **5–15 min**, **> 15 min**.
- Read the summary line, e.g. *{n} recordings • total {time} • {size}*.

Tick the checkbox on several rows and press **Delete selected** to remove them
in one go; Vonvert asks *Delete the N selected recordings? This cannot be
undone.* and reports *Deleted*. Doing it with nothing selected answers
*No items selected*.

### 9.4 Export

**Export** on a row opens **Export Recording**, where you choose a destination
and a format:

| Format | Character |
|---|---|
| **WAV** | Uncompressed; largest, exact. |
| **MP3** | Small, plays everywhere. |
| **FLAC** | Compressed but lossless. |
| **OGG** | Small, open codec. |
| **AAC / M4A** | Small, Apple-family friendly. |

You can also pick **Mono** or **Stereo**, and turn on **Use variable bitrate
(VBR)** for the lossy formats. A successful run reports *Export completed*; a
failure reports *Export failed*.

> Export runs the take through the **same limiter chain** the live path uses, so
> an exported file is not louder than what your listeners actually heard.

### 9.5 Deleting

**Delete** on a single row asks *Delete this recording? This cannot be undone.*
before removing it.

---

## Chapter 10 · Settings and personalization

Open **Settings** from the left rail. All choices persist across restarts.

### 10.1 Audio devices
- **Input Microphone** — your real mic.
- **Output (VB-Cable recommended)** — where the changed voice goes.
- **↻ Refresh Devices** — re-scan after plugging things in.
- The status line shows **Virtual audio device detected** or a clickable
  **"Set up a virtual audio device →"** link.

### 10.2 Interface language
**Language** switches the whole UI between **English** and **中文** instantly.
On first run Vonvert follows your Windows display language (zh-\* → Chinese,
otherwise English). Your choice is saved.

### 10.3 Data location
**Data location** is where your **presets, settings and logs** are stored.
- **Change…** lets you pick a different folder.
- You are asked whether to **copy** your existing data to the new folder.
- A change **takes effect after you restart Vonvert**.
- By default this lives under `%APPDATA%\ninesix-ai\Vonvert` (publisher
  namespaced, so it never collides with other Vonvert editions). You can point
  it elsewhere with **Change…** above.

### 10.4 Tray and background
Closing the window sends Vonvert to the **system tray**; the engine keeps
running. Double-click the tray icon (or right-click → **Open**) to restore, and
right-click → **Quit** to exit.

### 10.5 About, version and updates
The **About** page shows version, runtime and dependency details, with a
**Copy all** button for support. When a new **major** version is announced, a
badge appears in the bottom bar; clicking it opens the release page
(see [Privacy and data](#privacy) for what this does over the network).

### 10.6 My role

**Settings → My role** shows the role you chose in the
[setup wizard](#chapter-2--first-launch-setup-wizard-and-roles) and lets you
change it later. Your role drives two things:

- **Detail level** — one of **Minimal**, **Standard**, **Advanced**,
  **Professional**; it decides how many controls the interface exposes.
- **Suggested voices** — the built-in presets this role recommends.

Pick **No role (show everything)** to stop Vonvert tailoring anything and see
every control regardless of complexity.

> Changing your role does not delete presets or recordings; it only changes
> which controls are shown and which voices are suggested.

---

# Part III — Advanced voices

## Chapter 11 · Expert parameter panel

Selecting a voice opens a panel on the right of the voice page that exposes the
individual parameters behind it — pitch offset, EQ bands, compressor threshold,
reverb size and so on.

### 11.1 Simple and Professional

A dropdown at the top of the panel chooses how much you see:

| Mode | What it shows |
|---|---|
| **Simple** | Only the groups that matter for everyday use. |
| **Professional** | Every group the engine supports. |

The list is generated from the same catalogue that defines the effects, so a
parameter appears here as soon as the engine knows about it.

### 11.2 Saving what you tuned

Changes apply immediately and affect the running audio.

- **Save as…** keeps your tweaks as a **new** preset you can pick later or
  export from [§4.3](#43-import-export-and-delete-presets). Built-in presets are
  never overwritten.
- **Reset to preset** throws away your tweaks and restores the selected preset's
  original values.

### 11.3 How this relates to presets

A preset is a whole bundle of effect settings; this panel edits the bundle you
currently have loaded. To hear what a preset changes as a unit, use
[A/B compare](#62-ab-compare-dry--ab--wet). To understand what each control
does, see [Chapter 13](#chapter-13--understanding-the-voices-dsp-effects).

---

## Chapter 12 · Auto pitch (register normalization)

**Auto pitch** (top of the voice page) *"Normalizes pitch to a target register
using live F0 detection."* In plain terms, it keeps your output in a consistent
vocal range instead of drifting, which makes large pitch shifts (e.g. **Female**
or **Demon**) sound steadier. Turn it on when your voice jumps around; leave it
off if you want to keep every natural inflection.

---

## Chapter 13 · Understanding the voices (DSP effects)

Vonvert applies effects through **presets** — bundled recipes you pick rather
than build. Each preset can still be fine-tuned per effect in the
[expert parameter panel](#chapter-11--expert-parameter-panel); knowing what each
effect does helps you choose, tune or share presets.

### 13.1 The effects
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
| **Ring Mod** | Multiplies the voice by a sine wave; metallic, synthetic tone. |
| **Lo-Fi Reverb** | Crushed, down-sampled room with a short tail (radio / tape damage). |
| **Modulation Delay** | Delay whose time is swept, giving pitch drift / chorus-like wobble. |
| **Tilt EQ** | Slopes the whole spectrum down at bass, up at treble (or reverse). |
| **Graphic EQ** | Multi-band manual EQ. |
| **Bitcrusher** | Digital quantisation noise; deliberate lo-fi grit. |
| **Loudness Meter** | Measures output level; no audible change. |

### 13.2 What each built-in preset uses
| Preset | Effect recipe |
|---|---|
| **Normal** | Gate + Compressor only (clean) |
| **Deep Male** | Pitch −4 · EQ (low +3, high −2) · Compressor |
| **Female** | Pitch +4 · EQ (cut low, boost presence/air) · Compressor · Chorus · De-esser |
| **Robot** | Robot · Pitch −2 · light Reverb |
| **Demon** | Pitch −9 · Distortion · Reverb · Gate |
| **Android** | Ring Mod (carrier 120 Hz, mix 0.6) · Gate · Compressor |
| **Radio Ghost** | Lo-Fi Reverb (room 0.7 · decay 0.7 · downsample 8 · bit depth 5 · mix 0.5) · Gate · Compressor |
| **Tape Wobble** | Modulation Delay (base 18 ms · depth 0.7 · feedback 0.35 · mix 0.45) · Gate · Compressor |

### 13.3 Tuning workflow
A preset is a whole bundle, so start by **choosing** one. To go further, open
the [expert parameter panel](#chapter-11--expert-parameter-panel) and adjust the
individual parameters, then **Save as…** to keep the result as your own preset.
Use **A/B compare** ([§6.2](#62-ab-compare-dry--ab--wet)) to hear exactly what a
preset adds: flip between **DRY** and **A/B** to judge the change, or **WET** to
isolate the coloration. Export a preset you like ([§4.3](#43-import-export-and-delete-presets))
to back it up or share it.

---

# Part IV — Support and reference

## Chapter 14 · Troubleshooting

| Symptom | What to check |
|---|---|
| No sound at all | Vonvert **Output** is set to `CABLE Input`; the **OUT** VU bar moves when you speak. |
| Others hear your raw voice | The app's **microphone** is `CABLE Output`, not your real mic ([§5.3](#53-per-app-settings)). |
| A/B stuck on "DRY" | The segmented control is on **DRY**; switch it back to **A/B**. |
| Voice change does nothing | The bottom **Voice Change** button shows **Running (Effects ON)**; if it says **Standby (Direct Monitor)**, click it. |
| Echo / feedback | Turn **off** *Hear Myself*, or use headphones. |
| High / choppy latency | Lower the buffer in your system audio settings; close heavy apps; check the **Latency** read-out. |
| Device missing in list | Click **↻ Refresh Devices**; check Windows **Settings → System → Sound**. |
| "No microphone detected" | Plug in a mic, then click **Refresh Devices**. |
| `CABLE Input/Output` missing | The virtual cable driver did not install — see [VB-CABLE setup guide](VB-CABLE.md) (run setup **as administrator** and reboot). |

---

## Chapter 15 · Reference

### 15.1 License and third-party components
- Vonvert (OSS) is licensed under the **Apache License 2.0** — see `LICENSE` and
  `NOTICE` in the repository.
- Virtual audio devices (VB-Cable, VoiceMeeter, VAC) are **separate third-party
  products** with their own licenses, not covered by Vonvert's license.

<a id="privacy"></a>
### 15.2 Privacy and data
- **Audio stays on your machine.** Vonvert processes your voice locally; it does
  **not** upload your voice.
- **What is stored locally:** your settings, chosen devices, language and any
  imported presets live in the **data location** (default
  `%APPDATA%\ninesix-ai\Vonvert`, changeable in [§10.3](#103-data-location)).
- **Network:** the only automatic network activity is a small, best-effort
  **update check** to the GitHub releases page to notify you of a new **major**
  version. It is periodic, failures are ignored silently, and no voice data is
  sent. Clicking an update badge, the tray links, or the in-app help links opens
  normal web pages in your browser.

### 15.3 Related documents
- **[README](../README.md)** — project overview, build and test.
- **[VB-CABLE setup guide](VB-CABLE.md)** — install the recommended virtual cable.
- **[Virtual audio devices comparison](VIRTUAL-AUDIO-DEVICES.md)** — choose a device.
- **简体中文用户指南** — [USER-GUIDE.zh.md](zh/USER-GUIDE.zh.md).
