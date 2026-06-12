using Riptide.Core;
using UnityEngine;

namespace Riptide.UI
{
    /// <summary>
    /// World-space atmosphere for the game scene (visual pass; research model:
    /// Tetris Effect's "The Deep" — sunbeams, depth, particles, none of it in the
    /// way of play): a brighter water column behind the board, angled god rays
    /// from the surface, and a soft elevation shadow under the board frame.
    /// All behind the board sorting-wise; clarity untouched.
    /// </summary>
    public static class GameSceneDressing
    {
        public static void Create(Transform parent)
        {
            var root = new GameObject("SceneDressing");
            root.transform.SetParent(parent, false);

            // Brighter water toward the surface, behind everything.
            var glowGo = new GameObject("surfaceGlow");
            glowGo.transform.SetParent(root.transform, false);
            glowGo.transform.position = new Vector3(0f, 12f, 0f);
            glowGo.transform.localScale = new Vector3(60f, -26f, 1f); // fade points down
            var glow = glowGo.AddComponent<SpriteRenderer>();
            glow.sprite = SpriteFactory.VerticalFade();
            glow.color = ThemeRuntime.Color("bg.oceanTop");
            glow.sortingOrder = -20;

            BuildRay(root.transform, x: -2.6f, tiltDeg: 12f, width: 4.5f);
            BuildRay(root.transform, x: 1.8f, tiltDeg: -7f, width: 3.0f);
            BuildRay(root.transform, x: 4.6f, tiltDeg: 16f, width: 2.2f);

            // Elevation: a soft dark halo behind the board frame.
            float centerY = (BoardLayout.BoardBottomY + BoardLayout.BoardTopY) * 0.5f;
            var shadowGo = new GameObject("frameShadow");
            shadowGo.transform.SetParent(root.transform, false);
            shadowGo.transform.position = new Vector3(0f, centerY - 0.25f, 0f);
            shadowGo.transform.localScale = new Vector3(
                (BoardSpec.Width + 2f * BoardLayout.FramePad + 1.6f) / 2f,
                (BoardSpec.Height + 2f * BoardLayout.FramePad + 1.6f) / 2f, 1f);
            var shadow = shadowGo.AddComponent<SpriteRenderer>();
            shadow.sprite = MenuSprites.SoftGlow();
            shadow.color = ThemeRuntime.Color("shadow.soft");
            shadow.sortingOrder = 4; // just under the frame fill (5)
        }

        private static void BuildRay(Transform root, float x, float tiltDeg, float width)
        {
            var rayGo = new GameObject("ray");
            rayGo.transform.SetParent(root, false);
            rayGo.transform.position = new Vector3(x, 13f, 0f);
            rayGo.transform.rotation = Quaternion.Euler(0f, 0f, tiltDeg);
            rayGo.transform.localScale = new Vector3(width, 7f, 1f);
            var ray = rayGo.AddComponent<SpriteRenderer>();
            ray.sprite = MenuSprites.LightRay();
            ray.color = ThemeRuntime.Color("ray.light");
            ray.sortingOrder = -15;
        }
    }
}
