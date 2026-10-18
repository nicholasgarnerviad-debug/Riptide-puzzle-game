using UnityEngine;
using UnityEngine.UI;

namespace Riptide.UI
{
    /// <summary>
    /// Spec §1: binds a Graphic's color to a theme key. The literal-scan editor
    /// script flags any Graphic that carries a hand-set color without one of these.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ThemedElement : MonoBehaviour
    {
        public string colorKey = "";

        private void Awake() => Apply();

        public void Apply()
        {
            if (string.IsNullOrEmpty(colorKey))
            {
                return;
            }

            var graphic = GetComponent<Graphic>();
            if (graphic != null)
            {
                graphic.color = ThemeRuntime.Color(colorKey);
            }
        }

        public static ThemedElement Bind(GameObject target, string key)
        {
            var element = target.GetComponent<ThemedElement>();
            if (element == null)
            {
                element = target.AddComponent<ThemedElement>();
            }

            element.colorKey = key;
            element.Apply();
            return element;
        }
    }
}
