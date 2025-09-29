using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NaughtyAttributes.Editor
{
    public static class NaughtyGUI
    {
        private struct RectircleOutline
        {
            public Rect rect;
            public int cornerRadius;
            public int outlineWidth;

            public RectircleOutline(Rect rect, int cornerRadius, int outlineWidth)
            {
                this.rect = rect;
                this.cornerRadius = cornerRadius;
                this.outlineWidth = outlineWidth;
            }

            public override bool Equals(object obj)
            {
                if (!(obj is RectircleOutline))
                    return false;

                RectircleOutline other = (RectircleOutline)obj;
                return rect.Equals(other.rect) &&
                       cornerRadius == other.cornerRadius &&
                       outlineWidth == other.outlineWidth;
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 23 + rect.GetHashCode();
                    hash = hash * 23 + cornerRadius.GetHashCode();
                    hash = hash * 23 + outlineWidth.GetHashCode();
                    return hash;
                }
            }

            public static bool operator ==(RectircleOutline lhs, RectircleOutline rhs)
            {
                return lhs.Equals(rhs);
            }

            public static bool operator !=(RectircleOutline lhs, RectircleOutline rhs)
            {
                return !lhs.Equals(rhs);
            }
        }

        private struct Rectircle
        {
            public Rect rect;
            public int cornerRadius;

            public Rectircle(Rect rect, int cornerRadius)
            {
                this.rect = rect;
                this.cornerRadius = cornerRadius;
            }

            public override bool Equals(object obj)
            {
                if (!(obj is Rectircle))
                    return false;

                Rectircle other = (Rectircle)obj;
                return rect.Equals(other.rect) && cornerRadius == other.cornerRadius;
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 23 + rect.GetHashCode();
                    hash = hash * 23 + cornerRadius.GetHashCode();
                    return hash;
                }
            }

            public static bool operator ==(Rectircle lhs, Rectircle rhs)
            {
                return lhs.Equals(rhs);
            }

            public static bool operator !=(Rectircle lhs, Rectircle rhs)
            {
                return !lhs.Equals(rhs);
            }
        }

        private static Dictionary<Rectircle, Texture2D> _roundCornerRect = new Dictionary<Rectircle, Texture2D>();
        private static Dictionary<RectircleOutline, Texture2D> _roundCornerRectOutline = new Dictionary<RectircleOutline, Texture2D>();

        public static void DrawRectircle(Rect position, Color color, int cornerRadius)
        {
            Rectircle key = new Rectircle(position, cornerRadius);
            if (!_roundCornerRect.ContainsKey(key))
            {
                _roundCornerRect[key] = MakeRoundedRectTexture((int)position.width, (int)position.height, cornerRadius, Color.white);
            }

            Color temp = GUI.color;
            GUI.color *= color;
            GUI.DrawTexture(position, _roundCornerRect[key]);
            GUI.color = temp;
        }

        public static void DrawRectircleOutline(Rect position, Color color, int cornerRadius, int outlineWidth)
        {
            RectircleOutline key = new RectircleOutline(position, cornerRadius, outlineWidth);
            if (!_roundCornerRectOutline.ContainsKey(key))
            {
                _roundCornerRectOutline[key] = MakeRoundedRectOutlineTexture((int)position.width, (int)position.height, cornerRadius, outlineWidth, Color.white);
            }

            Color temp = GUI.color;
            GUI.color *= color;
            GUI.DrawTexture(position, _roundCornerRectOutline[key]);
            GUI.color = temp;
        }

        public static void DrawOutline(Rect rect, Color color, float outlineWidth)
        {
            if (Event.current.type == EventType.Repaint)
            {
                Color temp = GUI.color;
                GUI.color *= color;
                GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, outlineWidth), EditorGUIUtility.whiteTexture);
                GUI.DrawTexture(new Rect(rect.x, rect.y + rect.height - outlineWidth, rect.width, outlineWidth), EditorGUIUtility.whiteTexture);

                // Draw vertical lines slightly inset to avoid overlap with horizontal lines
                GUI.DrawTexture(new Rect(rect.x, rect.y, outlineWidth, rect.height), EditorGUIUtility.whiteTexture);
                GUI.DrawTexture(new Rect(rect.x + rect.width - outlineWidth, rect.y, outlineWidth, rect.height), EditorGUIUtility.whiteTexture);
                GUI.color = temp;
            }
        }

        private static Texture2D MakeRoundedRectOutlineTexture(int width, int height, int radius, int thickness, Color color)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.ARGB32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;

            Color clear = new Color(0, 0, 0, 0);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool insideOuter = IsInsideRoundedRect(x, y, width, height, radius, 0);
                    bool insideInner = IsInsideRoundedRect(x, y, width, height, Mathf.Max(0, radius - thickness), thickness);

                    tex.SetPixel(x, y, (insideOuter && !insideInner) ? color : clear);
                }
            }

            tex.Apply();
            return tex;
        }

        private static bool IsInsideRoundedRect(int x, int y, int width, int height, int radius, int inset)
        {
            int left = inset;
            int right = width - 1 - inset;
            int bottom = inset;
            int top = height - 1 - inset;

            // Check corners with quarter-circle
            if (x < left + radius && y < bottom + radius) // bottom-left
                return (x - (left + radius)) * (x - (left + radius)) +
                       (y - (bottom + radius)) * (y - (bottom + radius)) <= radius * radius;
            if (x > right - radius && y < bottom + radius) // bottom-right
                return (x - (right - radius)) * (x - (right - radius)) +
                       (y - (bottom + radius)) * (y - (bottom + radius)) <= radius * radius;
            if (x < left + radius && y > top - radius) // top-left
                return (x - (left + radius)) * (x - (left + radius)) +
                       (y - (top - radius)) * (y - (top - radius)) <= radius * radius;
            if (x > right - radius && y > top - radius) // top-right
                return (x - (right - radius)) * (x - (right - radius)) +
                       (y - (top - radius)) * (y - (top - radius)) <= radius * radius;

            // Otherwise inside rectangle body
            return (x >= left && x <= right && y >= bottom && y <= top);
        }

        private static Texture2D MakeRoundedRectTexture(int width, int height, int radius, Color color)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.ARGB32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;

            Color clear = new Color(0, 0, 0, 0);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool inside = true;

                    // Kiểm tra 4 góc (quarter circle)
                    if (x < radius && y < radius) // bottom-left
                        inside = (x - radius) * (x - radius) + (y - radius) * (y - radius) <= radius * radius;
                    else if (x >= width - radius && y < radius) // bottom-right
                        inside = (x - (width - radius - 1)) * (x - (width - radius - 1)) + (y - radius) * (y - radius) <= radius * radius;
                    else if (x < radius && y >= height - radius) // top-left
                        inside = (x - radius) * (x - radius) + (y - (height - radius - 1)) * (y - (height - radius - 1)) <= radius * radius;
                    else if (x >= width - radius && y >= height - radius) // top-right
                        inside = (x - (width - radius - 1)) * (x - (width - radius - 1)) + (y - (height - radius - 1)) * (y - (height - radius - 1)) <= radius * radius;

                    tex.SetPixel(x, y, inside ? color : clear);
                }
            }

            tex.Apply();
            return tex;
        }
    }
}