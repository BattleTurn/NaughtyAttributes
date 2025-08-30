
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
    }
}