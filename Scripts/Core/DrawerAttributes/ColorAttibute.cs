using System;

namespace NaughtyAttributes
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class ColorAttribute : DrawerAttribute
    {
        public string Path { get; private set; }

        public ColorAttribute(string path)
        {
            Path = path;
        }
    }
}
