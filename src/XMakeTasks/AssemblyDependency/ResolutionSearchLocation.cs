// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Resources;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Reflection;
using System.IO;
using System.Collections;
using System.Globalization;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// A place the resolver tried to look for an assembly along with some information
    /// that can be used to provide a good error message.
    /// </summary>
    internal class ResolutionSearchLocation
    {
        private string _fileNameAttempted = null;
        private string _searchPath = null;
        private AssemblyNameExtension _assemblyName = null;
        private NoMatchReason _reason = NoMatchReason.Unknown;

        /// <summary>
        /// The name of the file that was attempted to match.
        /// </summary>
        internal string FileNameAttempted
        {
            get { return _fileNameAttempted; }
            set { _fileNameAttempted = value; }
        }

        /// <summary>
        /// The literal searchpath element that was used to discover this location.
        /// </summary>
        internal string SearchPath
        {
            get { return _searchPath; }
            set { _searchPath = value; }
        }

        /// <summary>
        /// The name of the assembly found at that location. Will be null if there was no assembly there.
        /// </summary>
        internal AssemblyNameExtension AssemblyName
        {
            get { return _assemblyName; }
            set { _assemblyName = value; }
        }

        /// <summary>
        /// The reason there was no macth.
        /// </summary>
        internal NoMatchReason Reason
        {
            get { return _reason; }
            set { _reason = value; }
        }
    }
}
