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
        /// <summary>
        /// Gets or sets the full path to the destination file to create.
        /// </summary>
        [Required]
        public FileInfo DestinationFile { get; set; } = null!;

        /// <summary>
        /// Gets or sets a value indicating whether the destination file should be overwritten.
        /// </summary>
        public bool Overwrite { get; set; }

        /// <summary>
        /// Gets or sets the full path to the source directory to create a tar archive from.
        /// </summary>
        [Required]
        public DirectoryInfo SourceDirectory { get; set; } = null!;

        /// <summary>
        /// Question the incremental nature of this task.
        /// </summary>
        /// <remarks>This task does not support incremental build and will error out instead.</remarks>
        public bool FailIfNotIncremental { get; set; }

        /// <summary>
        /// Gets or sets the compression to apply to the tar archive.
        /// The default is <see cref="TarCompression.None"/>.
        /// This parameter is optional.
        /// </summary>
        public TarCompression Compression { get; set; } = TarCompression.None;

        /// <summary>
        /// Gets or sets the tar entry format to use for the archive.
        /// The default is <see cref="TarEntryFormat.Pax"/>.
        /// This parameter is optional.
        /// </summary>
        public TarEntryFormat Format { get; set; } = TarEntryFormat.Pax;

        /// <inheritdoc />
        public TaskEnvironment TaskEnvironment { get; set; } = TaskEnvironment.Fallback;

        public override bool Execute()
        {
            if (!SourceDirectory.Exists)
            {
                Log.LogErrorWithCodeFromResources("TarDirectory.ErrorDirectoryDoesNotExist", SourceDirectory.FullName);
                return false;
            }

            BuildEngine3.Yield();

            try
            {
                if (DestinationFile.Exists)
                {
                    if (!Overwrite || FailIfNotIncremental)
                    {
                        Log.LogErrorWithCodeFromResources("TarDirectory.ErrorFileExists", DestinationFile.FullName);

                        return false;
                    }

                    try
                    {
                        File.Delete(DestinationFile.FullName);
                    }
                    catch (Exception e)
                    {
                        string lockedFileMessage = LockCheck.GetLockedFileMessage(DestinationFile.FullName);
                        Log.LogErrorWithCodeFromResources("TarDirectory.ErrorFailed", SourceDirectory.FullName, DestinationFile.FullName, e.Message, lockedFileMessage);

                        return false;
                    }
                }

                try
                {
                    if (FailIfNotIncremental)
                    {
                        Log.LogErrorFromResources("TarDirectory.Comment", SourceDirectory.FullName, DestinationFile.FullName);
                    }
                    else
                    {
                        Log.LogMessageFromResources(MessageImportance.High, "TarDirectory.Comment", SourceDirectory.FullName, DestinationFile.FullName);

                        // Unknown is only reachable if it was explicitly set; fall back to the Pax default.
                        TarEntryFormat format = Format == TarEntryFormat.Unknown ? TarEntryFormat.Pax : Format;

                        using FileStream destinationStream = new FileStream(DestinationFile.FullName, FileMode.Create, FileAccess.Write, FileShare.None);

                        // Wrap the destination stream in the requested compression, if any. The tar archive is always
                        // written to the (optionally compressed) stream using the new .NET 11 overload that honors the
                        // requested TarEntryFormat.
                        using Stream? compressionStream = Compression switch
                        {
                            TarCompression.GZip => new GZipStream(destinationStream, CompressionLevel.Optimal),
                            TarCompression.ZStandard => new ZstandardStream(destinationStream, CompressionLevel.Optimal),
                            _ => null,
                        };

                        TarFile.CreateFromDirectory(SourceDirectory.FullName, compressionStream ?? destinationStream, includeBaseDirectory: false, format);
                    }
                }
                catch (Exception e)
                {
                    Log.LogErrorWithCodeFromResources("TarDirectory.ErrorFailed", SourceDirectory.FullName, DestinationFile.FullName, e.Message, string.Empty);
                }
            }
            finally
            {
                BuildEngine3.Reacquire();
            }

            return !Log.HasLoggedErrors;
        }

        /// <summary>
        /// Identifies the compression to apply to the tar archive stream.
        /// </summary>
        public enum TarCompression
        {
            None,
            GZip,
            ZStandard,
        }
    }
}
