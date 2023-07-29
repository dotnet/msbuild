// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Serialization.Json;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Watcher.Internal;
using Task = Microsoft.Build.Utilities.Task;

namespace DotNetWatchTasks
{
    public class FileSetSerializer : Task
    {
        public ITaskItem[] WatchFiles { get; set; } = null!;

        public bool IsNetCoreApp { get; set; }

        public string TargetFrameworkVersion { get; set; } = null!;

        public string RuntimeIdentifier { get; set; } = null!;

        public string DefaultAppHostRuntimeIdentifier { get; set; } = null!;

        public string RunCommand { get; set; } = null!;

        public string RunArguments { get; set; } = null!;

        public string RunWorkingDirectory { get; set; } = null!;

        public ITaskItem OutputPath { get; set; } = null!;

        public string[] PackageIds { get; set; } = null!;

        public override bool Execute()
        {
            var projectItems = new Dictionary<string, ProjectItems>(StringComparer.OrdinalIgnoreCase);
            var fileSetResult = new MSBuildFileSetResult
            {
                IsNetCoreApp = IsNetCoreApp,
                TargetFrameworkVersion = TargetFrameworkVersion,
                RuntimeIdentifier = RuntimeIdentifier,
                DefaultAppHostRuntimeIdentifier = DefaultAppHostRuntimeIdentifier,
                RunCommand = RunCommand,
                RunArguments = RunArguments,
                RunWorkingDirectory = RunWorkingDirectory,
                Projects = projectItems
            };

            foreach (var item in WatchFiles)
            {
                var fullPath = item.GetMetadata("FullPath");
                var staticWebAssetPath = item.GetMetadata("StaticWebAssetPath");
                var projectFullPath = item.GetMetadata("ProjectFullPath");

                if (!projectItems.TryGetValue(projectFullPath, out var project))
                {
                    projectItems[projectFullPath] = project = new ProjectItems();
                }

                if (string.IsNullOrEmpty(staticWebAssetPath))
                {
                    project.Files.Add(fullPath);
                }
                else
                {
                    project.StaticFiles.Add(new StaticFileItem
                    {
                        FilePath = fullPath,
                        StaticWebAssetPath = staticWebAssetPath,
                    });
                }
            }

            var serializer = new DataContractJsonSerializer(fileSetResult.GetType(), new DataContractJsonSerializerSettings
            {
                UseSimpleDictionaryFormat = true,
            });

            using var fileStream = File.Create(OutputPath.ItemSpec);
            using var writer = JsonReaderWriterFactory.CreateJsonWriter(fileStream, Encoding.UTF8, ownsStream: false, indent: true);
            serializer.WriteObject(writer, fileSetResult);

            return !Log.HasLoggedErrors;
        }
    }
}
