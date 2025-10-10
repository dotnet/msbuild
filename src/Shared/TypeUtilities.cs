// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
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
        /// Gets all attributes decorating the specified <paramref name="type"/>.
        /// </summary>
        public static IList<CustomAttributeData> GetCustomAttributes(this Type type)
        {
            if (type == null)
            {
                return Array.Empty<CustomAttributeData>();
            }

            try
            {
                return CustomAttributeData.GetCustomAttributes(type);
            }
            catch (Exception e) when (!ExceptionHandling.IsCriticalException(e))
            {
                // Skip this attribute - it references a type that can't be loaded/found
                // It might be available in the child node.
                return Array.Empty<CustomAttributeData>();
            }
        }

        /// <summary>
        /// Determines whether the list of <paramref name="attributes"/> contains an attribute whose simple type name
        /// equals <typeparamref name="T"/>, swallowing non-critical reflection exceptions..
        /// </summary>
        /// <param name="attributes">The list of attributes retrieved from a type.</param>
        /// <returns>True if the attribute is found, or false if it cannot be resolved.</returns>
        public static bool HasAttribute<T>(IList<CustomAttributeData> attributes)
            where T : Attribute
        {
            foreach (CustomAttributeData attr in attributes)
            {
                try
                {
                    if (attr.AttributeType?.Name == nameof(T))
                    {
                        return true;
                    }
                }
                catch (Exception e) when (!ExceptionHandling.IsCriticalException(e))
                {
                    // Skip this attribute - it references a type that can't be loaded/found
                    // It might be available in the child node.
                }
            }

            return false;
        }
    }
}
