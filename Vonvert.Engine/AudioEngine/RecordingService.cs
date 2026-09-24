// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using NAudio.Wave;
using Vonvert.Engine.PresetLibrary;

namespace Vonvert.Engine.AudioEngine;

/// <summary>
/// Records audio output (Original or Processed mode) and exports to WAV/MP3.
/// Supports single recording sessions and batch processing.
/// Thread-safe: WriteSamples is called from the DSP thread while other methods
/// run on the UI thread. Events are raised on the calling thread — subscribers
/// must dispatch to the UI thread if needed.
/// </summary>
public sealed class RecordingService : IDisposable
{
    // Derive from AudioConstants so the WAV-header rate always matches the
    // engine's actual DSP rate.
    private const int SAMPLE_RATE = AudioConstants.EngineRate;
    private const int CHANNELS = 1;

    private WaveFileWriter? _writer;
    private string? _currentFile;
    private volatile bool _isRecording;
    private readonly object _lock = new();
    private volatile RecordMode _recordMode = RecordMode.Processed;

    private readonly List<RecordingHistoryItem> _history = new();
    private readonly string _recordingsFolder;

    // Pre-allocated scratch buffers for span-based writes (DSP thread only).
    // Grown on demand via Array.Resize, never shrunk — avoids per-block allocation.
    private float[] _rawScratch = new float[8192];
    private float[] _procScratch = new float[8192];

    public bool IsRecording => _isRecording;
    public string? CurrentFile => _currentFile;

    /// <summary>Current recording mode: Original (raw mic) or Processed (post-DSP). Default: Processed.</summary>
    public RecordMode RecordMode
    {
        get => _recordMode;
        set => _recordMode = value;
    }

    /// <summary>Returns a snapshot of recording history (thread-safe copy).</summary>
    public List<RecordingHistoryItem> History
    {
        get { lock (_lock) { return _history.ToList(); } }
    }

    public string RecordingsFolder => _recordingsFolder;

    public event Action? RecordingStarted;
    public event Action? RecordingStopped;
    public event Action<RecordingHistoryItem>? RecordingSaved;
    /// <summary>Fired when the recorded WAV file could not be moved to the recordings folder.</summary>
    public event Action<string>? RecordingSaveFailed;

    public RecordingService()
    {
        // Everything user-facing lives under the single, user-relocatable
        // AppPaths.Root (publisher-namespaced), alongside presets/config/logs.
        _recordingsFolder = Path.Combine(AppPaths.Root, "Recordings");

        Directory.CreateDirectory(_recordingsFolder);
        LoadHistory();
    }

