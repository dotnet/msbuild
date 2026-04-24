// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET
using System.Runtime.CompilerServices;
#else
using System;
using System.Runtime.InteropServices;
#endif
using System.Threading;

namespace Microsoft.Build.Utilities;

/// <summary>
///  Type information for a type <typeparamref name="T"/>.
/// </summary>
internal static partial class TypeInfo<T>
{
    // Tri-state: 0 = not computed, 1 = false (no references), 2 = true (has references)
    private static int s_hasReferences;

    /// <summary>
    ///  Returns <see langword="true"/> if the type <typeparamref name="T"/> is a reference type or contains references.
    /// </summary>
    public static bool IsReferenceOrContainsReferences()
    {
        int value = Volatile.Read(ref s_hasReferences);
        if (value != 0)
        {
            return value == 2;
        }

#if NET
        bool result = RuntimeHelpers.IsReferenceOrContainsReferences<T>();
#else
        bool result = HasReferences();

        static bool HasReferences()
        {
            if (!typeof(T).IsValueType)
            {
                return true;
            }

            if (typeof(T).IsPrimitive
                || typeof(T).IsEnum
                || typeof(T) == typeof(DateTime))
            {
                return false;
            }

            try
            {
                GCHandle handle = GCHandle.Alloc(default(T), GCHandleType.Pinned);
                handle.Free();
                return false;
            }
            catch (Exception)
            {
                // Contained a reference
                return true;
            }
        }
#endif

        Interlocked.CompareExchange(ref s_hasReferences, result ? 2 : 1, 0);
        return result;
    }
}
