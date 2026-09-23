// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System.Collections;
using System.Reflection;

namespace Vonvert.Engine.PresetLibrary;

/// <summary>
/// Robustness guard for untrusted <see cref="VoiceProfile"/> data
/// (.vopreset imports and hand-edited / earlier-poisoned disk files).
///
/// A single NaN/Infinity parameter is sticky — it silently poisons every frame
/// downstream of the DSP chain — and absurd magnitudes (1e30 semitones,
/// -1e12 Hz) produce clipping or dead output. This guard walks the profile's
/// numeric parameters and resets out-of-trust values to the defaults a fresh
/// instance declares, leaving valid parameters untouched.
///
/// Hardening never throws: a property that refuses inspection is skipped.
/// </summary>
internal static class PresetValueGuard
{
    /// <summary>Beyond this magnitude every DSP parameter is certainly garbage —
    /// frequencies, dB, milliseconds, ratios and gains all live far below 1e6.</summary>
    internal const double AbsurdMagnitude = 1e6;

    public static void Sanitize(VoiceProfile? profile)
    {
        if (profile == null) return;
        try
        {
            Walk(profile, new VoiceProfile(), new HashSet<object>(ReferenceEqualityComparer.Instance), 0);
        }
        catch (Exception ex)
        {
            AppLog.Warning(ex, "[PresetValueGuard] Sanitize failed for preset '{Name}'", profile.Name);
        }
    }

    private static void Walk(object obj, object defaults, HashSet<object> visited, int depth)
    {
        if (!visited.Add(obj)) return;

        foreach (var prop in obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.CanRead || !prop.CanWrite || prop.GetIndexParameters().Length > 0) continue;
            try
            {
                var t = prop.PropertyType;
                if (t == typeof(float) || t == typeof(double))
                {
                    var value = Convert.ToDouble(prop.GetValue(obj));
                    if (IsSuspect(value))
                    {
                        // Reset to the declared default (e.g. ratio 1.0), not blindly 0.
                        prop.SetValue(obj, prop.GetValue(defaults) ?? Convert.ChangeType(0, t));
                    }
                }
                else if (t == typeof(float[]))
                {
                    if (prop.GetValue(obj) is float[] arr)
                        for (int i = 0; i < arr.Length; i++)
                            if (IsSuspect(arr[i])) arr[i] = 0f;   // arrays of gains: 0 is the neutral element
                }
                else if (t == typeof(List<float>))
                {
                    if (prop.GetValue(obj) is List<float> list)
                        for (int i = 0; i < list.Count; i++)
                            if (IsSuspect(list[i])) list[i] = 0f;
                }
                else if (depth < 2 && IsPlainConfig(t))
                {
                    if (prop.GetValue(obj) is object child)
                        Walk(child, Activator.CreateInstance(t)!, visited, depth + 1);
                }
                else if (depth < 2 && typeof(IEnumerable).IsAssignableFrom(t))
                {
                    if (prop.GetValue(obj) is IEnumerable seq)
                        foreach (var item in seq)
                            if (item != null && IsPlainConfig(item.GetType()))
                                Walk(item, Activator.CreateInstance(item.GetType())!, visited, depth + 1);
                }
            }
            catch
            {
                // Hardening must never throw on untrusted data — skip this property.
            }
        }
    }

    private static bool IsSuspect(double v)
        => double.IsNaN(v) || double.IsInfinity(v) || Math.Abs(v) > AbsurdMagnitude;

    /// <summary>Only recurse into Vonvert-owned simple config/POCO types — never
    /// into framework objects, delegates, or live engine handles.</summary>
    private static bool IsPlainConfig(Type t)
        => t.IsClass
           && t != typeof(string)
           && (t.Namespace?.StartsWith("Vonvert.", StringComparison.Ordinal) ?? false)
           && t.GetConstructor(Type.EmptyTypes) != null;
}
