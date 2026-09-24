// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

using NAudio.Lame;
using NAudio.Wave;
using Vonvert.Engine.PresetLibrary;

namespace Vonvert.Engine.AudioEngine;

/// <summary>Progress update for batch processing.</summary>
public record BatchProgress(int Current, int Total, string CurrentFile);

/// <summary>
/// Static export utilities extracted from <see cref="RecordingService"/>.
/// Handles format conversion (WAV/MP3/FLAC/OGG/AAC), batch DSP processing,
/// and low-level audio file I/O.
/// </summary>
public static class RecordingExporter
{
    private const int SAMPLE_RATE = AudioConstants.EngineRate;
    private const int CHANNELS = 1;

    /// <summary>Maximum accepted input audio file size (1 GB).
    /// Files larger than this are rejected to prevent OOM crashes.</summary>
    public const long MaxInputFileBytes = 1L * 1024 * 1024 * 1024;

    // ── Public export entry point ────────────────────────────────────────

    /// <summary>
    /// Export a recording to the specified format with configurable parameters.
    /// </summary>
    public static bool ExportRecording(RecordingHistoryItem item, string outputPath, ExportOptions options)
    {
        try
        {
            if (!File.Exists(item.FilePath)) return false;

            return options.Format switch
            {
                ExportFormat.Wav => ExportWav(item.FilePath, outputPath, options),
                ExportFormat.Mp3 => ExportMp3(item.FilePath, outputPath, options),
                ExportFormat.Flac => ExportFlac(item.FilePath, outputPath, options),
                ExportFormat.Ogg => ExportOgg(item.FilePath, outputPath, options),
                ExportFormat.Aac => ExportAac(item.FilePath, outputPath, options),
                _ => false
            };
        }
        catch (Exception ex)
        {
            AppLog.Warning(ex, "RecordingExporter: ExportRecording failed for '{Path}'", item.FilePath);
            return false;
        }
    }

    /// <summary>
    /// Backward-compatible overload using ExportFormat enum with default parameters.
    /// </summary>
    public static bool ExportRecording(RecordingHistoryItem item, string outputPath, ExportFormat format)
    {
        var options = new ExportOptions { Format = format };
        return ExportRecording(item, outputPath, options);
    }

    // ── Format-specific exporters ────────────────────────────────────────

    /// <summary>
    /// Export to WAV with configurable sample rate, bit depth, and channels.
    /// The intermediate buffers are allocated once and reused across the read loop.
    /// </summary>
    private static bool ExportWav(string sourcePath, string outputPath, ExportOptions options)
    {
        using var reader = new AudioFileReader(sourcePath);

        WaveFormat targetFormat = options.BitDepth switch
        {
            16 => new WaveFormat(options.SampleRate, 16, options.Channels),
            24 => new WaveFormat(options.SampleRate, 24, options.Channels),
            _ => WaveFormat.CreateIeeeFloatWaveFormat(options.SampleRate, options.Channels)
        };

        IWaveProvider source = reader;
        if (reader.WaveFormat.SampleRate != options.SampleRate)
            source = new MediaFoundationResampler(source, targetFormat);

        bool needsMonoToStereo = reader.WaveFormat.Channels == 1 && options.Channels == 2;

        const int BUFFER_FRAMES = 4096;
        var byteBuffer = new byte[BUFFER_FRAMES * 4];
        var floatBuffer = new float[BUFFER_FRAMES];
        var stereoBuffer = new float[BUFFER_FRAMES * 2];

        using var writer = new WaveFileWriter(outputPath, targetFormat);

        int bytesRead;
        while ((bytesRead = source.Read(byteBuffer, 0, byteBuffer.Length)) > 0)
        {
            int samplesRead = bytesRead / 4;
            Buffer.BlockCopy(byteBuffer, 0, floatBuffer, 0, samplesRead * 4);

            if (needsMonoToStereo)
            {
                for (int i = 0; i < samplesRead; i++)
                {
                    stereoBuffer[i * 2]     = floatBuffer[i];
                    stereoBuffer[i * 2 + 1] = floatBuffer[i];
                }
                writer.WriteSamples(stereoBuffer, 0, samplesRead * 2);
            }
            else
            {
                writer.WriteSamples(floatBuffer, 0, samplesRead);
            }
        }

        return true;
    }

