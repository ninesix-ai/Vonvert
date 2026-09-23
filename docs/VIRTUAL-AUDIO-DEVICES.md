<!--
TRANSLATOR NOTES (do not render to users; keep this HTML comment or translate it
for internal use only):
- Target audience: general Windows users. Use short, direct sentences
  (subject-verb-object). Avoid idioms, slang and culture-specific metaphors.
- Do NOT translate: product names (VB-Cable, VoiceMeeter, Virtual Audio Cable),
  company names (VB-Audio), file names (Vonvert.exe, VB-CABLE.md), URLs,
  device names shown in Windows (CABLE Input, VoiceMeeter B1 Out), and format
  names (WASAPI, ASIO, MME, KS, kHz, bit).
- Keep ONE consistent translation per term across the whole document; see the
  Terminology table in section 2 and reuse those exact words everywhere.
- Keep the table structure and column order identical. Keep section numbers.
- ✓ = yes/supported, ✗ = no/not supported. These symbols stay as-is.
- Prices and technical specs can change. The wording already says "check the
  official site"; keep that disclaimer.
-->

# Virtual audio devices: full comparison

> Language: English · [简体中文](zh/VIRTUAL-AUDIO-DEVICES.zh.md)

This document compares the **virtual audio devices** that work with Vonvert on
Windows. It helps you choose the right one for your setup.

- If you only want the recommended default and a step-by-step installation,
  read **[Setting up a virtual audio device (VB-Cable)](VB-CABLE.md)** instead.
- Read this document if you want to compare **all** the options in detail.

> **About accuracy:** prices and technical details change over time. The values
> below are a general guide. Always confirm the current details on each product's
> official website.

---

## 1. Key terms

This guide uses the words below with these exact meanings.

| Term | Meaning |
|---|---|
| Virtual audio device | A software driver that creates a fake microphone and a fake speaker so that audio can move between programs. |
| Virtual audio cable | Another name for a virtual audio device. In this document the two terms mean the same thing. |
| Cable Input | The playback (speaker) end of a cable. Vonvert sends the changed voice here. |
| Cable Output | The recording (microphone) end of a cable. Your chat app or game reads from here. |
| Render device | A playback device (a "speaker") in Windows. |
| Capture device | A recording device (a "microphone") in Windows. |
| Bus | An output channel inside a mixer. |
| Sample rate | How many audio samples are taken each second. Measured in kHz. |
| Channel | One audio stream. 2 channels = stereo. |
| ASIO | A low-latency audio driver type, used mainly by music software. |
| WASAPI | The standard Windows audio system. Vonvert uses it. |
| Latency | The delay between sound entering and leaving the system. Lower is better. |
| Loopback | Sending audio that is being played back so it can be recorded elsewhere. |

---

## 2. How Vonvert uses a virtual audio device

Vonvert reads your microphone, changes your voice, and writes the result to an
**output** device. To make other apps hear the changed voice, that output must be
a **Cable Input**. You then set the same cable's **Cable Output** as the
microphone inside your chat app or game.

**Data path:**

```
Your microphone
      |
      v
  Vonvert (changes the voice)
      |
      v
  Cable Input  ---[ virtual cable ]---  Cable Output
                                              |
                                              v
                                    Your chat app / game (as its microphone)
```

**What Vonvert detects automatically:**
Vonvert looks for an active **render device** whose name contains the text
`CABLE` (this covers VB-Cable and Virtual Audio Cable) or `VoiceMeeter`. When it
finds one, the status bar shows "Virtual audio device detected". If it does not
find one, the status bar shows a clickable "Set up a virtual audio device" link.

Any virtual audio device works with Vonvert through **Settings → Output**, even
if the status bar does not detect it automatically. In that case, select the
device by hand.

---

## 3. Device categories

| Category | What it does | Devices |
|---|---|---|
| A. Single simple cable | One cable, no control panel. Easiest. | VB-Cable, LoopBe1, Hi-Fi Cable, Audio Repeater |
| B. Many cables + routing | Several independent cables and rule-based routing. | Virtual Audio Cable (VAC), LoopMux, Synchronous Audio Router |
| C. Virtual mixer | Mix several audio sources, then send to virtual buses. | VoiceMeeter, SteelSeries Sonar |
| D. Effects suite with a virtual device | Main purpose is audio effects; a virtual device is included. | NVIDIA Broadcast |
| E. Built into Windows | Limited loopback with no install. | Stereo Mix, WASAPI loopback (see section 7) |

