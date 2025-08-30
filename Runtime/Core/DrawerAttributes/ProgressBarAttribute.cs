using System;
using UnityEngine;

namespace NaughtyAttributes
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class ProgressBarAttribute : DrawerAttribute
    {
        public string Name { get; private set; }
        public float MaxValue { get; set; }
        public string MaxValueName { get; private set; }
        public Color Color { get; private set; }
        public string ColorValueName { get; private set; }

        public ProgressBarAttribute(string name, float maxValue, string colorValueName)
        {
            Name = name;
            MaxValue = maxValue;
            if (colorValueName.Contains('#'))
            {
                Color = NaughtyColorUtility.GetColor(colorValueName, Color.white);
                return;
            }

            ColorValueName = colorValueName;
        }

        public ProgressBarAttribute(string name, string maxValueName, string colorValueName)
        {
            Name = name;
            MaxValueName = maxValueName;
            if (colorValueName.Contains('#'))
            {
                Color = NaughtyColorUtility.GetColor(colorValueName, Color.white);
                return;
            }

            ColorValueName = colorValueName;
        }

        public ProgressBarAttribute(string name, float maxValue, float r = 0, float g = 0, float b = 0, float a = 1)
        {
            Name = name;
            MaxValue = maxValue;
            Color = new Color(r, g, b, a);
        }

        public ProgressBarAttribute(string name, string maxValueName, float r = 0, float g = 0, float b = 0, float a = 1)
        {
            Name = name;
            MaxValueName = maxValueName;
            Color = new Color(r, g, b, a);
        }

        public ProgressBarAttribute(float maxValue, string colorValueName)
            : this("", maxValue, colorValueName)
        {
        }

        public ProgressBarAttribute(string maxValueName, string colorValueName)
            : this("", maxValueName, colorValueName)
        {
        }
    }
}
