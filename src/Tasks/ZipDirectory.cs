// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using System;
using System.IO;
using System.IO.Compression;

namespace Microsoft.Build.Tasks
{
    public sealed class ZipDirectory : TaskExtension
    {
        [Required]
        public ITaskItem DestinationFile { get; set; }

        [Required]
        public ITaskItem SourceDirectory { get; set; }

        public override bool Execute()
        {
            if (!Directory.Exists(SourceDirectory.ItemSpec))
            {
                Log.LogErrorFromResources("ZipDirectory.ErrorDirectoryDoesNotExist", SourceDirectory.ItemSpec);
                return false;
            }

            if (File.Exists(DestinationFile.ItemSpec))
            {
                Log.LogErrorFromResources("ZipDirectory.ErrorFileExists", DestinationFile.ItemSpec);

                return false;
            }

            try
            {
                Log.LogMessageFromResources(MessageImportance.High, "ZipDirectory.Comment", SourceDirectory.ItemSpec, DestinationFile.ItemSpec);
                ZipFile.CreateFromDirectory(SourceDirectory.ItemSpec, DestinationFile.ItemSpec);
            }
            catch (Exception e)
            {
                Log.LogErrorFromResources("ZipDirectory.ErrorFailed", SourceDirectory.ItemSpec, DestinationFile.ItemSpec, e.Message);
            }

            return !Log.HasLoggedErrors;
        }
    }
}
