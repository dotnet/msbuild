// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

#nullable disable

namespace Microsoft.Build.Tasks.ResourceHandling
{
    internal static class NameUtilities
    {
        /// <summary>
        /// Extract the full name of a type from an assembly-qualified name string.
        /// </summary>
        /// <param name="assemblyQualifiedName"></param>
        /// <returns></returns>
        internal static string FullNameFromAssemblyQualifiedName(string assemblyQualifiedName)
        {
            var commaIndex = assemblyQualifiedName.IndexOf(',');

            if (commaIndex == -1)
            {
                throw new ArgumentException(nameof(assemblyQualifiedName));
            }

            return assemblyQualifiedName.Substring(0, commaIndex);
        }
    }
}
