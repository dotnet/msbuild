// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

#nullable disable

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// Helper class to wrap the Microsoft.VisualStudio.Setup.Configuration.Interop API to query
    /// Visual Studio setup for instances installed on the machine.
    /// Code derived from sample: https://code.msdn.microsoft.com/Visual-Studio-Setup-0cedd331
    /// </summary>
    internal class VisualStudioLocationHelper
    {
        /// <summary>
        /// Query the Visual Studio setup API to get instances of Visual Studio installed
        /// on the machine. Will not include anything before Visual Studio "15".
        /// </summary>
        /// <returns>Enumerable list of Visual Studio instances</returns>
        internal static IList<VisualStudioInstance> GetInstances()
        {
            var validInstances = new List<VisualStudioInstance>();
            return validInstances;
        }
    }

    /// <summary>
    /// Wrapper class to represent an installed instance of Visual Studio.
    /// </summary>
    internal class VisualStudioInstance
    {
        /// <summary>
        /// Version of the Visual Studio Instance
        /// </summary>
        internal Version Version { get; }

        /// <summary>
        /// Path to the Visual Studio installation
        /// </summary>
        internal string Path { get; }

        /// <summary>
        /// Full name of the Visual Studio instance with SKU name
        /// </summary>
        internal string Name { get; }

        internal VisualStudioInstance(string name, string path, Version version)
        {
            Name = name;
            Path = path;
            Version = version;
        }
    }
}
