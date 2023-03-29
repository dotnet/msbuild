// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Sdk.BlazorWebAssembly
{
    public class GZipCompress : Task
    {
        [Required]
        public ITaskItem[] FilesToCompress { get; set; }

        [Output]
        public ITaskItem[] CompressedFiles { get; set; }

        [Required]
        public string OutputDirectory { get; set; }

        public override bool Execute()
        {
            CompressedFiles = new ITaskItem[FilesToCompress.Length];

            Directory.CreateDirectory(OutputDirectory);

            System.Threading.Tasks.Parallel.For(0, FilesToCompress.Length, i =>
            {
                var file = FilesToCompress[i];
                var inputFullPath = file.GetMetadata("FullPath");
                var relativePath = file.GetMetadata("RelativePath");

                var outputRelativePath = Path.Combine(
                    OutputDirectory,
                    BrotliCompress.CalculateTargetPath(inputFullPath, ".gz"));

                var outputItem = new TaskItem(outputRelativePath, file.CloneCustomMetadata());
                outputItem.SetMetadata("RelativePath", relativePath + ".gz");
                outputItem.SetMetadata("OriginalItemSpec", file.ItemSpec);
                CompressedFiles[i] = outputItem;

                if (!File.Exists(outputRelativePath))
                {
                    Log.LogMessage(MessageImportance.Low, "Compressing '{0}' because compressed file '{1}' does not exist.", file.ItemSpec, outputRelativePath);
                }
                else if (File.GetLastWriteTimeUtc(inputFullPath) < File.GetLastWriteTimeUtc(outputRelativePath))
                {
                    // Incrementalism. If input source doesn't exist or it exists and is not newer than the expected output, do nothing.
                    Log.LogMessage(MessageImportance.Low, "Skipping '{0}' because '{1}' is newer than '{2}'.", file.ItemSpec, outputRelativePath, file.ItemSpec);
                    return;
                }
                else
                {
                    Log.LogMessage(MessageImportance.Low, "Compressing '{0}' because file is newer than '{1}'.", inputFullPath, outputRelativePath);
                }

                try
                {
                    using var sourceStream = File.OpenRead(file.ItemSpec);
                    using var fileStream = File.Create(outputRelativePath);
                    using var stream = new GZipStream(fileStream, CompressionLevel.Optimal);

                    sourceStream.CopyTo(stream);
                }
                catch (Exception e)
                {
                    Log.LogErrorFromException(e);
                    return;
                }
            });

            return !Log.HasLoggedErrors;
        }
    }
}
