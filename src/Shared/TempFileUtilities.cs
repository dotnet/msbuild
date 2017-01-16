// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Methods to create temp files.</summary>
//-----------------------------------------------------------------------

using System;
using System.IO;
using System.Security;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// This class contains utility methods for file IO.
    /// It is in a separate file so that it can be selectively included into an assembly.
    /// </summary>
    static internal partial class FileUtilities
    {
        /// <summary>
        /// Generates a unique directory name in the temporary folder.  
        /// Caller must delete when finished. 
        /// </summary>
        internal static string GetTemporaryDirectory()
        {
            string temporaryDirectory = Path.Combine(Path.GetTempPath(), "Temporary" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryDirectory);

            return temporaryDirectory;
        }

        /// <summary>
        /// Generates a unique temporary file name with a given extension in the temporary folder.
        /// File is guaranteed to be unique.
        /// Extension may have an initial period.
        /// File will NOT be created.
        /// May throw IOException.
        /// </summary>
        internal static string GetTemporaryFileName(string extension)
        {
            return GetTemporaryFile(null, extension, false);
        }

        /// <summary>
        /// Generates a unique temporary file name with a given extension in the temporary folder.
        /// If no extension is provided, uses ".tmp".
        /// File is guaranteed to be unique.
        /// Caller must delete it when finished.
        /// </summary>
        internal static string GetTemporaryFile()
        {
            return GetTemporaryFile(".tmp");
        }

        /// <summary>
        /// Generates a unique temporary file name with a given extension in the temporary folder.
        /// File is guaranteed to be unique.
        /// Extension may have an initial period.
        /// Caller must delete it when finished.
        /// May throw IOException.
        /// </summary>
        internal static string GetTemporaryFile(string extension)
        {
            return GetTemporaryFile(null, extension);
        }

        /// <summary>
        /// Creates a file with unique temporary file name with a given extension in the specified folder.
        /// File is guaranteed to be unique.
        /// Extension may have an initial period.
        /// If folder is null, the temporary folder will be used.
        /// Caller must delete it when finished.
        /// May throw IOException.
        /// </summary>
        internal static string GetTemporaryFile(string directory, string extension, bool createFile = true)
        {
            ErrorUtilities.VerifyThrowArgumentLengthIfNotNull(directory, "directory");
            ErrorUtilities.VerifyThrowArgumentLength(extension, "extension");

            if (extension[0] != '.')
            {
                extension = '.' + extension;
            }

            try
            {
                directory = directory ?? Path.GetTempPath();

                Directory.CreateDirectory(directory);

                string file = Path.Combine(directory, string.Format("tmp{0}{1}", Guid.NewGuid().ToString("N"), extension));

                ErrorUtilities.VerifyThrow(!File.Exists(file), "Guid should be unique");

                if (createFile)
                {
                    File.WriteAllText(file, String.Empty);
                }

                return file;
            }
            catch (Exception ex) when (ExceptionHandling.IsIoRelatedException(ex))
            {
                throw new IOException(ResourceUtilities.FormatResourceString("Shared.FailedCreatingTempFile", ex.Message), ex);
            }
        }
    }
}
