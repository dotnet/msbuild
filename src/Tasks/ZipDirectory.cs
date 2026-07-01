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
    public sealed class ZipDirectory : TaskExtension, IIncrementalTask
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

        public override bool Execute()
        {
            DirectoryInfo sourceDirectory = new DirectoryInfo(SourceDirectory.ItemSpec);

            if (!sourceDirectory.Exists)
            {
                Log.LogErrorWithCodeFromResources("ZipDirectory.ErrorDirectoryDoesNotExist", sourceDirectory.FullName);
                return false;
            }

            FileInfo destinationFile = new FileInfo(DestinationFile.ItemSpec);

            BuildEngine3.Yield();

            try
            {
                if (destinationFile.Exists)
                {
                    if (!Overwrite || FailIfNotIncremental)
                    {
                        Log.LogErrorWithCodeFromResources("ZipDirectory.ErrorFileExists", destinationFile.FullName);

                        return false;
                    }

                    try
                    {
                        File.Delete(destinationFile.FullName);
                    }
                    catch (Exception e)
                    {
                        string lockedFileMessage = LockCheck.GetLockedFileMessage(destinationFile.FullName);
                        Log.LogErrorWithCodeFromResources("ZipDirectory.ErrorFailed", sourceDirectory.FullName, destinationFile.FullName, e.Message, lockedFileMessage);

                        return false;
                    }
                }

                try
                {
                    if (FailIfNotIncremental)
                    {
                        Log.LogErrorFromResources("ZipDirectory.Comment", sourceDirectory.FullName, destinationFile.FullName);
                    }
                    else
                    {
                        Log.LogMessageFromResources(MessageImportance.High, "ZipDirectory.Comment", sourceDirectory.FullName, destinationFile.FullName);
                        if (CompressionLevel is null)
                        {
                            ZipFile.CreateFromDirectory(sourceDirectory.FullName, destinationFile.FullName);
                        }
                        else if (TryParseCompressionLevel(CompressionLevel, out CompressionLevel? compressionLevel))
                        {
                            ZipFile.CreateFromDirectory(sourceDirectory.FullName, destinationFile.FullName, compressionLevel.Value, includeBaseDirectory: false);
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

                            ZipFile.CreateFromDirectory(sourceDirectory.FullName, destinationFile.FullName);
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.LogErrorWithCodeFromResources("ZipDirectory.ErrorFailed", sourceDirectory.FullName, destinationFile.FullName, e.Message, string.Empty);
                }
            }
            finally
            {
                BuildEngine3.Reacquire();
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
