﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <remarks>
    /// Represents a single input to a compilation-style task.
    /// Keeps track of timestamp for later comparison.
    ///
    /// Must be serializable because instances may be marshaled cross-AppDomain, see <see cref="ProcessResourceFiles.PortableLibraryCacheInfo"/>.
    /// </remarks>
#if FEATURE_APPDOMAIN
    [Serializable]
#endif
    internal class DependencyFile
    {
        // Filename
        internal string filename;

        // Date and time the file was last modified           
        internal DateTime lastModified;

        // Whether the file exists or not.
        internal bool exists = false;

        /// <summary>
        /// The name of the file.
        /// </summary>
        internal string FileName
        {
            get { return filename; }
        }

        /// <summary>
        /// The last-modified timestamp when the class was instantiated.
        /// </summary>
        internal DateTime LastModified
        {
            get { return lastModified; }
        }

        /// <summary>
        /// Returns true if the file existed when this class was instantiated.
        /// </summary>
        internal bool Exists
        {
            get { return exists; }
        }

        /// <summary>
        /// Construct.
        /// </summary>
        /// <param name="filename">The file name.</param>
        internal DependencyFile(string filename)
        {
            this.filename = FileUtilities.FixFilePath(filename);

            if (FileSystems.Default.FileExists(FileName))
            {
                lastModified = File.GetLastWriteTime(FileName);
                exists = true;
            }
            else
            {
                exists = false;
            }
        }

        internal DependencyFile()
        {
        }

        /// <summary>
        /// Checks whether the file has changed since the last time a timestamp was recorded.
        /// </summary>
        /// <returns></returns>
        internal bool HasFileChanged()
        {
            FileInfo info = FileUtilities.GetFileInfoNoThrow(filename);

            // Obviously if the file no longer exists then we are not up to date.
            if (info?.Exists != true)
            {
                return true;
            }

            // Check the saved timestamp against the current timestamp.
            // If they are different then obviously we are out of date.
            DateTime curLastModified = info.LastWriteTime;
            if (curLastModified != lastModified)
            {
                return true;
            }

            // All checks passed -- the info should still be up to date.
            return false;
        }
    }
}
