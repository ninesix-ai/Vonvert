// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2026 ninesix-ai studio

// Regression guard for the PresetIndex data race.
// The debounced Save() runs on a thread-pool timer thread and enumerates
// _entries / _recentList while another thread mutates them.  Without a shared
// lock this throws "Collection was modified" during JSON serialization
// (surfaced as the SaveFailed event) or writes a corrupt index.  This test
// hammers mutations and saves from many threads and asserts no save fails and
// the on-disk index reloads cleanly.

namespace Vonvert.Tests.Presets;

using System.IO;
using Vonvert.Engine.PresetLibrary;
using Xunit;

public class PresetIndexConcurrencyTests
{
    [Fact(DisplayName = "PIDX-RACE: concurrent mutations + saves never fail or corrupt")]
    public void ConcurrentMutationsAndSaves_DoNotFail()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pidx-race-{Guid.NewGuid():N}.json");
        try
        {
            var index = new PresetIndex(path);
            int saveFailures = 0;
            index.SaveFailed += _ => Interlocked.Increment(ref saveFailures);

            // Enough entries that serialization has a wide race window.
            for (int i = 0; i < 2000; i++) index.AddEntry($"seed{i}");
            index.Save(); // materialize the file so later saves exercise File.Replace

            const int writers = 8;
            const int iterations = 400;

            var writerTasks = Enumerable.Range(0, writers).Select(w => Task.Run(() =>
            {
                var rng = new Random(w);
                for (int it = 0; it < iterations; it++)
                {
                    var name = $"w{w}-{it}";
                    index.AddEntry(name);
                    index.RecordUsage(name);
                    index.ToggleFavorite(name);
                    index.UpdateMetadata(name, m => m.Notes = "x");   // in-place item edit
                    if (it % 3 == 0) index.RemoveEntry(name);
                    if (it % 5 == 0) index.AddEntry($"seed{rng.Next(2000)}"); // idempotent dup
                }
            })).ToArray();

            var saverTasks = Enumerable.Range(0, 3).Select(_ => Task.Run(() =>
            {
                for (int it = 0; it < 200; it++)
                {
                    index.Save();
                    Thread.Sleep(1);
                }
            })).ToArray();

            Assert.True(
                Task.WaitAll(writerTasks.Concat(saverTasks).ToArray(), TimeSpan.FromSeconds(60)),
                "PresetIndex concurrency test timed out");

            // Unlocked code surfaces "Collection was modified" here as a SaveFailed.
            Assert.Equal(0, saveFailures);

            // Final save + reload must round-trip a valid, non-empty index.
            index.Save();
            var reloaded = new PresetIndex(path);
            Assert.NotEmpty(reloaded.Entries);
        }
        finally
        {
            foreach (var f in new[] { path, path + ".bak", path + ".tmp", path + ".corrupt" })
                try { if (File.Exists(f)) File.Delete(f); } catch { /* temp cleanup is best-effort */ }
        }
    }
}
