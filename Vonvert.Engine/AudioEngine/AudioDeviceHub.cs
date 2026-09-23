// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace Vonvert.Engine.AudioEngine;

/*
 * Enumerates Windows audio endpoints and monitors hot-plug events.
 *
 * Implements IMMNotificationClient so the UI can react to devices being
 * plugged in or removed without polling.  All callbacks guard against
 * late invocation after Dispose via a volatile shutdown flag.
 */
public sealed class AudioDeviceHub : IMMNotificationClient, IDisposable
{
    private readonly MMDeviceEnumerator _mmEnum = new();
    private volatile bool _shuttingDown;

    public event Action? DevicesChanged;
    public event Action<string>? DeviceRemoved;
    public event Action<string>? DeviceAdded;

    public AudioDeviceHub()
    {
        _mmEnum.RegisterEndpointNotificationCallback(this);
    }

    // ── Public queries ───────────────────────────────────────────────────

    public IReadOnlyList<AudioDevice> InputDevices()   => EnumerateEndpoints(DataFlow.Capture);
    public IReadOnlyList<AudioDevice> OutputDevices()  => EnumerateEndpoints(DataFlow.Render);

    public MMDevice? Resolve(string id)
    {
        try { return _mmEnum.GetDevice(id); } catch { return null; }
    }

    public MMDevice? DefaultInputDevice()
    {
        try { return _mmEnum.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications); }
        catch { return null; }
    }

    public MMDevice? DefaultOutputDevice()
    {
        try { return _mmEnum.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia); }
        catch { return null; }
    }

    /// <summary>
    /// Search for a virtual audio render endpoint (loopback cable) to send the
    /// processed voice into. Recognises VB-Cable and VAC (both contain "CABLE")
    /// as well as VoiceMeeter's virtual inputs. Returns the first active match,
    /// or null when no virtual device is installed.
    /// </summary>
    public MMDevice? FindVBCable()
    {
        try
        {
            return _mmEnum.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                          .FirstOrDefault(d =>
                          {
                              var name = d.FriendlyName ?? string.Empty;
                              return name.Contains("CABLE", StringComparison.OrdinalIgnoreCase)
                                  || name.Contains("VOICEMEETER", StringComparison.OrdinalIgnoreCase);
                          });
        }
        catch { return null; }
    }

    // ── Enumeration helper ───────────────────────────────────────────────

    private IReadOnlyList<AudioDevice> EnumerateEndpoints(DataFlow flow)
    {
        var result = new List<AudioDevice>();
        try
        {
            foreach (var dev in _mmEnum.EnumerateAudioEndPoints(flow, DeviceState.Active))
            {
                try
                {
                    string devId   = dev.ID;
                    string devName = dev.FriendlyName;

                    try
                    {
                        var mix = dev.AudioClient.MixFormat;
                        result.Add(new AudioDevice(devId, devName, mix.Channels, mix.SampleRate, mix.BitsPerSample));
                    }
                    catch
                    {
                        // AudioClient unavailable (device busy / driver issue) —
                        // add with safe fallback so the UI dropdown still shows it.
                        result.Add(new AudioDevice(devId, devName, 2, 48000, 16));
                    }
                }
                catch
                {
                    // Individual device property access failed — skip it but
                    // continue enumerating the remaining devices.
                    AppLog.Warning("[AudioDeviceHub] Skipped a {Flow} endpoint (property access failed)", flow);
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Warning(ex, "[AudioDeviceHub] EnumerateAudioEndPoints({Flow}) failed", flow);
        }
        return result;
    }

    // ── IMMNotificationClient callbacks ──────────────────────────────────

    void IMMNotificationClient.OnDeviceAdded(string id)
    {
        if (_shuttingDown) return;
        DeviceAdded?.Invoke(id);
        DevicesChanged?.Invoke();
    }

    void IMMNotificationClient.OnDeviceRemoved(string id)
    {
        if (_shuttingDown) return;
        DeviceRemoved?.Invoke(id);
        DevicesChanged?.Invoke();
    }

    void IMMNotificationClient.OnDefaultDeviceChanged(DataFlow f, Role r, string id)
    {
        if (_shuttingDown) return;
        DevicesChanged?.Invoke();
    }

    void IMMNotificationClient.OnDeviceStateChanged(string id, DeviceState s)
    {
        if (_shuttingDown) return;
        DevicesChanged?.Invoke();
    }

    void IMMNotificationClient.OnPropertyValueChanged(string id, PropertyKey k) { }

    // ── Disposal ─────────────────────────────────────────────────────────

    public void Dispose()
    {
        // 1. Signal shutdown — guards against late IMM callbacks
        _shuttingDown = true;

        // 2. Detach all handlers before releasing COM objects so that any
        //    in-flight callback that slips past the flag check becomes a no-op.
        DevicesChanged = null;
        DeviceRemoved  = null;
        DeviceAdded    = null;

        // 3. Unregister and release the COM enumerator
        try { _mmEnum.UnregisterEndpointNotificationCallback(this); } catch { /* unregistration is best-effort */ }
        try { _mmEnum.Dispose(); } catch { /* enumerator dispose is best-effort */ }
    }
}
