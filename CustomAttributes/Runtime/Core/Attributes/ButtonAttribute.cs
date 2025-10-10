using System;

namespace StylizeAttributes.Core
{
    public enum EButtonEnableMode
    {
        /// <summary>
        /// Button should be active always
        /// </summary>
        Always,
        /// <summary>
        /// Button should be active only in editor
        /// </summary>
        Editor,
        /// <summary>
        /// Button should be active only in playmode
        /// </summary>
        Playmode
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class ButtonAttribute : SpecialCaseDrawerAttribute
    {
        public string MethodName { get; private set; }
        public EButtonEnableMode SelectedEnableMode { get; private set; }

        public ButtonAttribute(string methodName, Enum enumValue , EButtonEnableMode enabledMode = EButtonEnableMode.Always) : base(enumValue)
        {
            MethodName = methodName;
            SelectedEnableMode = enabledMode;
        }

        public ButtonAttribute(string methodName) : base()
        {
            MethodName = methodName;
            SelectedEnableMode = EButtonEnableMode.Always;
        }
    }
}