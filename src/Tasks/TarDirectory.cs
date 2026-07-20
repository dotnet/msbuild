// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
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

                        if (useGZip)
                        {
                            using FileStream destinationStream = new FileStream(destinationFile.FullName, FileMode.Create, FileAccess.Write, FileShare.None);
                            using GZipStream gzipStream = new GZipStream(destinationStream, CompressionLevel.Optimal);
                            TarFile.CreateFromDirectory(sourceDirectory.FullName, gzipStream, includeBaseDirectory: false);
                        }
                        else
                        {
                            TarFile.CreateFromDirectory(sourceDirectory.FullName, destinationFile.FullName, includeBaseDirectory: false);
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
        }
    }
}
