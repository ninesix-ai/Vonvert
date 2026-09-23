// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace Vonvert.App.UIServices;

/// <summary>
/// Collects runtime version information about the application, the .NET runtime,
/// the OS, and loaded dependency assemblies. The output is plain text with one
/// key/value per line so it can be shown in the About tab and copied to the
/// clipboard. Adapted for the minimal English-only GitHub build (no Chinese
/// labels, no privacy-policy link).
/// </summary>
public static class VersionInfoService
{
    /// <summary>
    /// Collect all version info and return it as a formatted plain-text string
    /// (key-value pairs, one per line).
    /// </summary>
    public static string GetVersionInfoText()
    {
        var sb = new StringBuilder();

        // ── Application info ──
        sb.AppendLine("=== Vonvert Version Info ===");

        var asm = Assembly.GetExecutingAssembly();
        var ver = asm.GetName().Version;
        sb.AppendLine($"Version: {ver?.ToString() ?? "unknown"}");

        var infoVer = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrEmpty(infoVer))
            sb.AppendLine($"Full version: {infoVer}");

        sb.AppendLine($".NET Runtime: {Environment.Version}");
        sb.AppendLine($"Framework: {RuntimeInformation.FrameworkDescription}");
        sb.AppendLine($"OS: {Environment.OSVersion}");
        sb.AppendLine($"OS architecture: {RuntimeInformation.OSArchitecture}");
        sb.AppendLine($"Process architecture: {RuntimeInformation.ProcessArchitecture}");

        // ── Dependency versions ──
        sb.AppendLine();
        sb.AppendLine("=== Dependency Components ===");

        var referencedAssemblies = asm.GetReferencedAssemblies();
        foreach (var refAsm in referencedAssemblies.OrderBy(a => a.Name))
        {
            try
            {
                var loaded = Assembly.Load(refAsm);
                var depVer = loaded.GetName().Version;
                sb.AppendLine($"{refAsm.Name}: {depVer?.ToString() ?? "unknown"}");
            }
            catch
            {
                sb.AppendLine($"{refAsm.Name}: {refAsm.Version}");
            }
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Returns the short app version string (e.g. "0.0.1").
    /// </summary>
    public static string GetShortVersion()
    {
        var ver = Assembly.GetExecutingAssembly().GetName().Version;
        return ver?.ToString() ?? "unknown";
    }
}