---

## 4. Comparison table (all devices)

Legend: ✓ = yes / ✗ = no / – = not applicable.

| Device | Maker | Price | Category | Cables | Channels | Sample rate | ASIO | Mixer UI | Auto-detected by Vonvert | Setup effort |
|---|---|---|---|---|---|---|---|---|---|---|
| **VB-Cable** | VB-Audio | Free | A | 1 pair | 2 | 48 kHz fixed | ✗ | ✗ | ✓ | Low |
| **LoopBe1** | VB-Audio | Free | A | 1 pair | 2 | 48 kHz fixed | ✗ | ✗ | ✓ | Low |
| **Hi-Fi Cable** | Independent | Paid | A | 1 pair | 2 | Adjustable | ✗ | ✗ | ✓ | Low |
| **Audio Repeater / SAM** | Spacial Audio | Free | A | 1 or more | 2 | Adjustable | ✗ | ✗ | ✓ | Medium |
| **Virtual Audio Cable (VAC)** | E. Muzychenko | Paid (~$40) | B | Many (up to ~254) | up to 64 | 11–192 kHz | KS driver | ✗ | ✓ | Medium |
| **LoopMux** | VB-Audio | Free | B | Many | 2 | 48 kHz fixed | ✗ | ✗ | ✓ | Low–Medium |
| **Synchronous Audio Router (SAR)** | Open source | Free | B | Many (per-app) | 2 | 44.1/48 kHz | ✗ | ✗ | ✓ | Medium |
| **VoiceMeeter** (Basic / Banana / Potato) | VB-Audio | Free / donation / paid | C | Several buses | up to 8 | 44.1/48 kHz | ✓ (Banana/Potato) | ✓ | ✓ | High |
| **SteelSeries Sonar** | SteelSeries | Free | C | Several buses | 2 (7.1 virtual) | 48 kHz | ✗ | ✓ | ✓ | Medium |
| **NVIDIA Broadcast** | NVIDIA | Free | D | 1 mic + 1 speaker | 2 | 48 kHz | ✗ | Simple UI | ✗ | Low |

Notes:
- "Channels" shows the typical maximum, which can depend on version and settings.
- "Auto-detected by Vonvert" = ✓ means the status bar recognises it without extra
  steps. NVIDIA Broadcast shows ✗ only because its default device name does not
  contain `CABLE` or `VoiceMeeter`; you can still select it manually.

---

## 5. Detailed device profiles

Each profile uses the same labelled fields so you can compare them easily.

### 5.1 VB-Cable  (recommended default)

- **Type / category:** Single simple cable (A)
- **Maker:** VB-Audio
- **Price:** Free
- **How it works:** Creates `CABLE Input` (playback) and `CABLE Output`
  (recording). Audio written to the input appears at the output.
- **Channels:** 2 in / 2 out
- **Sample rate:** 48 kHz (fixed)
- **ASIO:** No
- **Mixer / effects:** No
- **Routing automation:** No
- **Setup difficulty:** Low — install, restart, done
- **Strengths:** Simplest option; free; supported by almost every guide; good for
  one voice-to-app connection
- **Limits:** Only one cable; fixed format; no mixing
- **Download:** <https://vb-audio.com/Cable/>

### 5.2 LoopBe1

- **Type / category:** Single simple cable (A)
- **Maker:** VB-Audio
- **Price:** Free
- **How it works:** One low-latency playback-to-recording loopback device. It is
  the older product that VB-Cable replaced.
- **Channels:** 2
- **Sample rate:** 48 kHz
- **ASIO:** No
- **Mixer / effects:** No
- **Routing automation:** No
- **Setup difficulty:** Low
- **Strengths:** Very small and simple
- **Limits:** Single playback-only loopback; no longer actively developed
- **Download:** <https://vb-audio.com/Cable/>

### 5.3 Hi-Fi Cable

