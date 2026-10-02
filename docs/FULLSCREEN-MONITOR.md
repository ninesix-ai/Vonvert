<!--
TRANSLATOR NOTES (do not render to users; keep this HTML comment or translate it
for internal use only):
- Target audience: general Windows users (streamers, podcasters, content creators).
  Use short, direct sentences (subject-verb-object). Avoid idioms, slang and
  culture-specific metaphors.
- Do NOT translate: product names (Vonvert, OBS, VB-Cable, Discord, Zoom, Teams),
  company names (VB-Audio), file names (Vonvert.exe), key names (Esc, Ctrl, Alt),
  device names shown in Windows (CABLE Input, CABLE Output), and audio format names
  (LUFS, dBTP, Hz, ms, WASAPI).
- Keep UI labels exactly as the app shows them. The app shows plain words by default
  and the technical name underneath; the plain words are translated, so a translator
  must use the app's own translated labels, not the English ones.
- Keep the table structure, column order, and section numbers identical to the
  Chinese sibling.
-->

# Fullscreen monitor

> Language: English · [简体中文](zh/FULLSCREEN-MONITOR.zh.md)

Vonvert changes your voice while you speak. The **Fullscreen monitor** is a separate
window that shows what your voice is doing right now, as pictures. Use it to check your
sound before you go live, to watch your level while you record, or as a live visual on
your stream.

This guide explains what each picture means, how to drive the window, and how to put it
into OBS.

## 1. What the monitor is — and what it is not

**It is a dashboard for your voice.** Tachometer, fuel gauge and speedometer for the
thing you do with your mouth. It shows four measurements that come from the real audio
your computer is producing.

**It is not:**