    /// <summary>Export to MP3 with configurable bitrate and CBR/VBR mode.</summary>
    private static bool ExportMp3(string sourcePath, string outputPath, ExportOptions options)
    {
        using var reader = new AudioFileReader(sourcePath);

        if (options.UseVBR)
        {
            int vbrQuality = (int)((1.0f - options.VorbisQuality) * 9);
            vbrQuality = Math.Clamp(vbrQuality, 0, 9);
            using var writer = new LameMP3FileWriter(outputPath, reader.WaveFormat, vbrQuality);
            reader.CopyTo(writer);
        }
        else
        {
            using var writer = new LameMP3FileWriter(outputPath, reader.WaveFormat, options.Bitrate);
            reader.CopyTo(writer);
        }

        return true;
    }

    /// <summary>
    /// Export to FLAC. NAudio.Flac is not .NET 8 compatible, so this writes a
    /// 16-bit PCM WAV as a lossless fallback container.
    /// </summary>
    private static bool ExportFlac(string sourcePath, string outputPath, ExportOptions options)
        => ExportWav(sourcePath, outputPath, new ExportOptions
        {
            Format = ExportFormat.Wav,
            SampleRate = options.SampleRate,
            BitDepth = 16,
            Channels = options.Channels
        });

    /// <summary>Export to OGG Vorbis format (placeholder — no compatible library yet).</summary>
    private static bool ExportOgg(string sourcePath, string outputPath, ExportOptions options)
        => false;

    /// <summary>Export to AAC/M4A format using Windows MediaFoundation.</summary>
    private static bool ExportAac(string sourcePath, string outputPath, ExportOptions options)
    {
        using var reader = new AudioFileReader(sourcePath);
        try
        {
            MediaFoundationEncoder.EncodeToAac(reader, outputPath, options.AacBitrate * 1000);
            return true;
        }
        catch (Exception ex)
        {
            AppLog.Warning(ex, "RecordingExporter: ExportAac failed");
            return false;
        }
    }

    // ── Audio file I/O (used by batch processing) ───────────────────────

