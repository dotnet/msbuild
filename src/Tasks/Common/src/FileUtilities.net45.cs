// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET46

using System;
using System.Reflection;

namespace Microsoft.NET.Build.Tasks
{
    static partial class FileUtilities
    {
        private static Version GetAssemblyVersion(string sourcePath)
        {
            try
            {
                return AssemblyName.GetAssemblyName(sourcePath)?.Version;
            }
            catch(BadImageFormatException)
            {
                return null;
            }
        }
    }
}

#endif