| Not this | Why it matters |
|---|---|
| An editor | You cannot select, zoom, cut or fix audio here. Use an audio editor for that. |
| An alarm | Nothing pops up when a level is wrong. You read the pictures and decide. |
| A stream key or encoder | It does not broadcast. It only draws pictures on screen. |
| A sound source | The window carries **no audio**. See [§7 Using it in OBS](#7-using-it-in-obs). |

The small spectrum bar inside the main window stays as it is: a quick "am I audible"
glance. The monitor is the deeper view you open when you want detail.

## 2. Open it, move it, close it

**Open:** the four-arrows icon in the top-right of the main window (the tooltip says
"Fullscreen Monitor"). Clicking it again brings the monitor to the front instead of
opening a second copy.

**Move:** hold the left mouse button anywhere in the window and drag. There is no title
bar, so dragging the black area is how you reposition it.

**Close:** press `Esc`, or use the **Close** button.

**Show the buttons:** move the mouse. A small strip appears in the bottom-right corner.
Stop moving and it disappears after about 3 seconds, together with the mouse pointer, so
a captured picture stays clean.

| Button | What it does |
|---|---|
| **Close** | Closes the monitor. The main window keeps running. |
| **Always on top** | Turns stay-on-top on or off. Turn it off when the monitor covers something you need to click. |
| **Window size** | Cycles 1280×720 → 1920×1080 → fill the screen area. It never covers the taskbar. |
| **Reset view** | Puts the layout, size and always-on-top back to the defaults. |
| **Technical terms** / **Plain words** | Switches the panel titles between everyday words and audio-industry words. See [§3](#3-the-four-panels-in-plain-words). |
| **Help** | Opens a small card with the gestures and a short reading guide. |

The window is 1280x720 the first time you open it, and you can resize it from its edge.
After that it comes back the size and in the place you left it, still showing the same
voice, the same loudness target and the same wording - so an OBS layout survives to the
next stream. **Reset view** returns it to the defaults. If a stored position was on a
monitor that is no longer connected, the window opens in the middle of your main screen
instead of off-screen where you cannot reach it.

**None of this is a hidden gesture any more.** Each panel carries a small labelled chip:
click it, or double-click the panel, and that panel fills the whole window. While one panel
fills the window, the panels it hid do not vanish - they stay listed as a strip at the
bottom, and one click there brings them back, so you never have to know that a second
double-click exists. A single click on a panel itself never changes the layout, so you
cannot break your picture by accident mid-stream. Every time the window opens it also names
the three gestures - drag to move, double-click to enlarge, more buttons in the bottom-right
corner - for a few seconds, then fades out by itself.

## 3. The four panels in plain words

By default the window labels itself in plain words, with the technical name shown
underneath in smaller type. The **Technical terms** button swaps which one is the big
label.

| Panel title (plain) | Technical name | What it answers | How to read it |
|---|---|---|---|
| **Voice detail** | Spectrogram | Is there anything in my sound that should not be there? | Left is a few seconds ago, right is now. Down is low pitch, up is high pitch. Brighter means louder at that pitch. |
| **Voice shape** | Waveform | Did the voice changer change the shape of my sound? | The line swings up and down as you speak. A wide swing is a loud moment. |
| **Volume** | Loudness (LUFS) | Am I too quiet, or too loud? | One vertical meter with three marks. Watch the purple one. See [§6](#6-loudness-numbers-explained). |
| **Pitch badges** | cents / note | Am I in tune? | A note name, a number in cents, and a colour: green close, amber near, red off. |

Two things worth knowing:

- **The frequency axis is labelled, and it is not evenly spaced.** Bottom is low, top is
  high, the right edge is now. The shaded band marked "your voice lives here" is the part
  that matters; above it is mostly hiss, "s" sounds and room noise - which is exactly why
  the busiest-looking area of the picture is not the important one.
- **The horizontal window is about 4 seconds of sound**, not a minute.

### Voice detail: what the shapes mean

| You see | It usually is | You can |
|---|---|---|
| A bright band at the bottom that moves while you speak, everything else dark | A normal voice | Nothing, this is the healthy picture |
| A thin bright line at the top that stays when you are silent | Hum from power supply, fan, air conditioner | Move the microphone, switch the fan off, get closer |
| Bright spots at the top every time you say "s" or "t" | Harsh "s" sounds (sibilance) | Common after a voice change; fix it at the microphone or the room |
| Fine bright speckle over the whole picture while you are silent | Room noise recorded by the microphone | Add soft furnishing, close the window, get closer to the mic |
| A bright band that fades slowly after you stop speaking | Room echo (reverb tail) | Normal in an empty hard room; rugs and curtains reduce it |
| Extra stripes or a metallic look that the raw voice does not have | Artifacts of the voice change itself | Try another preset, or reduce the pitch shift |

### Voice shape: comparing before and after

Two buttons beside the panel choose what this panel draws:

| Button (plain) | Technical | What it is |
|---|---|---|
| **Your raw voice** | Dry | Your microphone, unchanged |
| **Changed voice** | Wet | What your listeners actually receive |

The buttons only change the picture. They never change your output: your audience hears
the changed voice either way. The line under the buttons always tells you which one you
are looking at.

Why compare: flip between the two and look at the shape. Similar shape with a shifted
pitch usually means the effect sounds natural. Flat tops on the wave (the line looks cut
off at the top and bottom) mean the signal is hitting its ceiling and will crackle —
lower your input level or move back from the microphone.

## 4. Start here: watch one number

You do not need to read four panels at once. Learn one rule:

> **The purple mark on the Volume meter should sit near the green line.**

- Purple mark clearly **below** the green line → you are quiet; listeners will raise
  their own volume.
- Purple mark clearly **above** the green line, and the number **TP** close to 0 → you
  risk distortion. Move back from the microphone or lower the input level.
- The **yellow** mark is the average of the whole session. Look at it only when you
  finish a recording, and reset it when you start a new one.
- The **cyan** mark moves every fraction of a second. Ignore it; watching it makes you
  nervous for no benefit.

Note: the green line marks a **target**, and the button under the readings changes which
one - **broadcast -23**, **streaming -14**, **podcast -16**, or none at all. Pick the one
that matches where your audio is going. The line carries its own number, so you can always
see what you are aiming at.

## 5. Quick checks before you go live

| Check | Green | Amber | Red |
|---|---|---|---|
| **Level** (purple mark, `TP`) | Purple within 3 of the line; `TP` at -2 or lower | 3–6 away; `TP` near -1 | More than 6 away, or `TP` at 0 or above |
| **Cleanliness** (Voice detail) | Picture is dark while you are silent | One thin line at the top when silent | Speckle everywhere when silent |
| **Naturalness** (compare the two voices) | Same shape, shifted pitch | Extra hiss or metallic edge on the changed voice | Wave tops cut flat |
| **Tuning** (badge colour) | Mostly green | Mostly amber | Red stays on screen |

One line to remember: **dark = silence, bright = you speaking, colour = good or not.**

## 6. Loudness numbers explained

Volume in this window is measured in **LUFS**. Your listeners judge how loud something
is by its average, not by its peaks, and LUFS is the standard way to measure that
average. Streaming platforms use the same measurement to level everything they host.

| On the meter | Meaning | Use it for |
|---|---|---|
| Range -60 at the bottom to 0 at the top | Louder is higher. All the numbers are negative, which surprises people. | Remember "up = loud" |
| **Cyan mark** (Momentary) | Roughly the last 0.4 seconds | Seeing how jumpy your level is |
| **Purple mark** (Short-term) | Roughly the last 3 seconds | The number you actually steer with |
| **Yellow mark** (Integrated) | The average of the whole session; it only ever accumulates | The number you compare between episodes |
| Green line, labelled **-23** | Broadcast target (EBU R128) | A reference, not necessarily your platform's target |
| `TP -x dBTP` | True peak: how close you are to clipping. 0 or above cracks. | Keep it at -1 or below when you are loudest |
| `Integrated -x LUFS` | The yellow mark as a number | Write it down before you publish |

The three marks are lettered **M**, **S** and **I** on the meter so you never have to guess
which is which, and the scale shows its numbers (louder is higher, and every figure is
negative). The `TP` line turns amber as you approach the ceiling and red once you cross it,
and says so in words rather than in colour alone.

**Why this matters:** if your recording is too loud, the platform turns it down and your
voice ends up small and distant in the listener's ears. If it is too quiet, some
platforms leave it there. Once audio is published and re-encoded you cannot undo this, so
watching the level while you record is what saves you a second take.

**Resetting the session average:** the button under the number restarts the count, so you
can measure one segment at a time. Right-clicking the number does the same thing.

## 7. Using it in OBS

**What OBS is:** OBS Studio is a free program that composes your live picture — game,
camera, images, and other program windows — and sends it to your streaming platform.

**Why capture this window:** the pictures come from your real audio, so what your
audience sees is genuinely what you sound like. It costs nothing, needs no subscription,
and the preset name that fades in when you switch voices works as a free on-air caption.

**Set it up:**

1. Open the monitor in Vonvert.
2. In OBS, add a source: **+ → Window Capture**.
3. Pick the Vonvert window in the list.
4. Drag the red handles to fill your canvas, or right-click the source → Transform →
   Fit to screen.
5. Let the mouse rest for 3 seconds so the pointer and the strip disappear from the
   captured picture.

**Two ways to use it:** fill the whole canvas (radio or talk shows), or keep it small in
a corner while you play games.

**Audio does not come with it.** Window Capture takes pixels only. Your changed voice
reaches OBS through a virtual audio cable, which is a separate step: add
**Audio Input Capture** and select `CABLE Output (VB-Audio Virtual Cable)`. If your
stream has pictures but no sound, this is the reason. See the
[virtual audio device guide](VB-CABLE.md).

**Picture sharpness:** the monitor opens at 1280×720. If your OBS canvas is 1920×1080,
that is upscaled and looks soft. Use the **Window size** button to select 1920×1080, or
set the canvas to 720p.

**If OBS shows a black or empty capture:** in the Window Capture properties, change
**Method** to "Windows 10 (1903 or later)".

## 8. First-run walkthrough and the help card

The first time you open the monitor it walks you through four stops — what the big
picture is, which mark is your volume, how to compare raw and changed voice, and where
the buttons are. Click anywhere to move to the next stop; **Skip** ends it. It does not
appear again.

The **Help** button in the corner strip always brings the same information back in one
small card: the gestures, and the one number to watch.

To get the walkthrough again after finishing it, delete the file `fs_monitor_tour_done`
in the Vonvert data folder (default
`%APPDATA%\ninesix-ai\Vonvert`).

## 9. Mouse and keyboard

| Action | How |
|---|---|
| Move the window | Hold and drag anywhere |
| Close | `Esc`, or the **Close** button |
| Fill the window with one panel | Click the chip on the panel, or double-click the panel; the strip at the bottom brings the hidden ones back |
| Compare raw and changed voice | The two buttons under the Voice shape panel |
| Resize | Drag the window edge |
| Forget the layout you made | **Reset view** in the corner strip |
| Keep the picture clean | Stop moving the mouse for 3 seconds |
| Help card | `H` or `F1` |
| Always on top | `T` |
| Window size | `S` |
| Reset view | `R` |
| Raw or changed voice | `D` / `W` |
| Loudness target | `G` |
| Fill the window with one panel | `Enter` steps through voice detail, volume, voice shape, then back to the three-panel view |

The window is driven by keyboard alone: every panel and every control is named for a
screen reader, and `Tab` reaches the buttons. Keys are ignored while Ctrl, Alt or Shift is
held, so nothing here takes a shortcut away from Windows.

## 10. Messages you may see

A short banner appears at the top when the picture cannot tell the whole truth.

| Banner | What happened | What to do |
|---|---|---|
| "Voice changing is off — turn it on in the main window" | There is no processed audio to measure | Press the power button in the main window |
| "No sound from your microphone — check the input device and the virtual cable" | The engine runs but receives nothing | Check **Settings → Input Microphone**, and that the cable is installed |
| "A/B is on DRY: the meters show your raw voice" | A/B is set to **DRY**, so the effect chain is out and the meters follow your unprocessed voice | Switch A/B back to normal when you want the pictures to describe what the audience hears |
| "The monitor could not be opened. Click the icon again to retry." | Opening failed | Click the icon again; if it repeats, restart Vonvert |

## 11. Privacy and safety

- All audio analysis happens on your machine. The monitor sends nothing anywhere and
  collects no statistics.
- Anything the monitor shows about your voice is visible to whoever can see your stream
  or recording. Room noise shows up on the picture as clearly as it does in the audio.
- The preset-name caption shows the name of the current voice to your audience, so avoid
  names that contain private notes.

## 12. Read next

- [Vonvert user guide](USER-GUIDE.md) — installation, voices, routing, hotkeys.
- [Setting up a virtual audio device (VB-Cable)](VB-CABLE.md) — how to get your changed
  voice into other applications.
- [First-run guide & role presets](onboarding-rolesystem.md) — the setup wizard and role
  selection.
