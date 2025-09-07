using System;
using UnityEngine;

namespace NaughtyAttributes
{
    /// <summary>
    /// Defines the visible range for an AnimationCurve field and an optional color for the curve.
    /// Reworked to remove dependency on EColor; now uses UnityEngine.Color directly.
    /// If a color is not specified the drawer will fallback to Color.green (Unity's default in the original implementation when EColor.Clear was used).
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class CurveRangeAttribute : DrawerAttribute
    {
        public Vector2 Min { get; private set; }
        public Vector2 Max { get; private set; }
        public bool HasColor { get; private set; }
        public Color Color { get; private set; }

        public CurveRangeAttribute(Vector2 min, Vector2 max)
        {
            Min = min;
            Max = max;
            HasColor = false;
            Color = Color.green; // fallback
        }

        public CurveRangeAttribute(Vector2 min, Vector2 max, float r, float g, float b, float a = 1f)
        {
            Min = min; Max = max; HasColor = true; Color = new Color(r, g, b, a);
        }

        public CurveRangeAttribute(float minX, float minY, float maxX, float maxY)
            : this(new Vector2(minX, minY), new Vector2(maxX, maxY))
        { }

        public CurveRangeAttribute(float minX, float minY, float maxX, float maxY, float r, float g, float b, float a = 1f)
            : this(new Vector2(minX, minY), new Vector2(maxX, maxY), r, g, b, a)
        { }

        /// <summary>
        /// Default: range 0..1 no custom color (will display green)
        /// </summary>
        public CurveRangeAttribute()
            : this(Vector2.zero, Vector2.one)
        { }
    }
}
