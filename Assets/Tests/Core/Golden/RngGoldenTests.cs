using NUnit.Framework;
using Riptide.Core;

namespace Riptide.Core.Tests
{
    /// <summary>
    /// ███ GOLDEN PINS — master prompt rule 5 ███
    /// These constants pin the xorshift128+ stream, splitmix64 seeding, deal draw
    /// order, state hash, and daily-seed derivation FOREVER. If a change breaks
    /// this fixture, that is a STOP-and-ask event for Nick, not a file update:
    /// every replay, daily board, and share card in the wild would break.
    /// Generated 2026-06-11 by a one-shot tool (since deleted) from the same sources.
    /// </summary>
    [TestFixture]
    public sealed class RngGoldenTests
    {
        private static readonly ulong[] Seeds = { 1UL, 42UL, 2026UL, 0xDEADBEEFUL, 123456789UL };

        private static readonly ulong[][] GoldenDraws =
        {
            new[] { 0x4FF5BB8DEE914928UL, 0x9B3DF27E919F6A0CUL, 0x913C0B97EDDC4551UL, 0xB8745298B438E33CUL, 0xEDAA33964D510CA2UL, 0xEA5C65419FF1ECA3UL, 0x364C9E32A6644D38UL, 0x293195A892628D7FUL },
            new[] { 0xE6C71559E2525F98UL, 0xB058533F2DE1E247UL, 0xB9CE3F9922D00C78UL, 0xE388DBC5079ED02BUL, 0xF13F58B2DFA8A228UL, 0x28ABF8CCF4B5D58EUL, 0x7AF7D9A85738B436UL, 0xE936886B2ED34708UL },
            new[] { 0x5458E8167EC9D280UL, 0xE56A7F4312F527AEUL, 0x963087B3F2D06B44UL, 0x50745971479B769DUL, 0xA013F4E853EF8CBAUL, 0x9649FC37403D1CB0UL, 0x732832F7845932A4UL, 0xF72567EBCF4FD5BEUL },
            new[] { 0x29382340AA6AF4BDUL, 0xF419B1F1DB3C4CC4UL, 0xE9590EB5FCF72857UL, 0xF41E96B6459E0EC9UL, 0xA92B88B1288C3B9BUL, 0xD4BAFCD01DD92296UL, 0x0B0793F823166A81UL, 0xD7ECC64904490027UL },
            new[] { 0x9CCE51F1775D64A7UL, 0xB2799E5292FB85AAUL, 0xF8BD68BA55D995F1UL, 0x2CEDCC8721A86D8DUL, 0x7C6BA06C5FF5D2BEUL, 0x0FAECDD152FB319BUL, 0x59CAE80434D54850UL, 0xEC5B3D83FDE32F26UL },
        };

        private static readonly (PieceId piece, byte color)[][] GoldenTrays =
        {
            new[] { (PieceId.L3MissingSW, (byte)4), (PieceId.O4, (byte)4), (PieceId.I4H, (byte)1) },
            new[] { (PieceId.I3V, (byte)5), (PieceId.T4, (byte)1), (PieceId.L3MissingSW, (byte)0) },
            new[] { (PieceId.T4, (byte)0), (PieceId.S4, (byte)5), (PieceId.I5H, (byte)4) },
            new[] { (PieceId.L3MissingNE, (byte)2), (PieceId.I3H, (byte)1), (PieceId.I5V, (byte)4) },
            new[] { (PieceId.I3H, (byte)0), (PieceId.O4, (byte)3), (PieceId.L4, (byte)3) },
        };

        private static readonly ulong[] GoldenHashes =
        {
            0x89FBC6814C20501EUL,
            0x60FF76DE24401A30UL,
            0x1E78D2560614E00AUL,
            0xE1E87BCBF2672FE3UL,
            0xF17F09A6ACC4830EUL,
        };

        /// <summary>Frozen — independent of TestKit so fixture drift can never move the pins.</summary>
        private static LevelConfig GoldenConfig()
        {
            var weights = new int[PieceCatalog.PieceCount];
            for (int i = 0; i < weights.Length; i++)
            {
                weights[i] = 1;
            }

            var scoring = new ScoringConfig(1, 80, 2, 1, 5, 250, 250, 30, 5, false);
            return new LevelConfig(1, 1, 8, 0, 8, 6, weights, scoring, GoalSet.None);
        }

        [Test]
        public void RawDrawStream_MatchesGolden_ForAllFiveSeeds()
        {
            for (int s = 0; s < Seeds.Length; s++)
            {
                var rng = DeterministicRng.FromSeed(Seeds[s]);
                for (int i = 0; i < GoldenDraws[s].Length; i++)
                {
                    RngDraw draw = rng.NextUInt64();
                    rng = draw.Rng;
                    Assert.That(draw.Value, Is.EqualTo(GoldenDraws[s][i]),
                        $"seed {Seeds[s]} draw {i}: the RNG stream changed — STOP, do not update this file (rule 5)");
                }
            }
        }

        [Test]
        public void NewGameTrays_MatchGolden_ForAllFiveSeeds()
        {
            for (int s = 0; s < Seeds.Length; s++)
            {
                GameState state = GameState.NewGame(GoldenConfig(), Seeds[s]);
                for (int i = 0; i < BoardSpec.TraySize; i++)
                {
                    TrayPiece piece = state.TrayAt(i)!.Value;
                    Assert.That(piece.Piece, Is.EqualTo(GoldenTrays[s][i].piece),
                        $"seed {Seeds[s]} slot {i}: deal stream changed — STOP (rule 5)");
                    Assert.That(piece.ColorId, Is.EqualTo(GoldenTrays[s][i].color),
                        $"seed {Seeds[s]} slot {i} color: deal stream changed — STOP (rule 5)");
                }
            }
        }

        [Test]
        public void NewGameStateHashes_MatchGolden_ForAllFiveSeeds()
        {
            for (int s = 0; s < Seeds.Length; s++)
            {
                GameState state = GameState.NewGame(GoldenConfig(), Seeds[s]);
                Assert.That(state.ComputeHash(), Is.EqualTo(GoldenHashes[s]),
                    $"seed {Seeds[s]}: state hash changed — STOP (rule 5)");
            }
        }

        [Test]
        public void DailySeeds_MatchGolden_ForPinnedDates()
        {
            Assert.That(DailySeed.For(2026, 6, 11), Is.EqualTo(0xAC2F5FE44B04C596UL));
            Assert.That(DailySeed.For(2026, 1, 1), Is.EqualTo(0x597025BEBDBD8370UL));
            Assert.That(DailySeed.For(2026, 12, 31), Is.EqualTo(0xDF9A7DEB86292FDFUL));
            Assert.That(DailySeed.For(2027, 2, 28), Is.EqualTo(0xBB20D8B5E548FE0DUL));
            Assert.That(DailySeed.For(2030, 7, 4), Is.EqualTo(0x6660BF562EF7FED6UL));
        }
    }
}
