// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Build.Tasks.ResourceHandling
{
    static class NameUtilities
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
