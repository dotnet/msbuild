using System.Runtime.InteropServices;

namespace System.Reflection
{
    public static class TypeExtensions
    {
        public static bool IsEquivalentTo(this Type type, Type other)
        {
            return type.Equals(other);
        }
    }
}