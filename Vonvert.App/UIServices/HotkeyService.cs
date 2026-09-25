// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Vonvert.Engine;
using Vonvert.Engine.Soundboard;

namespace Vonvert.App.UIServices;

/// <summary>
/// Configurable Windows global hotkey service.
/// Supports dynamic re-binding, modifier-key combinations, and JSON persistence
/// to %APPDATA%/Vonvert/hotkeys.json.
///
/// Discrete actions (power / mute / cycle-preset) use RegisterHotKey + a WndProc
/// WM_HOTKEY dispatch. Push-to-Talk needs key-UP events, which RegisterHotKey does
/// not deliver, so it uses a low-level keyboard hook instead.
/// </summary>
public sealed class HotkeyService : IDisposable
{
    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const int WM_HOTKEY = 0x0312;
    private const int ID_BASE   = 9001;

    // ── Public types ────────────────────────────────────────────────────

    /// <summary>Actions that can be bound to a hotkey.</summary>
    public enum HotkeyAction { TogglePower, ToggleMute, CyclePreset, PushToTalk }

    /// <summary>A single key-binding (virtual key + modifier flags).</summary>
    public class HotkeyBinding
    {
        public int  VirtualKey { get; set; }
        public bool Ctrl       { get; set; }
        public bool Alt        { get; set; }
        public bool Shift      { get; set; }

        /// <summary>Deep copy.</summary>
        public HotkeyBinding Clone() => new() { VirtualKey = VirtualKey, Ctrl = Ctrl, Alt = Alt, Shift = Shift };

        /// <summary>Two bindings are equal if VK and all modifiers match.</summary>
        public bool Matches(HotkeyBinding other) =>
            other != null && VirtualKey == other.VirtualKey &&
            Ctrl == other.Ctrl && Alt == other.Alt && Shift == other.Shift;
    }

    // ── Low-level keyboard hook for Push-to-Talk ──────────────────────

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    private const int WH_KEYBOARD_LL     = 13;
    private const int WM_KEYDOWN          = 0x0100;
    private const int WM_KEYUP            = 0x0101;
    private const int WM_SYSKEYDOWN       = 0x0104;
    private const int WM_SYSKEYUP         = 0x0105;

    private IntPtr _hookId = IntPtr.Zero;
    private LowLevelKeyboardProc? _hookProc;
    private bool _pttKeyPressed = false;

    // ── State ───────────────────────────────────────────────────────────

    private IntPtr   _hwnd = IntPtr.Zero;
    private HwndSource? _source;
    private int      _nextId = ID_BASE;
    private readonly Dictionary<HotkeyAction, int> _actionToId = new();

    // ── Dynamic per-sound hotkey slots (soundboard) ──────────────────────
    // Registered on a separate id range so they never collide with the discrete
    // actions above. Pressing one raises <see cref="SoundHotkeyFired"/>.
    private const int DYN_ID_BASE = 20000;
    private int _nextDynId = DYN_ID_BASE;
    private readonly Dictionary<int, string> _idToSound = new();

    /// <summary>Fires on the UI thread when a registered soundboard hotkey is pressed, with the sound id.</summary>
    public event Action<string>? SoundHotkeyFired;

    /// <summary>Default bindings factory.</summary>
    public static Dictionary<HotkeyAction, HotkeyBinding> DefaultBindings => new()
    {
        [HotkeyAction.TogglePower]  = new() { VirtualKey = 0x78 }, // F9
        [HotkeyAction.ToggleMute]   = new() { VirtualKey = 0x79 }, // F10
        [HotkeyAction.CyclePreset]  = new() { VirtualKey = 0x7A }, // F11
        [HotkeyAction.PushToTalk]   = new() { VirtualKey = 0x7B }, // F12
    };

    /// <summary>Current bindings (mutable — call <see cref="SaveConfig"/> after changes).</summary>
    public Dictionary<HotkeyAction, HotkeyBinding> Bindings { get; private set; } = DefaultBindings;

    /// <summary>Fires on the UI thread when a registered hotkey is pressed.</summary>
    public event Action<HotkeyAction>? HotkeyPressed;

    /// <summary>Fires when the Push-to-Talk key state changes. true = pressed, false = released.</summary>
    public event Action<bool>? PushToTalkStateChanged;

    /// <summary>PTT mode: false = Hold-to-Talk (default), true = Hold-to-Mute.</summary>
    public bool PttHoldToMute { get; set; } = false;

    // ── Registration ────────────────────────────────────────────────────

    /// <summary>Attach to the window handle, load saved config, and register all bindings.</summary>
    public void Register(IntPtr hwnd)
    {
        _hwnd = hwnd;
        _source = HwndSource.FromHwnd(hwnd);
        _source?.AddHook(WndProc);

        LoadConfig();
        RegisterAll();
        InstallPttHook();
    }

    /// <summary>Unregister all, apply new bindings, and re-register.</summary>
    public void UpdateBindings(Dictionary<HotkeyAction, HotkeyBinding> newBindings)
    {
        UnregisterAll();
        Bindings = newBindings;
        RegisterAll();
        InstallPttHook();
        SaveConfig();
    }

