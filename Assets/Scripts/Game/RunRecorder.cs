using System;
using System.Collections.Generic;
using System.IO;
using Riptide.Core;
using UnityEngine;

namespace Riptide.Game
{
    /// <summary>
    /// Mid-run persistence (SAVE_RESUME_DESIGN.md): writes the RunRecord after
    /// EVERY applied move (atomic temp-swap, own file — never the main save), so
    /// a process death at any point leaves a resumable run. IO failures degrade
    /// to "no resume", never to a gameplay crash.
    /// </summary>
    public sealed class RunRecorder
    {
        private readonly string path;
        private readonly string tempPath;
        private readonly List<Move> moves = new List<Move>();
        private string mode = "";
        private int zone;
        private int level;
        private long epochDay;
        private ulong seed;
        private bool active;

        public RunRecorder(string? overridePath = null)
        {
            path = overridePath ?? Path.Combine(Application.persistentDataPath, "riptide_run.json");
            tempPath = path + ".tmp";
        }

        /// <summary>A fresh run starts: previous pending record (if any) is gone.</summary>
        public void Begin(string runMode, int runZone, int runLevel, long runEpochDay, ulong runSeed)
        {
            mode = runMode;
            zone = runZone;
            level = runLevel;
            epochDay = runEpochDay;
            seed = runSeed;
            moves.Clear();
            active = true;
            DeleteFile();
        }

        /// <summary>Resume continues appending to the validated pending record.</summary>
        public void Resume(RunRecord record)
        {
            mode = record.Mode;
            zone = record.Zone;
            level = record.Level;
            epochDay = record.EpochDay;
            seed = record.Seed;
            moves.Clear();
            moves.AddRange(record.Moves);
            active = true;
        }

        /// <summary>Same-frame persistence after an applied move (design §4).</summary>
        public void Append(Move move, GameState next)
        {
            if (!active)
            {
                return;
            }

            moves.Add(move);
            var record = new RunRecord(mode, zone, level, epochDay, seed, moves, StateHash.Compute(next));
            try
            {
                File.WriteAllText(tempPath, record.Serialize());
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                File.Move(tempPath, path);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Riptide run record write failed (resume disabled this run): {ex.Message}");
            }
        }

        /// <summary>Run concluded (outcome processed) or explicitly abandoned.</summary>
        public void Finish()
        {
            active = false;
            DeleteFile();
        }

        /// <summary>Reads a pending record; any malformation deletes it and reports
        /// none — the graceful-discard path (design §6).</summary>
        public RunRecord? ReadPending()
        {
            try
            {
                if (!File.Exists(path))
                {
                    return null;
                }

                return RunRecord.Parse(File.ReadAllText(path));
            }
            catch (Exception)
            {
                DeleteFile();
                return null;
            }
        }

        public void DeleteFile()
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception)
            {
                // A locked file just means a stale resume offer next boot — harmless.
            }
        }
    }
}