    /// <summary>
    /// Start recording processed audio to a temporary file.
    /// Returns a Result indicating success or failure — callers must check the result
    /// to avoid silent recording failures.
    /// </summary>
    public Result<Unit> StartRecording()
    {
        lock (_lock)
        {
            if (_isRecording) return Result.Ok(Result.Value);

            try
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                string tempFile = Path.Combine(_recordingsFolder, $"temp_{timestamp}_{Environment.CurrentManagedThreadId}.wav");
                var format = WaveFormat.CreateIeeeFloatWaveFormat(SAMPLE_RATE, CHANNELS);
                _writer = new WaveFileWriter(tempFile, format);
                _currentFile = tempFile;
                _isRecording = true;

                RecordingStarted?.Invoke();
                return Result.Ok(Result.Value);
            }
            catch (Exception ex)
            {
                AppLog.Error(ex, "RecordingService.StartRecording failed");
                _writer?.Dispose();
                _writer = null;
                _currentFile = null;
                _isRecording = false;
                return Result.Fail<Unit>(ex);
            }
        }
    }

    /// <summary>
    /// Write audio samples to the recording file.
    /// Called from the DSP thread with processed audio.
    /// Uses lock to synchronize with StopRecording/CancelRecording on the UI thread.
    /// </summary>
    public void WriteSamples(float[] samples)
    {
        WriteSamples(samples, samples.Length);
    }

    /// <summary>
    /// Write a specified number of audio samples to the recording file.
    /// Allows passing a rented/pooled buffer with an explicit count — avoids per-block allocation.
    /// Uses TryEnter so the audio thread is never blocked by a UI-thread lock hold.
    /// </summary>
    public void WriteSamples(float[] samples, int count)
    {
        if (!_isRecording || _recordMode != RecordMode.Processed) return;
        if (!Monitor.TryEnter(_lock)) return;
        try
        {
            if (!_isRecording || _writer == null) return;
            if (_recordMode != RecordMode.Processed) return;
            _writer.WriteSamples(samples, 0, count);
        }
        catch
        {
            // Silent fail - never crash audio thread
        }
        finally
        {
            Monitor.Exit(_lock);
        }
    }

    /// <summary>
    /// Write raw (pre-DSP) audio samples to the recording file.
    /// Called from the DSP thread before DSP processing.
    /// Only writes when RecordMode == Original.
    /// </summary>
    public void WriteRawSamples(float[] samples)
    {
        WriteRawSamples(samples, samples.Length);
    }

    /// <summary>
    /// Write a specified number of raw audio samples to the recording file.
    /// Uses TryEnter so the audio thread is never blocked by a UI-thread lock hold.
    /// </summary>
    public void WriteRawSamples(float[] samples, int count)
    {
        if (!_isRecording || _recordMode != RecordMode.Original) return;
        if (!Monitor.TryEnter(_lock)) return;
        try
        {
            if (!_isRecording || _writer == null) return;
            if (_recordMode != RecordMode.Original) return;
            _writer.WriteSamples(samples, 0, count);
        }
        catch
        {
            // Silent fail - never crash audio thread
        }
        finally
        {
            Monitor.Exit(_lock);
        }
    }

    /// <summary>
    /// Write raw (pre-DSP) audio samples from a span. Copies into a stable
    /// internal buffer so the caller's memory can be reused immediately.
    /// Zero-allocation on the DSP hot path — avoids ToArray() per block.
    /// </summary>
    public void WriteRawSamples(ReadOnlySpan<float> samples)
    {
        if (!_isRecording || _recordMode != RecordMode.Original) return;
        if (!Monitor.TryEnter(_lock)) return;
        try
        {
            if (!_isRecording || _writer == null) return;
            if (_recordMode != RecordMode.Original) return;
            // Copy span into the pre-allocated scratch buffer, then write
            // from the buffer. Two-step ensures the caller's span memory
            // (e.g. stack pool, SampleQueue) is not held across the I/O.
            if (samples.Length > _rawScratch.Length)
                Array.Resize(ref _rawScratch, samples.Length);
            samples.CopyTo(_rawScratch);
            _writer.WriteSamples(_rawScratch, 0, samples.Length);
        }
        catch
        {
            // Silent fail - never crash audio thread
        }
        finally
        {
            Monitor.Exit(_lock);
        }
    }

    /// <summary>
    /// Write processed audio samples from a span. Copies into a stable
    /// internal buffer to avoid per-block allocation on the DSP thread.
    /// </summary>
    public void WriteSamples(ReadOnlySpan<float> samples)
    {
        if (!_isRecording || _recordMode != RecordMode.Processed) return;
        if (!Monitor.TryEnter(_lock)) return;
        try
        {
            if (!_isRecording || _writer == null) return;
            if (_recordMode != RecordMode.Processed) return;
            if (samples.Length > _procScratch.Length)
                Array.Resize(ref _procScratch, samples.Length);
            samples.CopyTo(_procScratch);
            _writer.WriteSamples(_procScratch, 0, samples.Length);
        }
        catch
        {
            // Silent fail - never crash audio thread
        }
        finally
        {
            Monitor.Exit(_lock);
        }
    }

    /// <summary>
    /// Stop recording and save the file.
    /// File I/O (move + history write) is done outside the lock so the DSP
    /// thread is never stalled by disk latency.
    /// </summary>
    public RecordingHistoryItem? StopRecording()
    {
        RecordingHistoryItem? savedItem = null;
        string? fileToMove = null;
        string? finalName = null;
        string? finalPath = null;

        // Phase 1: Under lock — stop recording and close writer
        lock (_lock)
        {
            if (!_isRecording || _writer == null) return null;

            try
            {
                // Set flag FIRST so DSP thread stops calling WriteSamples
                _isRecording = false;

                try { _writer.Dispose(); } catch { /* writer dispose is best-effort */ }
                _writer = null;

                // Capture file info for phase 2 (outside lock)
                if (_currentFile != null && File.Exists(_currentFile))
                {
                    fileToMove = _currentFile;
                    string modeTag = _recordMode == RecordMode.Original ? "Original_" : "";
                    finalName = $"Recording_{modeTag}{DateTime.Now:yyyyMMdd_HHmmss_fff}_{Environment.CurrentManagedThreadId}.wav";
                    finalPath = Path.Combine(_recordingsFolder, finalName);
                }

                _currentFile = null;
            }
            catch
            {
                _currentFile = null;
                _isRecording = false;
            }
        }

        // Phase 2: Outside lock — file I/O (avoids blocking the DSP thread)
        if (fileToMove != null && finalName != null && finalPath != null)
        {
            try
            {
                File.Move(fileToMove, finalPath, overwrite: true);

                var fileInfo = new FileInfo(finalPath);
                savedItem = new RecordingHistoryItem
                {
                    FileName = finalName,
                    FilePath = finalPath,
                    Duration = GetAudioDuration(finalPath),
                    FileSize = fileInfo.Length,
                    CreatedAt = fileInfo.CreationTime,
                };

                lock (_lock)
                {
                    _history.Insert(0, savedItem);
                    SaveHistory();
                }
            }
            catch (Exception ex)
            {
                AppLog.Warning(ex, "RecordingService: StopRecording file move failed '{Source}' → '{Dest}'", fileToMove, finalPath);
                savedItem = null;
                RecordingSaveFailed?.Invoke($"Recording could not be saved. The temporary file is still at: {fileToMove}");
            }
        }

        // Raise events OUTSIDE lock to prevent deadlocks
        if (savedItem != null)
            RecordingSaved?.Invoke(savedItem);
        RecordingStopped?.Invoke();

        return savedItem;
    }

    /// <summary>
    /// Cancel current recording and delete temporary file.
    /// </summary>
    public void CancelRecording()
    {
        bool wasRecording;

        lock (_lock)
        {
            wasRecording = _isRecording;
            _isRecording = false;

            try { _writer?.Dispose(); } catch { /* writer dispose is best-effort */ }
            _writer = null;

            if (_currentFile != null && File.Exists(_currentFile))
            {
                try { File.Delete(_currentFile); } catch { /* temp file cleanup is best-effort */ }
            }

            _currentFile = null;
        }

        // Raise event OUTSIDE lock
        if (wasRecording)
            RecordingStopped?.Invoke();
    }

    /// <summary>
    /// Registers an externally-produced recording file (already finalized on
    /// disk) into the recording history. Neutral extension point for
    /// alternative capture producers.
    /// </summary>
    public RecordingHistoryItem? RegisterExternalRecording(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return null;

            var fileInfo = new FileInfo(filePath);
            var item = new RecordingHistoryItem
            {
                FileName = Path.GetFileName(filePath),
                FilePath = filePath,
                Duration = GetAudioDuration(filePath),
                FileSize = fileInfo.Length,
                CreatedAt = fileInfo.CreationTime,
            };

            lock (_lock)
            {
                _history.Insert(0, item);
                SaveHistory();
            }

            RecordingSaved?.Invoke(item);
            return item;
        }
        catch (Exception ex)
        {
            AppLog.Warning(ex, "RecordingService: RegisterExternalRecording failed for '{Path}'", filePath);
            return null;
        }
    }

    /// <summary>Export a recording — delegates to <see cref="RecordingExporter"/>.</summary>
    public bool ExportRecording(RecordingHistoryItem item, string outputPath, ExportOptions options)
        => RecordingExporter.ExportRecording(item, outputPath, options);

    /// <summary>Backward-compatible overload using ExportFormat enum.</summary>
    public bool ExportRecording(RecordingHistoryItem item, string outputPath, ExportFormat format)
        => RecordingExporter.ExportRecording(item, outputPath, format);

    /// <summary>
    /// Delete a recording from history and disk.
    /// </summary>
    public bool DeleteRecording(RecordingHistoryItem item)
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(item.FilePath))
                {
                    File.Delete(item.FilePath);
                }

                _history.Remove(item);
                SaveHistory();
                return true;
            }
            catch (Exception ex)
            {
                AppLog.Warning(ex, "RecordingService: DeleteRecording failed for '{Path}'", item.FilePath);
                return false;
            }
        }
    }

    /// <summary>
    /// Delete multiple recordings from history and disk.
    /// </summary>
    public int DeleteRecordings(IEnumerable<RecordingHistoryItem> items)
    {
        int count = 0;
        lock (_lock)
        {
            var snapshot = items.ToList();
            foreach (var item in snapshot)
            {
                try
                {
                    if (File.Exists(item.FilePath))
                        File.Delete(item.FilePath);

                    if (_history.Remove(item))
                        count++;
                }
                catch (Exception ex)
                {
                    AppLog.Warning(ex, "RecordingService: DeleteRecordings failed for '{Path}'", item.FilePath);
                }
            }
            if (count > 0) SaveHistory();
        }
        return count;
    }

    /// <summary>Read an audio file — delegates to <see cref="RecordingExporter.ReadAudioFile"/>.</summary>
    public float[] ReadAudioFile(string path) => RecordingExporter.ReadAudioFile(path);

    /// <summary>Write samples as WAV — delegates to <see cref="RecordingExporter.WriteWavFile"/>.</summary>
    public void WriteWavFile(string path, float[] samples) => RecordingExporter.WriteWavFile(path, samples);

    private double GetAudioDuration(string path)
    {
        try
        {
            using var reader = new AudioFileReader(path);
            return reader.TotalTime.TotalSeconds;
        }
        catch (Exception ex) { AppLog.Debug(ex, "RecordingService: GetAudioDuration failed for '{Path}'", path); return 0; }
    }

    private void LoadHistory()
    {
        lock (_lock)
        {
            try
            {
                string historyFile = Path.Combine(_recordingsFolder, "history.json");
                var json = SafeFileHelper.ReadAllTextSafe(historyFile);
                if (json != null)
                {
                    var items = Newtonsoft.Json.JsonConvert.DeserializeObject<List<RecordingHistoryItem>>(json);
                    if (items != null)
                    {
                        _history.Clear();
                        _history.AddRange(items.Where(i => File.Exists(i.FilePath)));
                    }
                }
            }
            catch (Exception ex) { AppLog.Warning(ex, "RecordingService: LoadHistory failed"); }
        }
    }

    private void SaveHistory()
    {
        // Called under _lock from StopRecording/DeleteRecording
        try
        {
            string historyFile = Path.Combine(_recordingsFolder, "history.json");
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(_history, Newtonsoft.Json.Formatting.Indented);
            SafeFileHelper.WriteAllTextSafe(historyFile, json);
        }
        catch (Exception ex) { AppLog.Warning(ex, "RecordingService: SaveHistory failed"); }
    }

    /// <summary>Batch-process files — delegates to <see cref="RecordingExporter.BatchProcess"/>.</summary>
    public List<BatchProcessResult> BatchProcess(
        string[] inputPaths, VoiceProfile preset, string outputFolder,
        ExportFormat format, IProgress<BatchProgress>? progress = null)
        => RecordingExporter.BatchProcess(inputPaths, preset, outputFolder, format, progress);

    public void Dispose()
    {
        CancelRecording();
    }
}