    /// <summary>Re-bind a single action, re-register, and persist.</summary>
    public void RebindAction(HotkeyAction action, HotkeyBinding binding)
    {
        UnregisterAll();
        Bindings[action] = binding;
        RegisterAll();
        InstallPttHook();
        SaveConfig();
    }

    /// <summary>Reset all bindings to defaults, re-register, and persist.</summary>
    public void ResetToDefaults()
    {
        UnregisterAll();
        Bindings = DefaultBindings;
        RegisterAll();
        InstallPttHook();
        SaveConfig();
    }

    /// <summary>
    /// (Re)register global hotkeys for soundboard sounds. Each binding is given its own
    /// Win32 id on a dedicated range; a prior set is cleared first. Invalid virtual-key
    /// codes are skipped.
    /// </summary>
    public void RegisterSoundHotkeys(IEnumerable<KeyValuePair<string, SoundHotkeyBinding>> bindings)
    {
        UnregisterSoundHotkeys();
        if (_hwnd == IntPtr.Zero || bindings == null) return;
        foreach (var kv in bindings)
        {
            var b = kv.Value;
            if (b == null || b.VirtualKey < 0x01 || b.VirtualKey > 0xFE) continue;
            uint mod = (b.Ctrl ? 2u : 0u) | (b.Alt ? 1u : 0u) | (b.Shift ? 4u : 0u) | MOD_NOREPEAT;
            int id = _nextDynId++;
            if (RegisterHotKey(_hwnd, id, mod, (uint)b.VirtualKey))
                _idToSound[id] = kv.Key;
        }
    }

    /// <summary>Unregister all soundboard hotkeys, freeing their Win32 ids.</summary>
    public void UnregisterSoundHotkeys()
    {
        if (_hwnd != IntPtr.Zero)
            foreach (var id in _idToSound.Keys) UnregisterHotKey(_hwnd, id);
        _idToSound.Clear();
        _nextDynId = DYN_ID_BASE;
    }

    /// <summary>
    /// Check whether <paramref name="candidate"/> conflicts with any existing binding.
    /// Returns the conflicting action, or null if no conflict.
    /// </summary>
    public HotkeyAction? FindConflict(HotkeyAction excludeAction, HotkeyBinding candidate)
    {
        foreach (var (action, binding) in Bindings)
        {
            if (action.Equals(excludeAction)) continue;
            if (binding.Matches(candidate)) return action;
        }
        return null;
    }

    // ── Internal registration helpers ───────────────────────────────────

    private void RegisterAll()
    {
        if (_hwnd == IntPtr.Zero) return;
        _actionToId.Clear();
        _nextId = ID_BASE;

        foreach (var (action, binding) in Bindings)
        {
            // Validate the virtual key is within the usable Win32 range (0x01–0xFE).
            // RegisterHotKey silently ignores out-of-range values, but a corrupted
            // hotkeys.json could inject arbitrary ints — skip them explicitly.
            if (binding.VirtualKey < 0x01 || binding.VirtualKey > 0xFE)
            {
                AppLog.Warning("HotkeyService: skipping {Action} — invalid VK 0x{VK:X2}",
                    action, binding.VirtualKey);
                continue;
            }

            int id = _nextId++;
            uint mod = (binding.Ctrl ? 2u : 0u)
                     | (binding.Alt  ? 1u : 0u)
                     | (binding.Shift ? 4u : 0u);
            mod |= MOD_NOREPEAT;   // suppress OS key-repeat for hotkeys

            if (RegisterHotKey(_hwnd, id, mod, (uint)binding.VirtualKey))
            {
                _actionToId[action] = id;
            }
        }
    }

    private const uint MOD_NOREPEAT = 0x4000;   // Windows 7+

    private void UnregisterAll()
    {
        if (_hwnd == IntPtr.Zero) return;
        foreach (var id in _actionToId.Values)
            UnregisterHotKey(_hwnd, id);
        _actionToId.Clear();
    }

