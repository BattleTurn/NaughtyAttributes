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
    }

}