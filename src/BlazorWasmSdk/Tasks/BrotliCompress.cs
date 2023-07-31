// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Cryptography;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Sdk.BlazorWebAssembly
{
    public class BrotliCompress : ToolTask
    {
        private static readonly char[] InvalidPathChars = Path.GetInvalidFileNameChars();
        private string _dotnetPath;

        [Required]
        public ITaskItem[] FilesToCompress { get; set; }

        [Output]
        public ITaskItem[] CompressedFiles { get; set; }

        [Required]
        public string OutputDirectory { get; set; }

        public string CompressionLevel { get; set; }

        public bool SkipIfOutputIsNewer { get; set; }

        [Required]
        public string ToolAssembly { get; set; }

        protected override string ToolName => Path.GetDirectoryName(DotNetPath);

        private string DotNetPath
        {
            get
            {
                if (!string.IsNullOrEmpty(_dotnetPath))
                {
                    return _dotnetPath;
                }

                _dotnetPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
                if (string.IsNullOrEmpty(_dotnetPath))
                {
                    throw new InvalidOperationException("DOTNET_HOST_PATH is not set");
                }

                return _dotnetPath;
            }
        }

        private static string Quote(string path)
        {
            if (string.IsNullOrEmpty(path) || (path[0] == '\"' && path[path.Length - 1] == '\"'))
            {
                // it's already quoted
                return path;
            }

            return $"\"{path}\"";
        }

        protected override string GenerateCommandLineCommands() => Quote(ToolAssembly);

        protected override string GenerateResponseFileCommands()
        {
            var builder = new StringBuilder();


            builder.AppendLine("brotli");

            if (!string.IsNullOrEmpty(CompressionLevel))
            {
                builder.AppendLine("-c");
                builder.AppendLine(CompressionLevel);
            }

            CompressedFiles = new ITaskItem[FilesToCompress.Length];

            for (var i = 0; i < FilesToCompress.Length; i++)
            {
                var file = FilesToCompress[i];
                var inputFullPath = file.GetMetadata("FullPath");
                var relativePath = file.GetMetadata("RelativePath");
                var outputRelativePath = Path.Combine(OutputDirectory, CalculateTargetPath(inputFullPath, ".br"));
                var outputFullPath = Path.GetFullPath(outputRelativePath);

                var outputItem = new TaskItem(outputRelativePath, file.CloneCustomMetadata());
                outputItem.SetMetadata("RelativePath", relativePath + ".br");
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
                    continue;
                }
                else
                {
                    Log.LogMessage(MessageImportance.Low, "Compressing '{0}' because file is newer than '{1}'.", inputFullPath, outputRelativePath);
                }

                builder.AppendLine("-s");
                builder.AppendLine(Quote(inputFullPath));

                builder.AppendLine("-o");
                builder.AppendLine(Quote(outputFullPath));
            }

            return builder.ToString();
        }

        internal static string CalculateTargetPath(string relativePath, string extension)
        {
            // RelativePath can be long and if used as-is to write the output, might result in long path issues on Windows.
            // Instead we'll calculate a fixed length path by hashing the input file name. This uses SHA1 similar to the Hash task in MSBuild
            // since it has no crytographic significance.
            using var hash = SHA1.Create();
            var bytes = Encoding.UTF8.GetBytes(relativePath);
            var hashString = Convert.ToBase64String(hash.ComputeHash(bytes));

            var builder = new StringBuilder();

            for (var i = 0; i < 8; i++)
            {
                var c = hashString[i];
                builder.Append(InvalidPathChars.Contains(c) ? '+' : c);
            }

            builder.Append(extension);
            return builder.ToString();
        }

        protected override string GenerateFullPathToTool() => DotNetPath;
    }
}
