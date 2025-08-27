// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// THE ASSEMBLY BUILT FROM THIS SOURCE FILE HAS BEEN DEPRECATED FOR YEARS. IT IS BUILT ONLY TO PROVIDE
// BACKWARD COMPATIBILITY FOR API USERS WHO HAVE NOT YET MOVED TO UPDATED APIS. PLEASE DO NOT SEND PULL
// REQUESTS THAT CHANGE THIS FILE WITHOUT FIRST CHECKING WITH THE MAINTAINERS THAT THE FIX IS REQUIRED.

using System;
using System.IO;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Microsoft.Build.BuildEngine.Shared
{
    /// <summary>
    /// This class contains utility methods for file IO.
    /// Separate from FileUtilities because some assemblies may only need the patterns.
    /// PERF\COVERAGE NOTE: Try to keep classes in 'shared' as granular as possible. All the methods in
    /// each class get pulled into the resulting assembly.
    /// </summary>
    /// <owner>SumedhK, JomoF</owner>
    internal static class FileUtilitiesRegex
    {
        // regular expression used to match file-specs beginning with "<drive letter>:"
        internal static readonly Regex DrivePattern = new Regex(@"^[A-Za-z]:");

        // regular expression used to match UNC paths beginning with "\\<server>\<share>"
        internal static readonly Regex UNCPattern = new Regex(String.Format(CultureInfo.InvariantCulture,
            @"^[\{0}\{1}][\{0}\{1}][^\{0}\{1}]+[\{0}\{1}][^\{0}\{1}]+", Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    }
}
