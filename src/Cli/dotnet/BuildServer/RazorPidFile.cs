// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.BuildServer
{
    internal class RazorPidFile
    {
        public const string RazorServerType = "rzc";
        public const string FilePrefix = "rzc-";

        public RazorPidFile(FilePath path, int processId, FilePath serverPath, string pipeName) 
        {
            Path = path;
            ProcessId = processId;
            ServerPath = serverPath;
            PipeName = pipeName ?? throw new ArgumentNullException(pipeName);
        }

        public FilePath Path { get; }

        public int ProcessId;

        public FilePath ServerPath { get; }

        public string PipeName { get; }

        public static RazorPidFile Read(FilePath path, IFileSystem fileSystem = null)
        {
            fileSystem = fileSystem ?? FileSystemWrapper.Default;

            using (var stream = fileSystem.File.OpenFile(
                path.Value,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Write | FileShare.Delete,
                4096,
                FileOptions.None))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                if (!int.TryParse(reader.ReadLine(), out var processId))
                {
                    return null;
                }

                if (reader.ReadLine() != RazorServerType)
                {
                    return null;
                }

                var serverPath = reader.ReadLine();
                if (string.IsNullOrEmpty(serverPath))
                {
                    return null;
                }

                var pipeName = reader.ReadLine();
                if (string.IsNullOrEmpty(pipeName))
                {
                    return null;
                }

                return new RazorPidFile(path, processId, new FilePath(serverPath), pipeName);
            }
        }
    }
}