    /// <summary>
    /// Read an audio file and resample to engine rate / mono float.
    /// Reads directly into a sized buffer to avoid repeated intermediate copies.
    /// </summary>
    public static float[] ReadAudioFile(string path)
    {
        try
        {
            // Guard against OOM from oversized files.
            var fileInfo = new FileInfo(path);
            if (fileInfo.Length > MaxInputFileBytes)
            {
                AppLog.Warning("RecordingExporter: ReadAudioFile rejected '{Path}' — file size {Size} exceeds {Limit} byte limit",
                    path, fileInfo.Length, MaxInputFileBytes);
                return Array.Empty<float>();
            }

            using var reader = new AudioFileReader(path);
            var resampler = new MediaFoundationResampler(reader,
                WaveFormat.CreateIeeeFloatWaveFormat(SAMPLE_RATE, CHANNELS));

            using (resampler)
            {
                var srcFmt = reader.WaveFormat;
                long outputBytes = (long)reader.Length
                    * SAMPLE_RATE * CHANNELS
                    / (srcFmt.SampleRate * srcFmt.Channels);
                long totalBytes = outputBytes;
                int totalSamples = (int)(totalBytes / 4);

                if (totalSamples <= 0)
                {
                    var allBytes = new MemoryStream();
                    const int CHUNK_BYTES = 4 * 1024 * 1024;
                    var buffer = new byte[CHUNK_BYTES];
                    int bytesRead;
                    while ((bytesRead = resampler.Read(buffer, 0, buffer.Length)) > 0)
                        allBytes.Write(buffer, 0, bytesRead);
                    byte[] rawBytes = allBytes.ToArray();
                    int sampleCount = rawBytes.Length / 4;
                    var samples = new float[sampleCount];
                    Buffer.BlockCopy(rawBytes, 0, samples, 0, sampleCount * 4);
                    return samples;
                }

                var result = new float[totalSamples];
                var byteResult = new byte[totalBytes];
                int offset = 0;
                int remaining = (int)totalBytes;
                var readBuf = new byte[Math.Min(remaining, 4 * 1024 * 1024)];
                while (remaining > 0)
                {
                    int toRead = Math.Min(remaining, readBuf.Length);
                    int read = resampler.Read(readBuf, 0, toRead);
                    if (read == 0) break;
                    Buffer.BlockCopy(readBuf, 0, byteResult, offset, read);
                    offset += read;
                    remaining -= read;
                }
                Buffer.BlockCopy(byteResult, 0, result, 0, offset);
                if (offset < totalBytes)
                    Array.Resize(ref result, offset / 4);
                return result;
            }
        }
        catch (Exception ex)
        {
            AppLog.Warning(ex, "RecordingExporter: ReadAudioFile failed for '{Path}'", path);
            return Array.Empty<float>();
        }
    }

    /// <summary>Write float samples as a WAV file at engine rate / mono.</summary>
    public static void WriteWavFile(string path, float[] samples)
    {
        var format = WaveFormat.CreateIeeeFloatWaveFormat(SAMPLE_RATE, CHANNELS);
        using var writer = new WaveFileWriter(path, format);
        writer.WriteSamples(samples, 0, samples.Length);
    }

    // ── Batch processing ─────────────────────────────────────────────────

    /// <summary>
    /// Batch-process multiple audio files through a DSP chain built from a voice profile.
    /// </summary>
    public static List<BatchProcessResult> BatchProcess(
        string[] inputPaths,
        VoiceProfile preset,
        string outputFolder,
        ExportFormat format,
        IProgress<BatchProgress>? progress = null)
    {
        var results = new List<BatchProcessResult>();
        var chain = preset.CreateDSPChain();
        Directory.CreateDirectory(outputFolder);

        for (int i = 0; i < inputPaths.Length; i++)
        {
            var inputPath = inputPaths[i];
            progress?.Report(new BatchProgress(i + 1, inputPaths.Length, Path.GetFileName(inputPath)));

            try
            {
                // Pre-check file size before attempting to read.
                var inputSize = new FileInfo(inputPath).Length;
                if (inputSize > MaxInputFileBytes)
                {
                    results.Add(new BatchProcessResult
                    {
                        InputFile = inputPath, Success = false,
                        Error = $"File too large ({inputSize / (1024 * 1024)} MB). Maximum is {MaxInputFileBytes / (1024 * 1024)} MB."
                    });
                    continue;
                }

                chain.Reset();
                var samples = ReadAudioFile(inputPath);
                if (samples.Length == 0)
                {
                    results.Add(new BatchProcessResult
                    {
                        InputFile = inputPath, Success = false, Error = "Failed to read audio file"
                    });
                    continue;
                }

                var span = samples.AsSpan();
                chain.Process(span);

                string outFile = Path.Combine(outputFolder,
                    Path.GetFileNameWithoutExtension(inputPath) + "_vonvert.wav");
                WriteWavFile(outFile, samples.ToArray());

                results.Add(new BatchProcessResult
                {
                    InputFile = inputPath, OutputFile = outFile, Success = true
                });
            }
            catch (Exception ex)
            {
                results.Add(new BatchProcessResult
                {
                    InputFile = inputPath, Success = false, Error = ex.Message
                });
            }
        }

        return results;
    }
}