public record RecordingHistoryItem
{
    public string FileName { get; init; } = "";
    public string FilePath { get; init; } = "";
    public double Duration { get; init; }
    public long FileSize { get; init; }
    public DateTime CreatedAt { get; init; }
}

public record BatchProcessResult
{
    public string InputFile { get; init; } = "";
    public string? OutputFile { get; init; }
    public bool Success { get; init; }
    public string? Error { get; init; }
}

/// <summary>
/// Export parameters for all supported formats.
/// </summary>
public record ExportOptions
{
    public ExportFormat Format { get; set; } = ExportFormat.Wav;

    // WAV parameters
    public int SampleRate { get; set; } = AudioConstants.EngineRate; // engine default (48000)
    public int BitDepth { get; set; } = 32;           // 16, 24, or 32
    public int Channels { get; set; } = 1;             // 1 (Mono) or 2 (Stereo)

    // MP3 parameters
    public int Bitrate { get; set; } = 128;            // 64, 96, 128, 192, 256, 320
    public bool UseVBR { get; set; } = false;          // CBR or VBR

    // FLAC parameters
    public int FlacCompression { get; set; } = 5;      // 0 (fastest) - 8 (best compression)

    // OGG Vorbis parameters
    public float VorbisQuality { get; set; } = 0.5f;   // 0.0 (lowest) - 1.0 (highest)

    // AAC parameters
    public int AacBitrate { get; set; } = 128;         // 64, 96, 128, 192, 256
}

public enum ExportFormat
{
    Wav,
    Mp3,
    Flac,
    Ogg,
    Aac
}

public enum RecordMode
{
    Processed,  // Record processed audio (default)
    Original    // Record raw mic audio (pre-DSP)
}
