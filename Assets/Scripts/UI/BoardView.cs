using Riptide.Core;
using UnityEngine;

namespace Riptide.UI
{
    /// <summary>
    /// Renders the 9x12 grid purely from GameState (GDD 8.2: views never re-derive
    /// rules). Exposes per-cell truth accessors so the play-mode acceptance test
    /// can assert view == sim after every move.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        private readonly SpriteRenderer[] renderers = new SpriteRenderer[BoardSpec.CellCount];
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
            for (int row = 0; row < BoardSpec.Height; row++)
            {
                for (int col = 0; col < BoardSpec.Width; col++)
                {
                    var cellGo = new GameObject($"cell_{col}_{row}");
                    cellGo.transform.SetParent(transform, false);
                    cellGo.transform.localPosition = BoardLayout.CellToWorld(col, row);
                    cellGo.transform.localScale = Vector3.one * 0.94f;
                    var sr = cellGo.AddComponent<SpriteRenderer>();
                    sr.sprite = SpriteFactory.Cell();
                    sr.color = Palette.EmptyCell;
                    sr.sortingOrder = 10;
                    renderers[BoardSpec.IndexOf(col, row)] = sr;
                }
            }
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
                    sr.transform.localScale = Vector3.one * 0.94f;
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
                            sr.sprite = SpriteFactory.Creature();
                            sr.color = Palette.CreatureColor(cell.Id);
                            break;
                        default:
                            sr.sprite = SpriteFactory.Cell();
                            sr.color = Palette.EmptyCell;
                            break;
                    }
                }
            }
        }

        public CellKind KindAt(int col, int row) => kinds[BoardSpec.IndexOf(col, row)];

        public Color ColorAt(int col, int row) => renderers[BoardSpec.IndexOf(col, row)].color;

        public SpriteRenderer RendererAt(int col, int row) => renderers[BoardSpec.IndexOf(col, row)];
    }
}