- **Type / category:** Single simple cable (A)
- **Maker:** Independent (commercial)
- **Price:** Paid
- **How it works:** A single virtual cable that supports higher and adjustable
  audio quality.
- **Channels:** 2
- **Sample rate:** Adjustable
- **ASIO:** No (KS/WDM)
- **Mixer / effects:** No
- **Routing automation:** No
- **Setup difficulty:** Low
- **Strengths:** Better audio formats than VB-Cable
- **Limits:** Paid; still only one cable; similar niche to VAC but less flexible
- **Download:** Check the vendor site (search "Hi-Fi Cable")

### 5.4 Audio Repeater / SAM Virtual Audio

- **Type / category:** Single simple cable (A)
- **Maker:** Spacial Audio
- **Price:** Free
- **How it works:** Repeats one playback stream to a recording device. Aimed at
  broadcasting and streaming.
- **Channels:** 2
- **Sample rate:** Adjustable
- **ASIO:** No
- **Mixer / effects:** No
- **Routing automation:** No
- **Setup difficulty:** Medium (has its own control window)
- **Strengths:** Good for capture/streaming use
- **Limits:** Designed for broadcasting, not general routing
- **Download:** <https://www.spacialaudio.com/>

### 5.5 Virtual Audio Cable (VAC)

- **Type / category:** Many cables + routing (B)
- **Maker:** Eugene Muzychenko
- **Price:** Paid (about $40; a trial is available)
- **How it works:** Creates many independent virtual cables. A background
  Routing Engine applies rules to connect, rename, and duplicate streams between
  any devices.
- **Channels:** Up to 64 per device
- **Sample rate:** Wide, adjustable (about 11–192 kHz), plus selectable bit depth
- **ASIO:** No native ASIO, but uses a low-latency KS driver
- **Mixer / effects:** No mixing console (it is a routing tool)
- **Routing automation:** Yes — the strongest of any tool here
- **Setup difficulty:** Medium (powerful but technical)
- **Strengths:** Many isolated streams; custom formats; scripted routing
- **Limits:** Paid; plain interface; overkill for a single connection
- **Download:** <https://vacutesoftware.com/>

### 5.6 LoopMux

- **Type / category:** Many cables + routing (B)
- **Maker:** VB-Audio
- **Price:** Free
- **How it works:** Creates several lightweight virtual playback devices. Each one
  is a simple loopback; there is no mixing.
- **Channels:** 2 per device
- **Sample rate:** 48 kHz
- **ASIO:** No
- **Mixer / effects:** No
- **Routing automation:** Manual only
- **Setup difficulty:** Low to Medium
- **Strengths:** Several simple cables for free; good for splitting one source to
  many apps
- **Limits:** No mixing; manual routing
- **Download:** <https://vb-audio.com/Cable/>

### 5.7 Synchronous Audio Router (SAR)

- **Type / category:** Many cables + routing (B)
- **Maker:** Open source
- **Price:** Free
- **How it works:** Assigns a virtual input and output to each running
  application, so different apps can use different virtual devices.
- **Channels:** 2
- **Sample rate:** 44.1 / 48 kHz
- **ASIO:** No
- **Mixer / effects:** No
- **Routing automation:** Per-application rules
- **Setup difficulty:** Medium
- **Strengths:** Free; controls routing per app
- **Limits:** Not actively maintained; compatibility on newer Windows can be
  unreliable
- **Download:** Search "Synchronous Audio Router" on GitHub

### 5.8 VoiceMeeter (Basic / Banana / Potato)

- **Type / category:** Virtual mixer (C)
- **Maker:** VB-Audio
- **Price:** Basic is free; Banana uses a donation model; Potato is paid
- **How it works:** A mixer with faders. Virtual inputs
  (`VoiceMeeter Input`, `VoiceMeeter Aux Input`, …) receive audio. You mix it and
  send it to real hardware outputs and to virtual **buses** (`B1`, `B2`; Potato
  adds `B1`–`B5`) that appear as recording devices.
