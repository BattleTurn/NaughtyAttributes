using UnityEditor;
using UnityEngine;

namespace NaughtyAttributes.Editor
{
    public static class EditorRectUtility
    {
        public static bool BeenRightClickOn(this Rect rect)
        {
            Event currentEvent = Event.current;
            if (currentEvent.type == EventType.ContextClick && rect.Contains(currentEvent.mousePosition))
            {
                currentEvent.Use();
                return true;
            }

            return false;
        }

        public static bool IsBlockClick(this Rect r, Event currentEvent)
        {
            if (r.Contains(currentEvent.mousePosition))
            {
                if (currentEvent.type == EventType.MouseDown ||
                    currentEvent.type == EventType.MouseDrag ||
                    currentEvent.type == EventType.MouseUp)
                {
                    currentEvent.Use();
                    return true;
                }
            }

            return false;
        }

        public static void DrawOutline(this Rect rect, Color color, float outlineWidth)
        {
            if (Event.current.type == EventType.Repaint)
            {
                Color color2 = GUI.color;
                GUI.color *= color;
                GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, outlineWidth), EditorGUIUtility.whiteTexture);
                GUI.DrawTexture(new Rect(rect.x, rect.y + rect.height - outlineWidth, rect.width, outlineWidth), EditorGUIUtility.whiteTexture);

                // Draw vertical lines slightly inset to avoid overlap with horizontal lines
                GUI.DrawTexture(new Rect(rect.x, rect.y, outlineWidth, rect.height), EditorGUIUtility.whiteTexture);
                GUI.DrawTexture(new Rect(rect.x + rect.width - outlineWidth, rect.y, outlineWidth, rect.height), EditorGUIUtility.whiteTexture);
                GUI.color = color2;
            }
        }
    }

}