    // ── WndProc ─────────────────────────────────────────────────────────

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            foreach (var (action, actionId) in _actionToId)
            {
                if (actionId == id)
                {
                    HotkeyPressed?.Invoke(action);
                    handled = true;
                    return IntPtr.Zero;
                }
            }
            if (_idToSound.TryGetValue(id, out var soundId))
                SoundHotkeyFired?.Invoke(soundId);
            handled = true;
        }
        return IntPtr.Zero;
    }

    // ── Persistence ─────────────────────────────────────────────────────

    /// <summary>
    /// Test seam: when set, overrides the on-disk config location so persistence
    /// round-trips can be exercised without touching the user's %APPDATA%.
    /// </summary>
    internal string? ConfigPathOverride { get; set; }

    internal string ConfigPath => ConfigPathOverride ?? Path.Combine(
        AppPaths.Root, "hotkeys.json");

    /// <summary>Reserved JSON key storing the Push-to-Talk hold mode (not a binding).</summary>
    private const string PttHoldToMuteKey = "_pttHoldToMute";

    public void SaveConfig()
    {
        try
        {
            var dir = Path.GetDirectoryName(ConfigPath)!;
            Directory.CreateDirectory(dir);

            // Bindings serialize as before (flat action→binding entries) plus one
            // reserved "_pttHoldToMute" boolean so the selected mode survives restarts.
            var dict = new Dictionary<string, object>();
            foreach (var (k, v) in Bindings)
                dict[k.ToString()] = v;
            dict[PttHoldToMuteKey] = PttHoldToMute;

            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* hotkey config save is best-effort */ }
    }

    public void LoadConfig()
    {
        try
        {
            if (!File.Exists(ConfigPath)) return;
            var json = File.ReadAllText(ConfigPath);
            // Read as raw elements so the reserved boolean can coexist with binding objects.
            var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            if (dict == null) return;

            var loaded = new Dictionary<HotkeyAction, HotkeyBinding>();
            foreach (var (key, element) in dict)
            {
                if (key == PttHoldToMuteKey)
                {
                    if (element.ValueKind == JsonValueKind.True) PttHoldToMute = true;
                    else if (element.ValueKind == JsonValueKind.False) PttHoldToMute = false;
                    continue;
                }
                if (!Enum.TryParse<HotkeyAction>(key, out var action)) continue;
                var binding = element.Deserialize<HotkeyBinding>();
                if (binding == null) continue;
                // Reject out-of-range VirtualKey values read from persisted config.
                if (binding.VirtualKey < 0x01 || binding.VirtualKey > 0xFE) continue;
                loaded[action] = binding;
            }

            // Merge: loaded values override defaults, but keep any actions not in the file
            foreach (var (k, v) in loaded)
                Bindings[k] = v;
        }
        catch { /* hotkey config load is best-effort */ }
    }

    // ── Key formatting ──────────────────────────────────────────────────

    /// <summary>Format a binding as a human-readable string (e.g. "Ctrl+F9").</summary>
    public static string FormatKey(HotkeyBinding b)
    {
        var parts = new List<string>(4);
        if (b.Ctrl)  parts.Add("Ctrl");
        if (b.Alt)   parts.Add("Alt");
        if (b.Shift) parts.Add("Shift");

        string keyName;
        try { keyName = KeyInterop.KeyFromVirtualKey(b.VirtualKey).ToString(); }
        catch { keyName = $"VK_{b.VirtualKey}"; }

        // Shorten common key names
        if (keyName.Length == 1) keyName = keyName.ToUpperInvariant(); // single letters
        if (keyName.StartsWith("D") && keyName.Length == 2 && char.IsDigit(keyName[1]))
            keyName = keyName[1].ToString(); // D1→1, D2→2 …
        if (keyName.StartsWith("Oem")) keyName = keyName.Substring(3); // OemPeriod→Period

        parts.Add(keyName);
        return string.Join("+", parts);
    }

    // ── PTT keyboard hook management ──────────────────────────────────

    /// <summary>Install or remove the low-level keyboard hook based on whether PTT is bound.</summary>
    private void InstallPttHook()
    {
        bool hasPtt = Bindings.ContainsKey(HotkeyAction.PushToTalk);
        if (hasPtt && _hookId == IntPtr.Zero)
        {
            _hookProc = HookCallback;
            _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _hookProc, GetModuleHandle("user32.dll"), 0);
        }
        else if (!hasPtt && _hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
            _hookProc = null;
            _pttKeyPressed = false;
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (nCode >= 0 && Bindings.TryGetValue(HotkeyAction.PushToTalk, out var pttBinding))
            {
                int vk = pttBinding.VirtualKey;
                int msg = wParam.ToInt32();

                if ((msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN) && !_pttKeyPressed)
                {
                    // Check if the pressed key matches the PTT binding (ignore extra modifiers for PTT)
                    var modifiers = Keyboard.Modifiers;
                    bool modifiersOk = (!pttBinding.Ctrl  || (modifiers & ModifierKeys.Control)  != 0)
                                    && (!pttBinding.Alt   || (modifiers & ModifierKeys.Alt)      != 0)
                                    && (!pttBinding.Shift || (modifiers & ModifierKeys.Shift)    != 0);

                    if (Marshal.ReadInt32(lParam) == vk && modifiersOk)
                    {
                        _pttKeyPressed = true;
                        try { PushToTalkStateChanged?.Invoke(true); } catch { /* PTT state change is best-effort */ }
                    }
                }
                else if ((msg == WM_KEYUP || msg == WM_SYSKEYUP) && _pttKeyPressed)
                {
                    if (Marshal.ReadInt32(lParam) == vk)
                    {
                        _pttKeyPressed = false;
                        try { PushToTalkStateChanged?.Invoke(false); } catch { /* PTT state change is best-effort */ }
                    }
                }
            }
        }
        catch
        {
            // NEVER let an exception escape the hook callback — an unhandled
            // exception here would break the global keyboard hook chain and
            // potentially crash the process or leave the hook installed.
        }
        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    // ── Cleanup ─────────────────────────────────────────────────────────

    public void Dispose()
    {
        UnregisterAll();
        UnregisterSoundHotkeys();
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
            _hookProc = null;
        }
        _source?.RemoveHook(WndProc);
    }
}
