using System;
using UnityEngine;

namespace NaughtyAttributes
{
    /// <summary>
    /// Draws a horizontal line in the inspector. Reworked to use UnityEngine.Color instead of the removed EColor enum.
    /// Supports specifying a static color via RGBA floats. (Dynamic color callbacks like ProgressBar are not implemented here
    /// because DecoratorDrawer does not expose the target object instance easily without extra plumbing.)
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true, Inherited = true)]
    public class HorizontalLineAttribute : DrawerAttribute
    {
        public const float DefaultHeight = 2.0f;

        // Default line color (previously EColor.Gray)
        public static readonly Color DefaultColor = new Color32(128, 128, 128, 255);

        public float Height { get; private set; }
        public Color Color { get; private set; }

        /// <summary>
        /// Default: height = 2, color = gray.
        /// </summary>
        public HorizontalLineAttribute(float height = DefaultHeight)
        {
            Height = height;
            Color = DefaultColor;
        }

        /// <summary>
        /// Specify height and a custom color.
        /// </summary>
        public HorizontalLineAttribute(float height, float r, float g, float b, float a = 1f)
        {
            Height = height;
            Color = new Color(r, g, b, a);
        }

        /// <summary>
        /// Convenience: only color, default height.
        /// </summary>
        public HorizontalLineAttribute(float r, float g, float b, float a = 1f)
        {
            Height = DefaultHeight;
            Color = new Color(r, g, b, a);
        }
    }
}
