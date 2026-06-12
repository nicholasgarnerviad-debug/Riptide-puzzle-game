using System;
using System.Collections.Generic;
using System.Text;

namespace Riptide.Core
{
    /// <summary>
    /// Mid-run save (SAVE_RESUME_DESIGN.md): the INPUTS of a run — mode identity,
    /// seed, complete move list, and the StateHash after the last move. Replaying
    /// the inputs through the deterministic engine reproduces the run exactly;
    /// state snapshots are never persisted.
    /// </summary>
    public sealed class RunRecord
    {
        public const int CurrentSchema = 1;

        /// <summary>"Voyage" | "Endless" | "Daily" — Core stays enum-agnostic; the
        /// Game layer owns GameMode.</summary>
        public string Mode { get; }

        /// <summary>Voyage only; 0 otherwise.</summary>
        public int Zone { get; }
        public int Level { get; }

        /// <summary>Daily only; 0 otherwise — locks the record to its calendar day.</summary>
        public long EpochDay { get; }

        public ulong Seed { get; }
        public IReadOnlyList<Move> Moves { get; }

        /// <summary>StateHash after the last recorded move (divergence guard).</summary>
        public ulong StateHashAfterMoves { get; }

        public RunRecord(string mode, int zone, int level, long epochDay, ulong seed,
            IReadOnlyList<Move> moves, ulong stateHashAfterMoves)
        {
            Mode = mode ?? throw new ArgumentNullException(nameof(mode));
            Zone = zone;
            Level = level;
            EpochDay = epochDay;
            Seed = seed;
            Moves = moves ?? throw new ArgumentNullException(nameof(moves));
            StateHashAfterMoves = stateHashAfterMoves;
        }

        public string Serialize()
        {
            var sb = new StringBuilder(256 + Moves.Count * 40);
            sb.Append("{\n");
            sb.Append($"  \"schema\": {CurrentSchema},\n");
            sb.Append($"  \"mode\": \"{Mode}\",\n");
            sb.Append($"  \"zone\": {Zone},\n");
            sb.Append($"  \"level\": {Level},\n");
            sb.Append($"  \"epochDay\": {EpochDay},\n");
            // ulongs travel as strings: FNV-1a 64 daily seeds and state hashes
            // exceed the JSON parser's signed-long number range.
            sb.Append($"  \"seed\": \"{Seed}\",\n");
            sb.Append($"  \"stateHash\": \"{StateHashAfterMoves}\",\n");
            sb.Append("  \"moves\": [");
            for (int i = 0; i < Moves.Count; i++)
            {
                sb.Append(i == 0 ? "\n    " : ",\n    ");
                sb.Append(SerializeMove(Moves[i]));
            }

            sb.Append("\n  ]\n}\n");
            return sb.ToString();
        }

        private static string SerializeMove(Move move) => move switch
        {
            PlaceMove p => $"{{ \"t\": \"place\", \"slot\": {p.TraySlot}, \"col\": {p.Target.Col}, \"row\": {p.Target.Row} }}",
            DrainPumpMove => "{ \"t\": \"drain\" }",
            BubblePopMove b => $"{{ \"t\": \"pop\", \"col\": {b.Target.Col}, \"row\": {b.Target.Row} }}",
            NewTideMove => "{ \"t\": \"newTide\" }",
            PieceSwapMove s => $"{{ \"t\": \"swap\", \"slot\": {s.TraySlot} }}",
            ContinueMove => "{ \"t\": \"continue\" }",
            _ => throw new InvalidOperationException($"Unserializable move type {move.GetType().Name}"),
        };

        /// <summary>Parses a record; throws ContentException on any malformation —
        /// callers translate that into the graceful-discard path.</summary>
        public static RunRecord Parse(string json)
        {
            try
            {
                JsonObject root = JsonParser.Parse(json).AsObject();
                int schema = root.Require("schema").AsInt();
                if (schema > CurrentSchema)
                {
                    throw new JsonParseException($"Run record schema {schema} is newer than {CurrentSchema}", root.Line, root.Column);
                }

                JsonArray movesArray = root.Require("moves").AsArray();
                var moves = new List<Move>(movesArray.Count);
                foreach (JsonValue item in movesArray.Items)
                {
                    moves.Add(ParseMove(item.AsObject()));
                }

                return new RunRecord(
                    root.Require("mode").AsString(),
                    root.Require("zone").AsInt(),
                    root.Require("level").AsInt(),
                    root.Require("epochDay").AsLong(),
                    ParseUlong(root.Require("seed")),
                    moves,
                    ParseUlong(root.Require("stateHash")));
            }
            catch (JsonParseException ex)
            {
                throw new ContentException("riptide_run.json", ex.Message);
            }
        }

        private static ulong ParseUlong(JsonValue node)
        {
            string raw = node.AsString();
            return ulong.TryParse(raw, System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out ulong value)
                ? value
                : throw new JsonParseException($"'{raw}' is not a ulong", node.Line, node.Column);
        }

        private static Move ParseMove(JsonObject obj)
        {
            string type = obj.Require("t").AsString();
            switch (type)
            {
                case "place":
                    return new PlaceMove(obj.Require("slot").AsInt(),
                        new GridPos(obj.Require("col").AsInt(), obj.Require("row").AsInt()));
                case "drain":
                    return new DrainPumpMove();
                case "pop":
                    return new BubblePopMove(
                        new GridPos(obj.Require("col").AsInt(), obj.Require("row").AsInt()));
                case "newTide":
                    return new NewTideMove();
                case "swap":
                    return new PieceSwapMove(obj.Require("slot").AsInt());
                case "continue":
                    return new ContinueMove();
                default:
                    throw new JsonParseException($"Unknown move type '{type}'", obj.Line, obj.Column);
            }
        }
    }

    public enum RunReplayStatus
    {
        Ok,
        Diverged,
        IllegalMove,
    }

    public readonly struct RunReplayResult
    {
        public RunReplayStatus Status { get; }
        public GameState? State { get; }

        public RunReplayResult(RunReplayStatus status, GameState? state)
        {
            Status = status;
            State = state;
        }
    }

    /// <summary>
    /// Replays a RunRecord against a rebuilt config: every move re-applied through
    /// SimEngine, then the end hash compared to the recorded one. Any mismatch —
    /// content drift between app versions, corruption, an illegal move — reports
    /// a non-Ok status and the caller discards gracefully (design §6).
    /// </summary>
    public static class RunReplay
    {
        public static RunReplayResult Rebuild(LevelConfig config, RunRecord record)
        {
            GameState state = GameState.NewGame(config, record.Seed);
            foreach (Move move in record.Moves)
            {
                try
                {
                    state = SimEngine.ApplyMove(state, move).Next;
                }
                catch (InvalidMoveException)
                {
                    return new RunReplayResult(RunReplayStatus.IllegalMove, null);
                }
            }

            return StateHash.Compute(state) == record.StateHashAfterMoves
                ? new RunReplayResult(RunReplayStatus.Ok, state)
                : new RunReplayResult(RunReplayStatus.Diverged, null);
        }
    }
}
