// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.IO.Compression;
using Microsoft.Build.Framework;

#nullable disable

namespace Microsoft.Build.Tasks
{
    public sealed class ZipDirectory : TaskExtension, IIncrementalTask
    {
        /// <summary>
        /// Gets or sets a <see cref="ITaskItem"/> containing the full path to the destination file to create.
        /// </summary>
        [Required]
        public ITaskItem DestinationFile { get; set; }

        /// <summary>
        /// Gets or sets a value indicating if the destination file should be overwritten.
        /// </summary>
        public bool Overwrite { get; set; }

        /// <summary>
        /// Gets or sets a <see cref="ITaskItem"/> containing the full path to the source directory to create a zip archive from.
        /// </summary>
        [Required]
        public ITaskItem SourceDirectory { get; set; }

        /// <summary>
        /// Question the incremental nature of this task.
        /// </summary>
        /// <remarks>This task does not support incremental build and will error out instead.</remarks>
        public bool FailIfNotIncremental { get; set; }

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
                        Log.LogErrorWithCodeFromResources("ZipDirectory.ErrorFailed", sourceDirectory.FullName, destinationFile.FullName, e.Message);

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
                        ZipFile.CreateFromDirectory(sourceDirectory.FullName, destinationFile.FullName);
                    }
                }
                catch (Exception e)
                {
                    Log.LogErrorWithCodeFromResources("ZipDirectory.ErrorFailed", sourceDirectory.FullName, destinationFile.FullName, e.Message);
                }
            }
            finally
            {
                BuildEngine3.Reacquire();
            }

            return !Log.HasLoggedErrors;
        }
    }
}
