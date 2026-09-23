# Setting up a virtual audio device (VB-Cable)

> Language: English · [简体中文](zh/VB-CABLE.zh.md)

Vonvert changes your voice in real time, but for **other apps to hear the changed
voice** — Discord, Zoom, Teams, OBS, your games — the processed audio has to be
routed back into them as a *microphone*. That is what a **virtual audio device**
(virtual audio cable) does. This guide uses **VB-Cable** from VB-Audio as the
recommended default, but **Vonvert works with any virtual audio device** — see
[§5 Other virtual audio devices](#5-other-virtual-audio-devices) for VoiceMeeter and VAC.

This guide walks you through downloading, installing and wiring a virtual audio
cable up with Vonvert. A virtual audio device is **optional but strongly
recommended**: without one you can still use the *Hear Myself* monitor, but chat
apps and games cannot pick up your changed voice as their input device.

> VB-Cable is a third-party, proprietary (free) driver from VB-Audio. It is **not
> bundled** with Vonvert and is **not installed automatically** — you install it
> once, manually. Because it is a system audio driver it requires administrator
> rights.

---

## 1. Download

1. Open the official page: <https://vb-audio.com/Cable/>
   - Tip: if no virtual audio device is detected, the **bottom status bar in Vonvert
     shows a clickable "Set up a virtual audio device →" link** — clicking it opens
     this page for you.
2. Scroll to **Download VB-Cable** and download the installer zip
   (`VB-CABLE_Driver.zip`).

## 2. Install

1. Unzip the download.
2. **Right-click `VBCABLE_Setup_x64.exe` → *Run as administrator***
   (64‑bit Windows; use `VBCABLE_Setup.exe` on 32‑bit).
   > The setup will not install without elevation — a normal double-click can
   > silently fail. Always run it as administrator.
3. Follow the prompts, then **reboot** when asked.
4. After the reboot, two new devices exist:
   - **`CABLE Input`** — a *playback* device. This is where Vonvert sends the
     processed voice.
   - **`CABLE Output`** — a *recording* device (appears as a microphone). This is
     what your chat app / game selects as its input.

## 3. Wire it up in Vonvert

1. Launch `Vonvert.exe`.
2. Open **Settings** (left sidebar) and set:
   - **Input Microphone** → your **real** microphone (e.g. your headset mic).
   - **Output** → **`CABLE Input (VB-Audio Virtual Cable)`**.
3. (Optional) Toggle **Hear Myself** if you want to monitor your own changed
   voice in headphones while you speak.
4. The bottom status bar should now report **VB-Cable detected**.

## 4. Point your chat app / game at the cable

In the voice/audio settings of the app you want to use your changed voice in
(Discord, Zoom, OBS, a game, …), set its **Input Device / Microphone** to:

> 👉 **`CABLE Output (VB-Audio Virtual Cable)`**

Speak — the other party now hears Vonvert's processed voice instead of your raw
microphone.

### Audio routing at a glance

```
Your mic ──► Vonvert (DSP: pitch / EQ / …) ──► CABLE Input
                                                      │  (virtual cable)
                                                      ▼
Their app ◄────────────────────────────────  CABLE Output  (seen as a microphone)
```

## 5. Other virtual audio devices

VB-Cable is the recommended default, but Vonvert works with **any** WASAPI virtual
audio device — just pick the one you have installed in **Settings → Output**. For a
full comparison of the options, see the **[Virtual audio devices chooser's
guide](VIRTUAL-AUDIO-DEVICES.md)**.

- **VoiceMeeter** (Basic / Banana / Potato) — a full virtual mixer with built-in
  buses (B1, B2, …). Set **VoiceMeeter Input** as Vonvert's output, then route
  **VoiceMeeter B1 Out** to your chat app's microphone. Best when you also mix BGM,
  run several mics, or need ASIO.
- **VAC (Virtual Audio Cable)** — a paid, pro-grade tool that creates many
  independent cables with custom sample rates and a rule-based routing engine. Best
  for multi-stream / studio setups.

> The bottom status bar turns green for **any** detected virtual device whose name
> contains "CABLE" (VB-Cable, VAC) or "VoiceMeeter" — you do **not** have to use
> VB-Cable specifically. With a non‑VB‑Cable device, select it manually in
> **Settings → Output** after installing it.

## 6. Troubleshooting

| Symptom | Fix |
|---|---|
| Status bar keeps prompting "Set up a virtual audio device →" | Make sure you installed as **administrator** and **rebooted**; click **Refresh** in Vonvert's device settings to re-enumerate. |
| Others hear your raw voice | Confirm the app's **microphone** is set to `CABLE Output`, not your real mic. |
| No sound / processing at all | Confirm Vonvert's **Output** is set to `CABLE Input`. |
| Echo / feedback | Turn **off** *Hear Myself*, or use headphones instead of speakers. |
| `CABLE Input/Output` missing in Windows | The driver did not install — re-run `VBCABLE_Setup_x64.exe` **as administrator** and reboot. |

## 7. Uninstall

Remove **VB-Cable** from *Windows Settings → Apps* (or run the uninstaller from
the VB-Audio download) and reboot. Vonvert keeps working without it — you just
lose the ability to route the changed voice into other apps.

---

**Official download page:** <https://vb-audio.com/Cable/>

Want a different virtual audio device? See the **[Virtual audio devices chooser's
guide](VIRTUAL-AUDIO-DEVICES.md)**.

VB-Cable © VB-Audio Software. It is a separate, proprietary (free) product and is
governed by its own license, not by Vonvert's Apache-2.0 license.