- **Channels:** Up to 8 on the buses
- **Sample rate:** 44.1 / 48 kHz (selectable)
- **ASIO:** Yes, on Banana and Potato (through the VB-Audio ASIO bridge)
- **Mixer / effects:** Yes — EQ, gate, compressor, reverb, delay
- **Routing automation:** Manual patch-style routing
- **Setup difficulty:** High — many concepts; wrong settings cause silence or echo
- **Strengths:** Combines your voice, music, and microphones in one place;
  handles monitoring and recording; no separate cable needed
- **Limits:** Steep learning curve; can be slow to set up correctly
- **Use with Vonvert:** Set Vonvert **Output → `VoiceMeeter Input`**, then use
  `VoiceMeeter B1 Out` as the microphone in your chat app or game.
- **Download:** <https://vb-audio.com/Voicemeeter/>

### 5.9 SteelSeries Sonar

- **Type / category:** Virtual mixer (C)
- **Maker:** SteelSeries
- **Price:** Free
- **How it works:** A virtual 7.1 mixer with named buses (Game, Chat, Media, Mic,
  and so on). Built for gaming and streaming.
- **Channels:** Stereo, with virtual 7.1 spatial features
- **Sample rate:** 48 kHz
- **ASIO:** No
- **Mixer / effects:** Yes — per-bus EQ and presets
- **Routing automation:** Manual
- **Setup difficulty:** Medium
- **Strengths:** Easy game/chat separation; polish features; free
- **Limits:** Part of the SteelSeries GG software; less flexible than VAC or
  VoiceMeeter
- **Download:** <https://steelseries.com/gg/sonar>

### 5.10 NVIDIA Broadcast

- **Type / category:** Effects suite with a virtual device (D)
- **Maker:** NVIDIA
- **Price:** Free (needs an NVIDIA graphics card)
- **How it works:** Main purpose is AI effects — noise removal, room-echo
  removal, and auto-framing video. It creates a virtual microphone and virtual
  speaker as a side effect.
- **Channels:** 2
- **Sample rate:** 48 kHz
- **ASIO:** No
- **Mixer / effects:** Effects, not a general mixer
- **Routing automation:** No
- **Setup difficulty:** Low
- **Strengths:** Excellent noise removal; useful as a clean-up stage in front of
  Vonvert
- **Limits:** Requires NVIDIA hardware; not a routing tool; default device name is
  not auto-detected by Vonvert (select it manually)
- **Download:** <https://www.nvidia.com/en-us/geforce/broadcasting/broadcast-app/>

---

## 6. How to choose

| Your goal | Recommended device |
|---|---|
| Send the changed voice into one chat app or game | **VB-Cable** |
| Mix your voice, background music, and microphones; stream and record | **VoiceMeeter Banana / Potato** |
| Many independent streams, custom formats, or rule-based routing | **Virtual Audio Cable (VAC)** |
| Split one source to several apps, for free | **LoopMux** |
| Clean up a noisy microphone first, change voice second | **NVIDIA Broadcast** + **VB-Cable** |
| Control routing per application, free, low stakes | **SAR** (check compatibility first) |

**Summary:** For most Vonvert users, **VB-Cable is the best default** because it
is free, simple, and widely documented. Choose **VoiceMeeter** when you need
mixing, and **VAC** when you need many cables or automation.

---

## 7. Built into Windows (limited)

These are not installable virtual sound cards, but they are sometimes mentioned:

- **Stereo Mix:** An old driver feature that records what the computer is playing.
  It is missing on many modern sound cards.
- **WASAPI loopback:** A programming method for capturing playback inside an app.
  It does **not** create a device you can select. Vonvert's *Hear Myself* monitor
  uses this kind of capture, not a virtual cable.

---

## 8. Important notes for any device

- **Administrator rights:** A virtual audio device is a system driver. Installing
  it needs administrator rights, and a **restart** is often required before the
  device appears.
- **Device not listed:** Click **Refresh** in Vonvert's device settings, then check
  **Windows Settings → System → Sound**.
- **Echo or feedback:** Use headphones, or turn off *Hear Myself*.
- **Licenses:** These are third-party products with their own licenses, separate
  from the Vonvert Apache-2.0 license.

---

## 9. Related

- Step-by-step setup of the recommended default: **[VB-CABLE.md](VB-CABLE.md)**
- Official pages: <https://vb-audio.com/> · <https://vacutesoftware.com/>
