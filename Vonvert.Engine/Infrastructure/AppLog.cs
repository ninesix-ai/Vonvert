// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using Serilog;
using Serilog.Events;
using System.Diagnostics;
using System.Reflection;

namespace Vonvert.Engine;

/// <summary>
/// Application-wide logging facade backed by Serilog.
/// Logs are written to %APPDATA%\Vonvert\logs\ with daily rolling.
/// Initialized once at startup by the UI layer; safe to call from any thread.
/// </summary>
public static class AppLog
{
    /// <summary>Root log directory: %APPDATA%\Vonvert\logs\</summary>
    public static string LogDirectory { get; private set; } = "";

    /// <summary>Current minimum log level (default: Debug).</summary>
    public static LogEventLevel MinimumLevel { get; private set; } = LogEventLevel.Debug;

    /// <summary>
    /// Initialize the Serilog pipeline. Call once from App.OnStartup.
    /// </summary>
    public static void Init()
    {
        Init(LogEventLevel.Debug);
    }

    /// <summary>
    /// Initialize the Serilog pipeline with a specified minimum level.
    /// Call once from App.OnStartup.
    /// </summary>
    public static void Init(LogEventLevel minimumLevel)
    {
        MinimumLevel = minimumLevel;
        LogDirectory = Path.Combine(AppPaths.Root, "logs");
        Directory.CreateDirectory(LogDirectory);

        var logFile = Path.Combine(LogDirectory, "vonvert-.log"); // Serilog appends date before .log

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
            .WriteTo.File(
                logFile,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                shared: true,
                fileSizeLimitBytes: 10_000_000,     // 10 MB per file
                rollOnFileSizeLimit: true,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{Thread}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        Information("=== Vonvert starting ===");
        Information("Version: {Version}", Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown");
        // Reduce logged system info to avoid exposing sensitive environment details:
        // only the OS version family, not the full version string.
        Information("OS: Windows {OSVersion}", Environment.OSVersion.Version.Major + "." + Environment.OSVersion.Version.Minor);
        Information("Runtime: .NET {Runtime}", System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription);
        // Don't log the full log directory path (could reveal user profile structure).
        Information("Log directory: %APPDATA%\\Vonvert\\logs");
        Information("Minimum level: {Level}", minimumLevel);
    }

    /// <summary>
    /// Change the minimum log level at runtime. Useful for temporarily
    /// enabling verbose Debug logging to troubleshoot a user issue.
    /// </summary>
    public static void SetMinimumLevel(LogEventLevel level)
    {
        MinimumLevel = level;
        // Reconfigure the pipeline with the new minimum level
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(level)
            .WriteTo.File(
                Path.Combine(LogDirectory, "vonvert-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                shared: true,
                fileSizeLimitBytes: 10_000_000,
                rollOnFileSizeLimit: true,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{Thread}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
        Information("Log level changed to {Level}", level);
    }

    /// <summary>
    /// Change the minimum log level at runtime using a string name
    /// ("Verbose", "Debug", "Information", "Warning", "Error", "Fatal").
    /// Unknown values are ignored and a warning is logged.
    /// </summary>
    public static void SetMinimumLevel(string level)
    {
        if (!Enum.TryParse<LogEventLevel>(level, ignoreCase: true, out var parsed))
        {
            Warning("SetMinimumLevel: unknown level '{Level}', ignored", level);
            return;
        }
        SetMinimumLevel(parsed);
    }

    /// <summary>
    /// Create a scoped timing helper that logs elapsed time when disposed.
    /// Usage: using (AppLog.TimeScope("Loading presets")) { ... }
    /// </summary>
    public static TimingScope TimeScope(string operation) => new(operation);

    /// <summary>
    /// Log a performance metric at Information level.
    /// </summary>
    public static void Performance(string metric, double value, string unit = "")
    {
        Information("[Perf] {Metric} = {Value:0.##}{Unit}", metric, value, unit);
    }

    /// <summary>
    /// Disposable timing scope that logs elapsed milliseconds on dispose.
    /// </summary>
    public sealed class TimingScope : IDisposable
    {
        private readonly string _operation;
        private readonly Stopwatch _sw = Stopwatch.StartNew();

        internal TimingScope(string operation)
        {
            _operation = operation;
            Debug("Timing scope started: {Operation}", _operation);
        }

        public void Dispose()
        {
            _sw.Stop();
            Information("[Timing] {Operation} completed in {Ms:0.##}ms", _operation, _sw.Elapsed.TotalMilliseconds);
        }
    }

    // ── Convenience methods ─────────────────────────────────────────────

    public static void Information(string msg)              => Log.Information(msg);
    public static void Information(string msg, params object[] args) => Log.Information(msg, args);
    public static void Warning(string msg)                  => Log.Warning(msg);
    public static void Warning(string msg, params object[] args)     => Log.Warning(msg, args);
    public static void Warning(Exception ex, string msg)    => Log.Warning(ex, "{Message}", msg);
    public static void Warning(Exception ex, string msg, params object[] args) => Log.Warning(ex, msg, args);
    public static void Error(string msg)                    => Log.Error(msg);
    public static void Error(Exception ex, string msg)      => Log.Error(ex, "{Message}", msg);
    public static void Error(string msg, params object[] args)       => Log.Error(msg, args);
    public static void Error(Exception ex, string msg, params object[] args) => Log.Error(ex, msg, args);
    public static void Debug(string msg)                    => Log.Debug(msg);
    public static void Debug(string msg, params object[] args)       => Log.Debug(msg, args);
    public static void Debug(Exception ex, string msg)               => Log.Debug(ex, "{Message}", msg);
    public static void Debug(Exception ex, string msg, params object[] args) => Log.Debug(ex, msg, args);

    /// <summary>
    /// Force-flush buffered log entries to disk.
    /// Serilog's File sink buffers writes; call this after critical errors
    /// so the entry survives a process crash that skips Shutdown().
    /// Implementation: swaps in a fresh logger (CloseAndFlush drains the old one).
    /// </summary>
    public static void Flush()
    {
        try
        {
            var current = Log.Logger;
            Log.CloseAndFlush();
            // Re-create the logger so subsequent writes still work.
            // We reuse the same configuration as Init().
            var logFile = Path.Combine(LogDirectory, "vonvert-.log");
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Is(MinimumLevel)
                .WriteTo.File(
                    logFile,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    shared: true,
                    fileSizeLimitBytes: 10_000_000,
                    rollOnFileSizeLimit: true,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{Thread}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();
        }
        catch
        {
            // Flush is best-effort; never throw from here.
        }
    }

    /// <summary>Flush and close the Serilog pipeline. Call at app exit.</summary>
    public static void Shutdown()
    {
        try { Log.Information("=== Vonvert shutting down ==="); } catch { /* shutdown log is best-effort */ }
        Log.CloseAndFlush();
    }
}
