using System.Collections;
using System.Collections.Generic;
using System.IO;

using Microsoft.Build.Framework;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Abstractions;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Domain;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Services
{
    internal class ResolveAssemblyReferenceTaskGateway : IResolveAssemblyReferenceService
    {
        private IResolveAssemblyReferenceTask RarTask { get; }

        internal ResolveAssemblyReferenceTaskGateway(IResolveAssemblyReferenceTask rarTask)
        {
            RarTask = rarTask;
        }

        public ResolveAssemblyReferenceResponse ResolveAssemblyReferences(ResolveAssemblyReferenceRequest req)
        {
            var ioTracker = new ResolveAssemblyReferenceIOTracker();
            var buildEngine = new EventQueueBuildEngine();

            ResolveAssemblyReferenceTaskInput input = ConvertRequestToTaskInput(req, ioTracker, buildEngine);
            ResolveAssemblyReferenceTaskOutput output = RarTask.Execute(input);

            return ConvertTaskOutputToResponse(output, ioTracker, buildEngine);
        }

        private static ResolveAssemblyReferenceTaskInput ConvertRequestToTaskInput(
            ResolveAssemblyReferenceRequest req,
            ResolveAssemblyReferenceIOTracker ioTracker,
            EventQueueBuildEngine buildEngine
        )
        {
            return new ResolveAssemblyReferenceTaskInput
            {
                AllowedAssemblyExtensions = req.AllowedAssemblyExtensions,
                AllowedRelatedFileExtensions = req.AllowedRelatedFileExtensions,
                AppConfigFile = req.AppConfigFile,
                Assemblies = req.Assemblies,
                AssemblyFiles = req.AssemblyFiles,
                AutoUnify = req.AutoUnify,
                BuildEngine = buildEngine,
                CandidateAssemblyFiles = req.CandidateAssemblyFiles,
                CopyLocalDependenciesWhenParentReferenceInGac = req.CopyLocalDependenciesWhenParentReferenceInGac,
                DoNotCopyLocalIfInGac = req.DoNotCopyLocalIfInGac,
                FindDependencies = req.FindDependencies,
                FindDependenciesOfExternallyResolvedReferences = req.FindDependenciesOfExternallyResolvedReferences,
                FindRelatedFiles = req.FindRelatedFiles,
                FindSatellites = req.FindSatellites,
                FindSerializationAssemblies = req.FindSerializationAssemblies,
                FullFrameworkAssemblyTables = req.FullFrameworkAssemblyTables,
                FullFrameworkFolders = req.FullFrameworkFolders,
                FullTargetFrameworkSubsetNames = req.FullTargetFrameworkSubsetNames,
                IgnoreDefaultInstalledAssemblySubsetTables = req.IgnoreDefaultInstalledAssemblySubsetTables,
                IgnoreDefaultInstalledAssemblyTables = req.IgnoreDefaultInstalledAssemblyTables,
                IgnoreTargetFrameworkAttributeVersionMismatch = req.IgnoreTargetFrameworkAttributeVersionMismatch,
                IgnoreVersionForFrameworkReferences = req.IgnoreVersionForFrameworkReferences,
                InstalledAssemblySubsetTables = req.InstalledAssemblySubsetTables,
                InstalledAssemblyTables = req.InstalledAssemblyTables,
                IoTracker = ioTracker,
                LatestTargetFrameworkDirectories = req.LatestTargetFrameworkDirectories,
                ProfileName = req.ProfileName,
                ResolvedSDKReferences = req.ResolvedSDKReferences,
                SearchPaths = req.SearchPaths,
                ShouldExecuteInProcess = true,
                Silent = req.Silent,
                StateFile = req.StateFile,
                SupportsBindingRedirectGeneration = req.SupportsBindingRedirectGeneration,
                TargetFrameworkDirectories = req.TargetFrameworkDirectories,
                TargetFrameworkMoniker = req.TargetFrameworkMoniker,
                TargetFrameworkMonikerDisplayName = req.TargetFrameworkMonikerDisplayName,
                TargetFrameworkSubsets = req.TargetFrameworkSubsets,
                TargetFrameworkVersion = req.TargetFrameworkVersion,
                TargetProcessorArchitecture = req.TargetProcessorArchitecture,
                TargetedRuntimeVersion = req.TargetedRuntimeVersion,
                UnresolveFrameworkAssembliesFromHigherFrameworks = req.UnresolveFrameworkAssembliesFromHigherFrameworks,
                WarnOrErrorOnTargetArchitectureMismatch = req.WarnOrErrorOnTargetArchitectureMismatch,
            };
        }

        private static ResolveAssemblyReferenceResponse ConvertTaskOutputToResponse
        (
            ResolveAssemblyReferenceTaskOutput taskOutput,
            ResolveAssemblyReferenceIOTracker ioTracker,
            EventQueueBuildEngine buildEngine
        )
        {
            var copyLocalFiles = new HashSet<ITaskItem>(taskOutput.CopyLocalFiles);
            var filesWritten = ExtractTaskItemPayloadList(copyLocalFiles, taskOutput.FilesWritten);
            var relatedFiles = ExtractTaskItemPayloadList(copyLocalFiles, taskOutput.RelatedFiles);
            var resolvedDependencyFiles = ExtractTaskItemPayloadList(copyLocalFiles, taskOutput.ResolvedDependencyFiles);
            var resolvedFiles = ExtractTaskItemPayloadList(copyLocalFiles, taskOutput.ResolvedFiles);
            var satelliteFiles = ExtractTaskItemPayloadList(copyLocalFiles, taskOutput.SatelliteFiles);
            var scatterFiles = ExtractTaskItemPayloadList(copyLocalFiles, taskOutput.ScatterFiles);
            var serializationAssemblyFiles = ExtractTaskItemPayloadList(copyLocalFiles, taskOutput.SerializationAssemblyFiles);
            var suggestedRedirects = ExtractTaskItemPayloadList(copyLocalFiles, taskOutput.SuggestedRedirects);

            HashSet<string> trackedPaths = ioTracker.TrackedPaths;
            var trackedDirectories = new List<string>(trackedPaths.Count);
            var trackedFiles = new List<string>(trackedPaths.Count);

            // TODO: Investigate within RAR which delegate calls intercepted by IOTracker are guaranteed
            // to be called with a file or directory, then let IOTracker return TrackedDirectories/TrackedFiles.
            // Currently checking for dir separators is necessary since one of either GetDirectories or DirectoryExists
            // appear to be called with a file path at some point, causing files to appear in TrackDirectories and
            // eventually crashing FileSystemWatcher.
            foreach (string path in trackedPaths)
            {
                if (IsDirectory(path))
                {
                    trackedDirectories.Add(path);
                }
                else
                {
                    trackedFiles.Add(path);
                }
            }

            return new ResolveAssemblyReferenceResponse
            {
                // TODO: Major perf improvement, simply adding the recorded BuildEventArgs to the response
                // accounts for anywhere from 40-50% of RAR-aas overhead. This is a combination of triggering
                // ResolveAssemblyReferenceServiceGateway.LogBuildEvents() and blowing up the response
                // payload size, resulting in slower serialization. RAR will likely need some method of knowing
                // the current verbosity.
                BuildEventArgsQueue = buildEngine.BuildEventArgsQueue,
                NumCopyLocalFiles = taskOutput.CopyLocalFiles.Length,
                DependsOnNETStandard = taskOutput.DependsOnNETStandard,
                DependsOnSystemRuntime = taskOutput.DependsOnSystemRuntime,
                FilesWritten = filesWritten,
                RelatedFiles = relatedFiles,
                ResolvedDependencyFiles = resolvedDependencyFiles,
                ResolvedFiles = resolvedFiles,
                SatelliteFiles = satelliteFiles,
                ScatterFiles = scatterFiles,
                SerializationAssemblyFiles = serializationAssemblyFiles,
                SuggestedRedirects = suggestedRedirects,
                TrackedDirectories = trackedDirectories,
                TrackedFiles = trackedFiles,
            };
        }

        private static ReadOnlyTaskItem[] ExtractTaskItemPayloadList(HashSet<ITaskItem> copyLocalFiles, ITaskItem[] taskItems)
        {
            int numTaskItems = taskItems.Length;
            var taskItemPayloadList = new ReadOnlyTaskItem[numTaskItems];

            for (int i = 0; i < numTaskItems; i++)
            {
                ITaskItem taskItem = taskItems[i];
                var taskItemPayload = new ReadOnlyTaskItem(taskItem.ItemSpec, taskItem.MetadataCount);

                // TODO: Perf improvement, copying metadata from Utilities.TaskItem accounts for ~10% of RAR-aas overhead
                // due to slow copying of metadata from the backing CopyOnWriteDictionary.
                if (taskItem is ITaskItem2 taskItem2)
                {
                    foreach (DictionaryEntry metadataNameWithValue in taskItem2.CloneCustomMetadataEscaped())
                    {
                        var metadataName = (string)metadataNameWithValue.Key;
                        var metadataValue = (string)metadataNameWithValue.Value;
                        taskItemPayload.MetadataNameToValue[metadataName] = metadataValue;
                    }
                }
                else
                {
                    taskItem.CopyMetadataTo(taskItemPayload);
                }

                taskItemPayload.IsCopyLocalFile = copyLocalFiles.Contains(taskItem);
                taskItemPayloadList[i] = taskItemPayload;
            }

            return taskItemPayloadList;
        }

        private static bool IsDirectory(string path)
        {
            if (path.Length == 0)
            {
                return false;
            }

            char ch = path[path.Length - 1];

            return ch == Path.DirectorySeparatorChar
                   || ch == Path.AltDirectorySeparatorChar
                   || ch == Path.VolumeSeparatorChar;
        }
    }
}
