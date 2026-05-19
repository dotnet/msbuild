// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using System.Reflection;

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// Utility extension methods for working with <see cref="Type"/> metadata in a resilient manner.
    /// </summary>
    /// <remarks>
    /// These helpers intentionally:
    /// - Catch and suppress non-critical reflection/type loading exceptions so callers can probe for attributes safely
    ///   even when some referenced types cannot be loaded in the current load context.
    /// - Compare attribute types by their simple <see cref="MemberInfo.Name"/> (e.g. <c>ObsoleteAttribute</c>) rather than
    ///   full name or assembly-qualified name. This mirrors existing MSBuild behavior but means ambiguous attribute
    ///   short names across different assemblies cannot be distinguished here.
    /// </remarks>
    internal static class TypeUtilities
    {
        /// <summary>
        /// Determines whether the specified <paramref name="type"/> is decorated with an attribute of type <typeparamref name="T"/>.
        /// </summary>
        public static bool HasAttribute<T>(this Type type)
            where T : Attribute
        {
            return type.HasAttribute(typeof(T).Name);
        }

        /// <summary>
        /// Determines whether the specified <paramref name="type"/> is decorated with an attribute whose simple type name
        /// equals <paramref name="attributeName"/>.
        /// </summary>
        public static bool HasAttribute(this Type type, string attributeName)
        {
            if (type == null)
            {
                return false;
            }

            try
            {
                return CustomAttributeData
                    .GetCustomAttributes(type)
                    .Any(attr => SafeGetAttributeName(attr) == attributeName);
            }
            catch (Exception e) when (!ExceptionHandling.IsCriticalException(e))
            {
                // Skip this attribute - it references a type that can't be loaded/found
                // It might be available in the child node.
                return false;
            }
        }

        /// <summary>
        /// Safely retrieves the simple name of an attribute's type, swallowing non-critical reflection exceptions.
        /// </summary>
        /// <param name="attr">The attribute metadata.</param>
        /// <returns>The simple attribute type name, or <c>null</c> if it cannot be resolved.</returns>
        private static string? SafeGetAttributeName(CustomAttributeData attr)
        {
            try
            {
                return attr.AttributeType?.Name;
            }
            catch (Exception e) when (!ExceptionHandling.IsCriticalException(e))
            {
                // Skip this attribute - it references a type that can't be loaded/found
                // It might be available in the child node.
                return null;
            }
        }
    }
}
