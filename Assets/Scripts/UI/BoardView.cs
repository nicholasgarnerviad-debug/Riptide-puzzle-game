using Riptide.Core;
using UnityEngine;

namespace Riptide.UI
{
    /// <summary>
    /// Renders the 9x12 grid purely from GameState (GDD 8.2: views never re-derive
    /// rules). Exposes per-cell truth accessors so the play-mode acceptance test
    /// can assert view == sim after every move. Tidal Pop: blocks carry a white
    /// gloss overlay and trapped creatures sit on a light rescue tile.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        private const float CellScale = 0.94f;

        private readonly SpriteRenderer[] renderers = new SpriteRenderer[BoardSpec.CellCount];
        private readonly SpriteRenderer[] glosses = new SpriteRenderer[BoardSpec.CellCount];
        private readonly SpriteRenderer[] backings = new SpriteRenderer[BoardSpec.CellCount];
        private readonly CellKind[] kinds = new CellKind[BoardSpec.CellCount];

        public static BoardView Create(Transform parent)
        {
            var go = new GameObject("BoardView");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<BoardView>();
            view.Build();
            return view;
        }

        private void Build()
        {
            Color glossTint = ThemeRuntime.Color("panel.light");
            Color backingTint = ThemeRuntime.Color("creature.cell");
            for (int row = 0; row < BoardSpec.Height; row++)
            {
                for (int col = 0; col < BoardSpec.Width; col++)
                {
                    int index = BoardSpec.IndexOf(col, row);
                    var cellGo = new GameObject($"cell_{col}_{row}");
                    cellGo.transform.SetParent(transform, false);
                    cellGo.transform.localPosition = BoardLayout.CellToWorld(col, row);
                    cellGo.transform.localScale = Vector3.one * CellScale;
                    var sr = cellGo.AddComponent<SpriteRenderer>();
                    sr.sprite = SpriteFactory.CellWell();
                    sr.color = Palette.EmptyCell;
                    sr.sortingOrder = 10;
                    renderers[index] = sr;

                    glosses[index] = Child(cellGo.transform, "gloss", SpriteFactory.CellGloss(), glossTint, 11);
                    backings[index] = Child(cellGo.transform, "backing", SpriteFactory.Cell(), backingTint, 9);
                }
            }
        }

        private static SpriteRenderer Child(Transform parent, string name, Sprite sprite, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            sr.enabled = false;
            return sr;
        }

        public void Render(GameState state)
        {
            for (int row = 0; row < BoardSpec.Height; row++)
            {
                for (int col = 0; col < BoardSpec.Width; col++)
                {
                    int index = BoardSpec.IndexOf(col, row);
                    Cell cell = state.CellAt(col, row);
                    kinds[index] = cell.Kind;
                    SpriteRenderer sr = renderers[index];
                    sr.transform.localScale = Vector3.one * CellScale;
                    glosses[index].enabled = cell.Kind == CellKind.Block;
                    backings[index].enabled = cell.Kind == CellKind.Creature;
                    switch (cell.Kind)
                    {
                        case CellKind.Block:
                            sr.sprite = SpriteFactory.Cell();
                            sr.color = Palette.BlockColor(cell.Id);
                            break;
                        case CellKind.Coral:
                            // §8: coral is textured, not just recolored.
                            sr.sprite = SpriteFactory.CoralCell();
                            sr.color = Palette.Coral;
                            break;
                        case CellKind.Creature:
                            sr.sprite = CreatureSprites.For(cell.Id);
                            sr.color = Palette.CreatureColor(cell.Id);
                            break;
                        default:
                            // Empties are inset wells; pieces pop out of them.
                            sr.sprite = SpriteFactory.CellWell();
                            sr.color = Palette.EmptyCell;
                            break;
                    }
                }
            }
        }

        /// <summary>Shows/hides a cell's gloss overlay (animations that swap a cell's sprite).</summary>
        public static void SetGloss(SpriteRenderer cell, bool on)
        {
            Transform gloss = cell.transform.Find("gloss");
            if (gloss != null)
            {
                gloss.GetComponent<SpriteRenderer>().enabled = on;
            }
        }

        public CellKind KindAt(int col, int row) => kinds[BoardSpec.IndexOf(col, row)];

        public Color ColorAt(int col, int row) => renderers[BoardSpec.IndexOf(col, row)].color;

        public SpriteRenderer RendererAt(int col, int row) => renderers[BoardSpec.IndexOf(col, row)];
    }
}
