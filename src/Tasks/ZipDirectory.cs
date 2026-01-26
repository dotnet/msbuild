// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks
{
    [MSBuildMultiThreadableTask]
    public sealed class ZipDirectory : TaskExtension, IIncrementalTask, IMultiThreadableTask
    {
        public const string CompressionLevelOptimal = "Optimal";
        public const string CompressionLevelFastest = "Fastest";
        public const string CompressionLevelNoCompression = "NoCompression";

        /// <summary>
        /// Note: This compression level is unavailable on .NET Framework.
        /// Attempts to use it on .NET Framework will use the default compression level instead, and log a warning.
        /// </summary>
        public const string CompressionLevelSmallestSize = "SmallestSize";

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
        /// Gets or sets a <see cref="ITaskItem"/> containing the full path to the source directory to create a zip archive from.
        /// </summary>
        [Required]
        public ITaskItem SourceDirectory { get; set; } = null!;

        /// <summary>
        /// Question the incremental nature of this task.
        /// </summary>
        /// <remarks>This task does not support incremental build and will error out instead.</remarks>
        public bool FailIfNotIncremental { get; set; }

        /// <summary>
        /// Gets or sets the compression level to use when creating the zip archive.
        /// Valid values are "Optimal", "Fastest", and "NoCompression".
        /// This parameter is optional.
        /// </summary>
        /// <remarks>
        /// Versions of MSBuild that run on .NET (not .NET Framework) additionally support
        /// the "SmallestSize" compression level. Attempting to use that level on
        /// .NET Framework will use the default compression level instead, and log a warning.
        /// </remarks>
        public string? CompressionLevel { get; set; }

        /// <inheritdoc />
        public TaskEnvironment TaskEnvironment { get; set; } = null!;

        public override bool Execute()
        {
            AbsolutePath sourceDirectoryPath = TaskEnvironment.GetAbsolutePath(FrameworkFileUtilities.FixFilePath(SourceDirectory.ItemSpec));

            if (!Directory.Exists(sourceDirectoryPath))
            {
                Log.LogErrorWithCodeFromResources("ZipDirectory.ErrorDirectoryDoesNotExist", sourceDirectoryPath.OriginalValue);
                return false;
            }

            AbsolutePath destinationFilePath = TaskEnvironment.GetAbsolutePath(FrameworkFileUtilities.FixFilePath(DestinationFile.ItemSpec));

            if (File.Exists(destinationFilePath))
            {
                if (!Overwrite || FailIfNotIncremental)
                {
                    Log.LogErrorWithCodeFromResources("ZipDirectory.ErrorFileExists", destinationFilePath.OriginalValue);

                    return false;
                }

                try
                {
                    File.Delete(destinationFilePath);
                }
                catch (Exception e)
                {
                    string lockedFileMessage = LockCheck.GetLockedFileMessage(destinationFilePath);
                    Log.LogErrorWithCodeFromResources("ZipDirectory.ErrorFailed", sourceDirectoryPath.OriginalValue, destinationFilePath.OriginalValue, e.Message, lockedFileMessage);

                    return false;
                }
            }

            try
            {
                if (FailIfNotIncremental)
                {
                    Log.LogErrorFromResources("ZipDirectory.Comment", sourceDirectoryPath.OriginalValue, destinationFilePath.OriginalValue);
                }
                else
                {
                    Log.LogMessageFromResources(MessageImportance.High, "ZipDirectory.Comment", sourceDirectoryPath.OriginalValue, destinationFilePath.OriginalValue);
                    if (CompressionLevel is null)
                    {
                        ZipFile.CreateFromDirectory(sourceDirectoryPath, destinationFilePath);
                    }
                    else if (TryParseCompressionLevel(CompressionLevel, out CompressionLevel? compressionLevel))
                    {
                        ZipFile.CreateFromDirectory(sourceDirectoryPath, destinationFilePath, compressionLevel.Value, includeBaseDirectory: false);
                    }
                    else
                    {
#if NETFRAMEWORK
                        // If new compression levels are added to .NET in future (and not .NET Framework) we should add a check for them here.
                        if (StringComparer.OrdinalIgnoreCase.Equals(CompressionLevel, CompressionLevelSmallestSize))
                        {
                            Log.LogWarningWithCodeFromResources("ZipDirectory.WarningCompressionLevelUnsupportedOnFramework", CompressionLevel);
                        }
                        else
#endif
                        {
                            Log.LogWarningWithCodeFromResources("ZipDirectory.ErrorInvalidCompressionLevel", CompressionLevel);
                        }

                        ZipFile.CreateFromDirectory(sourceDirectoryPath, destinationFilePath);
                    }
                }
            }
            catch (Exception e)
            {
                Log.LogErrorWithCodeFromResources("ZipDirectory.ErrorFailed", sourceDirectoryPath.OriginalValue, destinationFilePath.OriginalValue, e.Message, string.Empty);
            }

            return !Log.HasLoggedErrors;

            static bool TryParseCompressionLevel(string levelString, [NotNullWhen(returnValue: true)] out CompressionLevel? level)
            {
                if (StringComparer.OrdinalIgnoreCase.Equals(levelString, CompressionLevelOptimal))
                {
                    level = System.IO.Compression.CompressionLevel.Optimal;
                }
                else if (StringComparer.OrdinalIgnoreCase.Equals(levelString, CompressionLevelFastest))
                {
                    level = System.IO.Compression.CompressionLevel.Fastest;
                }
                else if (StringComparer.OrdinalIgnoreCase.Equals(levelString, CompressionLevelNoCompression))
                {
                    level = System.IO.Compression.CompressionLevel.NoCompression;
                }
#if NET
                else if (StringComparer.OrdinalIgnoreCase.Equals(levelString, CompressionLevelSmallestSize))
                {
                    // Note: "SmallestSize" is not available on .NET Framework.
                    level = System.IO.Compression.CompressionLevel.SmallestSize;
                }
#endif
                else
                {
                    level = default;
                    return false;
                }

                return true;
            }
        }
    }
}
