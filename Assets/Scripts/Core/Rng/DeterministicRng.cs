using System;

namespace Riptide.Core
{
    /// <summary>
    /// Deterministic xorshift128+ RNG (Vigna reference variant, shifts 23/18/5),
    /// seeded via splitmix64. Immutable: every draw returns the advanced generator
    /// alongside the value. GDD 8.3: the only randomness source in the sim; the
    /// state travels inside GameState so identical (levelDef, seed, moves) replay
    /// identically. Phase 2 golden files pin this exact stream forever.
    /// </summary>
    public readonly struct DeterministicRng : IEquatable<DeterministicRng>
    {
        public ulong S0 { get; }
        public ulong S1 { get; }

        public DeterministicRng(ulong s0, ulong s1)
        {
            // xorshift128+ degenerates on the all-zero state.
            if (s0 == 0UL && s1 == 0UL)
            {
                s1 = 1UL;
            }

            S0 = s0;
            S1 = s1;
        }

        public static DeterministicRng FromSeed(ulong seed)
        {
            ulong x = seed;
            ulong s0 = SplitMix64(ref x);
            ulong s1 = SplitMix64(ref x);
            return new DeterministicRng(s0, s1);
        }

        private static ulong SplitMix64(ref ulong x)
        {
            ulong z = x += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        public RngDraw NextUInt64()
        {
            ulong a = S0;
            ulong b = S1;
            ulong result = a + b;
            a ^= a << 23;
            ulong newS1 = a ^ b ^ (a >> 18) ^ (b >> 5);
            return new RngDraw(new DeterministicRng(b, newS1), result);
        }

        /// <summary>Unbiased draw in [0, maxExclusive) via modulo rejection.</summary>
        public RngIntDraw NextInt(int maxExclusive)
        {
            if (maxExclusive <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), "maxExclusive must be positive.");
            }

            ulong n = (ulong)maxExclusive;
            ulong threshold = (0UL - n) % n;
            DeterministicRng rng = this;
            while (true)
            {
                RngDraw draw = rng.NextUInt64();
                rng = draw.Rng;
                if (draw.Value >= threshold)
                {
                    return new RngIntDraw(rng, (int)(draw.Value % n));
                }
            }
        }

        public bool Equals(DeterministicRng other) => S0 == other.S0 && S1 == other.S1;

        public override bool Equals(object? obj) => obj is DeterministicRng other && Equals(other);

        public override int GetHashCode() => (S0 ^ (S1 * 0x9E3779B97F4A7C15UL)).GetHashCode();

        public override string ToString() => $"Rng({S0:X16},{S1:X16})";
    }

    /// <summary>A 64-bit draw plus the advanced generator.</summary>
    public readonly struct RngDraw
    {
        public DeterministicRng Rng { get; }
        public ulong Value { get; }

        public RngDraw(DeterministicRng rng, ulong value)
        {
            Rng = rng;
            Value = value;
        }
    }

    /// <summary>A bounded int draw plus the advanced generator.</summary>
    public readonly struct RngIntDraw
    {
        public DeterministicRng Rng { get; }
        public int Value { get; }

        public RngIntDraw(DeterministicRng rng, int value)
        {
            Rng = rng;
            Value = value;
        }
    }
}
