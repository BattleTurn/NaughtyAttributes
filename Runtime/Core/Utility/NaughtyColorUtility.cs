
using UnityEngine;

namespace NaughtyAttributes
{
    public static class NaughtyColorUtility
    {
        public static Color GetColor(string hex, Color fallback)
        {
            if (!string.IsNullOrEmpty(hex))
            {
                if (ColorUtility.TryParseHtmlString(hex, out var c))
                    return c;
            }

            return fallback; // fallback default, e.g., Color.white
        }

        public static bool TryParse(object input, out Color c)
        {
            if (input is Color col) { c = col; return true; }
            if (input is string s && ColorUtility.TryParseHtmlString(s, out var parsed))
            {
                c = parsed; return true;
            }
            c = Color.white;
            return false;
        }

    }
}