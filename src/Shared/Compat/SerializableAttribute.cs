using System.Runtime.InteropServices;

namespace System
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum | AttributeTargets.Delegate, Inherited = false)]
    [ComVisible(true)]
    public sealed class SerializableAttribute : Attribute
    {
        public SerializableAttribute() { }
    }

    [AttributeUsage(AttributeTargets.Field, Inherited = false)]
    [ComVisible(true)]
    public sealed class NonSerializedAttribute : Attribute
    {
        public NonSerializedAttribute() { }
    }
}