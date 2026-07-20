// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.IO.Enumeration;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Represents a task that can create a tar archive from a directory.
    /// </summary>
    /// <remarks>
    /// This task uses the <see cref="System.Formats.Tar"/> APIs which are only available when MSBuild
    /// runs on .NET (not .NET Framework). It is therefore registered to run only on the .NET runtime and
    /// is unavailable in Visual Studio / MSBuild.exe.
    /// </remarks>
    [MSBuildMultiThreadableTask]
    public sealed class TarDirectory : TaskExtension, IIncrementalTask, IMultiThreadableTask
    {
        public const string CompressionNone = "None";
        public const string CompressionGZip = "GZip";

        /// <summary>
        /// Gets or sets a <see cref="ITaskItem"/> containing the full path to the destination file to create.
        /// </summary>
        [Required]
        public ITaskItem DestinationFile { get; set; } = null!;

        /// <summary>
        /// Gets or sets a value indicating whether the destination file should be overwritten.
        /// </summary>
        public bool Overwrite { get; set; }

        /// <summary>
        /// Gets or sets a <see cref="ITaskItem"/> containing the full path to the source directory to create a tar archive from.
        /// </summary>
        [Required]
        public ITaskItem SourceDirectory { get; set; } = null!;

        /// <summary>
        /// Question the incremental nature of this task.
        /// </summary>
        /// <remarks>This task does not support incremental build and will error out instead.</remarks>
        public bool FailIfNotIncremental { get; set; }

        /// <summary>
        /// Gets or sets the compression to apply to the tar archive.
        /// Valid values are "None" (the default) and "GZip".
        /// This parameter is optional.
        /// </summary>
        public string? Compression { get; set; }

        /// <summary>
        /// Gets or sets the tar entry format to use for the archive.
        /// Valid values are "Pax" (the default), "Gnu", "Ustar", and "V7".
        /// This parameter is optional.
        /// </summary>
        public string? Format { get; set; }

        /// <inheritdoc />
        public TaskEnvironment TaskEnvironment { get; set; } = TaskEnvironment.Fallback;

        public override bool Execute()
        {
            AbsolutePath sourceDirectoryAbsolutePath = TaskEnvironment.GetAbsolutePath(SourceDirectory.ItemSpec);
            DirectoryInfo sourceDirectory = new DirectoryInfo(sourceDirectoryAbsolutePath);

            if (!sourceDirectory.Exists)
            {
                Log.LogErrorWithCodeFromResources("TarDirectory.ErrorDirectoryDoesNotExist", sourceDirectory.FullName);
                return false;
            }

            AbsolutePath destinationFileAbsolutePath = TaskEnvironment.GetAbsolutePath(DestinationFile.ItemSpec);
            FileInfo destinationFile = new FileInfo(destinationFileAbsolutePath);

            BuildEngine3.Yield();

            try
            {
                if (destinationFile.Exists)
                {
                    if (!Overwrite || FailIfNotIncremental)
                    {
                        Log.LogErrorWithCodeFromResources("TarDirectory.ErrorFileExists", destinationFile.FullName);

                        return false;
                    }

                    try
                    {
                        File.Delete(destinationFile.FullName);
                    }
                    catch (Exception e)
                    {
                        string lockedFileMessage = LockCheck.GetLockedFileMessage(destinationFile.FullName);
                        Log.LogErrorWithCodeFromResources("TarDirectory.ErrorFailed", sourceDirectory.FullName, destinationFile.FullName, e.Message, lockedFileMessage);

                        return false;
                    }
                }

                try
                {
                    if (FailIfNotIncremental)
                    {
                        Log.LogErrorFromResources("TarDirectory.Comment", sourceDirectory.FullName, destinationFile.FullName);
                    }
                    else
                    {
                        Log.LogMessageFromResources(MessageImportance.High, "TarDirectory.Comment", sourceDirectory.FullName, destinationFile.FullName);

                        bool useGZip = false;
                        if (!string.IsNullOrEmpty(Compression) && !TryParseCompression(Compression, out useGZip))
                        {
                            Log.LogErrorWithCodeFromResources("TarDirectory.ErrorInvalidCompression", Compression);

                            return false;
                        }

                        TarEntryFormat format = TarEntryFormat.Pax;
                        if (!string.IsNullOrEmpty(Format) && !TryParseFormat(Format, out format))
                        {
                            Log.LogErrorWithCodeFromResources("TarDirectory.ErrorInvalidFormat", Format);

                            return false;
                        }

                        using FileStream destinationStream = new FileStream(destinationFile.FullName, FileMode.Create, FileAccess.Write, FileShare.None);

                        if (useGZip)
                        {
                            using GZipStream gzipStream = new GZipStream(destinationStream, CompressionLevel.Optimal);
                            CreateTarFromDirectory(sourceDirectory.FullName, gzipStream, format);
                        }
                        else
                        {
                            CreateTarFromDirectory(sourceDirectory.FullName, destinationStream, format);
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.LogErrorWithCodeFromResources("TarDirectory.ErrorFailed", sourceDirectory.FullName, destinationFile.FullName, e.Message, string.Empty);
                }
            }
            finally
            {
                BuildEngine3.Reacquire();
            }

            return !Log.HasLoggedErrors;

            static bool TryParseCompression(string compression, out bool useGZip)
            {
                if (StringComparer.OrdinalIgnoreCase.Equals(compression, CompressionNone))
                {
                    useGZip = false;
                }
                else if (StringComparer.OrdinalIgnoreCase.Equals(compression, CompressionGZip)
                    || StringComparer.OrdinalIgnoreCase.Equals(compression, "gz")
                    || StringComparer.OrdinalIgnoreCase.Equals(compression, "gzip"))
                {
                    useGZip = true;
                }
                else
                {
                    useGZip = false;
                    return false;
                }

                return true;
            }

            static bool TryParseFormat(string format, out TarEntryFormat tarFormat) =>
                Enum.TryParse(format, ignoreCase: true, out tarFormat) && tarFormat != TarEntryFormat.Unknown;
        }

        /// <summary>
        /// Writes all filesystem entries under <paramref name="sourceDirectoryFullPath"/> to <paramref name="destination"/> as a tar archive.
        /// </summary>
        /// <remarks>
        /// This mirrors the behavior of <see cref="TarFile.CreateFromDirectory(string, System.IO.Stream, bool)"/> with
        /// <c>includeBaseDirectory: false</c>, but additionally honors the requested <paramref name="format"/>. The
        /// <see cref="TarFile"/> overloads that accept a <see cref="TarEntryFormat"/> are not available on the
        /// <c>net10.0</c> API surface MSBuild targets, so the directory walk is performed directly against a
        /// <see cref="TarWriter"/>.
        /// </remarks>
        private static void CreateTarFromDirectory(string sourceDirectoryFullPath, Stream destination, TarEntryFormat format)
        {
            using TarWriter writer = new TarWriter(destination, format, leaveOpen: true);

            foreach ((string fullPath, string entryName) in EnumerateEntries(sourceDirectoryFullPath, sourceDirectoryFullPath.Length))
            {
                writer.WriteEntry(fullPath, entryName);
            }
        }

        /// <summary>
        /// Recursively enumerates the filesystem entries under <paramref name="directory"/>, yielding the full path and
        /// the corresponding archive entry name for each, while avoiding recursion into directory symlinks.
        /// </summary>
        private static IEnumerable<(string fullPath, string entryName)> EnumerateEntries(string directory, int basePathLength)
        {
            // Recurse into subdirectories after yielding their own entry so that directory entries precede their contents,
            // matching the ordering produced by TarFile.CreateFromDirectory.
            FileSystemEnumerable<(string fullPath, string entryName, bool recurse)> enumerable = new(
                directory,
                (ref FileSystemEntry entry) =>
                {
                    string fullPath = entry.ToFullPath();
                    bool isRealDirectory = entry.IsDirectory && (entry.Attributes & FileAttributes.ReparsePoint) == 0;
                    string entryName = GetEntryName(fullPath.AsSpan(basePathLength), appendDirectorySeparator: isRealDirectory);
                    return (fullPath, entryName, isRealDirectory);
                });

            foreach ((string fullPath, string entryName, bool recurse) in enumerable)
            {
                yield return (fullPath, entryName);

                if (recurse)
                {
                    foreach ((string fullPath, string entryName) childEntry in EnumerateEntries(fullPath, basePathLength))
                    {
                        yield return childEntry;
                    }
                }
            }
        }

        /// <summary>
        /// Converts a filesystem path that is relative to the archive root into a tar entry name using forward slashes,
        /// appending a trailing slash for directory entries.
        /// </summary>
        private static string GetEntryName(ReadOnlySpan<char> relativePath, bool appendDirectorySeparator)
        {
            relativePath = relativePath.TrimStart("/\\".AsSpan());
            string entryName = relativePath.ToString().Replace('\\', '/');

            return appendDirectorySeparator ? entryName + '/' : entryName;
        }
    }
}
