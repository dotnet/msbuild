// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETFRAMEWORK

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