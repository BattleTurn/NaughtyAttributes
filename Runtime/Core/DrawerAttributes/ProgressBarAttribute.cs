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

        public ProgressBarAttribute(string name, float maxValue, string colorValueName) : this(name, maxValue, (object)colorValueName)
        {
        }

        public ProgressBarAttribute(string name, string maxValueName, string colorValueName) : this(name, maxValueName, (object)colorValueName)
        {
        }

        public ProgressBarAttribute(string name, float maxValue, float r = 0, float g = 0, float b = 0, float a = 1) : this(name, maxValue, new Color(r, g, b, a))
        {
        }

        public ProgressBarAttribute(string name, string maxValueName, float r = 0, float g = 0, float b = 0, float a = 1) : this(name, maxValueName, new Color(r, g, b, a))
        {
        }

        private ProgressBarAttribute(string name, float maxValue, object colorValue = null)
        {
            Name = name;
            MaxValue = maxValue;
            if (NaughtyColorUtility.TryParse(colorValue, out var c))
                Color = c;
            else if (colorValue is string s)
                ColorValueName = s;
        }

        private ProgressBarAttribute(string name, string maxValueName, object colorValue = null)
        {
            Name = name;
            MaxValueName = maxValueName;
            if (NaughtyColorUtility.TryParse(colorValue, out var c))
                Color = c;
            else if (colorValue is string s)
                ColorValueName = s;
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
