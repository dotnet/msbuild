using System;

namespace Microsoft.DotNet.Tools.Compiler.Native
{
    public static class EnumExtensions
    {
        internal static T Parse<T>(string value)
        {
            return (T)Enum.Parse(typeof(T), value, true);
        }
    }

}
