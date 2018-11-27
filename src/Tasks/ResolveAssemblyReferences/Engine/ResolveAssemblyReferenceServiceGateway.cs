using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Build.Framework;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Abstractions;
using Microsoft.Build.Tasks.ResolveAssemblyReferences.Domain;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks.ResolveAssemblyReferences.Engine
{
    internal class ResolveAssemblyReferenceServiceGateway : IResolveAssemblyReferenceTask
    {
        private IResolveAssemblyReferenceService RarService { get; }

        internal ResolveAssemblyReferenceServiceGateway(IResolveAssemblyReferenceService rarService)
        {
            RarService = rarService;
        }

        public ResolveAssemblyReferenceTaskOutput Execute(ResolveAssemblyReferenceTaskInput input)
        {
            ResolveAssemblyReferenceRequest req = ConvertTaskInputToRequest(input);
            ResolveAssemblyReferenceResponse resp = RarService.ResolveAssemblyReferences(req);
            LogBuildEvents(input.BuildEngine, resp.BuildEventArgsQueue);

            return ConvertResponseToTaskOutput(resp);
        }

        private static ResolveAssemblyReferenceRequest ConvertTaskInputToRequest(ResolveAssemblyReferenceTaskInput input)
        {
            ReadOnlyTaskItem[] assemblies = CreateReadOnlyTaskItems(input.Assemblies);
            ReadOnlyTaskItem[] assemblyFiles = CreateReadOnlyTaskItems(input.AssemblyFiles);
            ReadOnlyTaskItem[] fullFrameworkAssemblyTables = CreateReadOnlyTaskItems(input.FullFrameworkAssemblyTables);
            ReadOnlyTaskItem[] installedAssemblySubsetTables = CreateReadOnlyTaskItems(input.InstalledAssemblySubsetTables);
            ReadOnlyTaskItem[] installedAssemblyTables = CreateReadOnlyTaskItems(input.InstalledAssemblyTables);
            ReadOnlyTaskItem[] resolvedSdkReferences = CreateReadOnlyTaskItems(input.ResolvedSDKReferences);
            string stateFile = Path.GetFullPath(input.StateFile);

            return new ResolveAssemblyReferenceRequest
            {
                AllowedAssemblyExtensions = input.AllowedAssemblyExtensions,
                AllowedRelatedFileExtensions = input.AllowedRelatedFileExtensions,
                AppConfigFile = input.AppConfigFile,
                Assemblies = assemblies,
                AssemblyFiles = assemblyFiles,
                AutoUnify = input.AutoUnify,
                CandidateAssemblyFiles = input.CandidateAssemblyFiles,
                CopyLocalDependenciesWhenParentReferenceInGac = input.CopyLocalDependenciesWhenParentReferenceInGac,
                DoNotCopyLocalIfInGac = input.DoNotCopyLocalIfInGac,
                FindDependencies = input.FindDependencies,
                FindDependenciesOfExternallyResolvedReferences = input.FindDependenciesOfExternallyResolvedReferences,
                FindRelatedFiles = input.FindRelatedFiles,
                FindSatellites = input.FindSatellites,
                FindSerializationAssemblies = input.FindSerializationAssemblies,
                FullFrameworkAssemblyTables = fullFrameworkAssemblyTables,
                FullFrameworkFolders = input.FullFrameworkFolders,
                FullTargetFrameworkSubsetNames = input.FullTargetFrameworkSubsetNames,
                IgnoreDefaultInstalledAssemblySubsetTables = input.IgnoreDefaultInstalledAssemblySubsetTables,
                IgnoreDefaultInstalledAssemblyTables = input.IgnoreDefaultInstalledAssemblyTables,
                IgnoreTargetFrameworkAttributeVersionMismatch = input.IgnoreTargetFrameworkAttributeVersionMismatch,
                IgnoreVersionForFrameworkReferences = input.IgnoreVersionForFrameworkReferences,
                InstalledAssemblySubsetTables = installedAssemblySubsetTables,
                InstalledAssemblyTables = installedAssemblyTables,
                LatestTargetFrameworkDirectories = input.LatestTargetFrameworkDirectories,
                ProfileName = input.ProfileName,
                ResolvedSDKReferences = resolvedSdkReferences,
                SearchPaths = input.SearchPaths,
                Silent = input.Silent,
                StateFile = stateFile,
                SupportsBindingRedirectGeneration = input.SupportsBindingRedirectGeneration,
                TargetFrameworkDirectories = input.TargetFrameworkDirectories,
                TargetFrameworkMoniker = input.TargetFrameworkMoniker,
                TargetFrameworkMonikerDisplayName = input.TargetFrameworkMonikerDisplayName,
                TargetFrameworkSubsets = input.TargetFrameworkSubsets,
                TargetFrameworkVersion = input.TargetFrameworkVersion,
                TargetProcessorArchitecture = input.TargetProcessorArchitecture,
                TargetedRuntimeVersion = input.TargetedRuntimeVersion,
                UnresolveFrameworkAssembliesFromHigherFrameworks = input.UnresolveFrameworkAssembliesFromHigherFrameworks,
                WarnOrErrorOnTargetArchitectureMismatch = input.WarnOrErrorOnTargetArchitectureMismatch
            };
        }

        private static ReadOnlyTaskItem[] CreateReadOnlyTaskItems(ITaskItem[] taskItems)
        {
            var readOnlyTaskItems = new ReadOnlyTaskItem[taskItems.Length];

            for (int i = 0; i < taskItems.Length; i++)
            {
                ITaskItem taskItem = taskItems[i];

                if (taskItem is ITaskItem2 taskItem2 && taskItem2.CloneCustomMetadataEscaped() is Dictionary<string, string> metadataNameToValueDict)
                {
                    readOnlyTaskItems[i] = new ReadOnlyTaskItem(taskItem.ItemSpec, metadataNameToValueDict);
                }
                else
                {
                    var readOnlyTaskItem = new ReadOnlyTaskItem(taskItem.ItemSpec);
                    taskItem.CopyMetadataTo(readOnlyTaskItem);
                    readOnlyTaskItems[i] = readOnlyTaskItem;
                }
            }

            return readOnlyTaskItems;
        }

        private static ResolveAssemblyReferenceTaskOutput ConvertResponseToTaskOutput(ResolveAssemblyReferenceResponse resp)
        {
            int nextCopyLocalFilesIndex = 0;
            var copyLocalFiles = new ITaskItem[resp.NumCopyLocalFiles];
            ITaskItem[] filesWritten = ExtractTaskItems(copyLocalFiles, ref nextCopyLocalFilesIndex, resp.FilesWritten);
            ITaskItem[] relatedFiles = ExtractTaskItems(copyLocalFiles, ref nextCopyLocalFilesIndex, resp.RelatedFiles);
            ITaskItem[] resolvedDependencyFiles = ExtractTaskItems(copyLocalFiles, ref nextCopyLocalFilesIndex, resp.ResolvedDependencyFiles);
            ITaskItem[] resolvedFiles = ExtractTaskItems(copyLocalFiles, ref nextCopyLocalFilesIndex, resp.ResolvedFiles);
            ITaskItem[] satelliteFiles = ExtractTaskItems(copyLocalFiles, ref nextCopyLocalFilesIndex, resp.SatelliteFiles);
            ITaskItem[] scatterFiles = ExtractTaskItems(copyLocalFiles, ref nextCopyLocalFilesIndex, resp.ScatterFiles);
            ITaskItem[] serializationAssemblyFiles = ExtractTaskItems(copyLocalFiles, ref nextCopyLocalFilesIndex, resp.SerializationAssemblyFiles);
            ITaskItem[] suggestedRedirects = ExtractTaskItems(copyLocalFiles, ref nextCopyLocalFilesIndex, resp.SuggestedRedirects);

            return new ResolveAssemblyReferenceTaskOutput
            {
                CopyLocalFiles = copyLocalFiles,
                FilesWritten = filesWritten,
                DependsOnNETStandard = resp.DependsOnNETStandard,
                DependsOnSystemRuntime = resp.DependsOnSystemRuntime,
                RelatedFiles = relatedFiles,
                ResolvedDependencyFiles = resolvedDependencyFiles,
                ResolvedFiles = resolvedFiles,
                SatelliteFiles = satelliteFiles,
                ScatterFiles = scatterFiles,
                SerializationAssemblyFiles = serializationAssemblyFiles,
                SuggestedRedirects = suggestedRedirects
            };
        }

        private static ITaskItem[] ExtractTaskItems(ITaskItem[] copyLocalFiles, ref int nextCopyLocalFilesIndex, ReadOnlyTaskItem[] taskItemPayloadList)
        {
            int numTaskItems = taskItemPayloadList.Length;
            var taskItems = new ITaskItem[numTaskItems];

            for (int i = 0; i < numTaskItems; i++)
            {
                // TODO: Perf improvement, constructing Utilities.TaskItems accounts for ~10% of RAR-aas overhead
                // due to slow setting of metadata in the backing CopyOnWriteDictionary.
                ReadOnlyTaskItem taskItemPayload = taskItemPayloadList[i];
                var taskItem = new TaskItem(taskItemPayload);
                taskItems[i] = taskItem;

                if (taskItemPayload.IsCopyLocalFile)
                {
                    copyLocalFiles[nextCopyLocalFilesIndex] = taskItem;
                    nextCopyLocalFilesIndex++;
                }
            }

            return taskItems;
        }

        private static void LogBuildEvents(IBuildEngine buildEngine, List<ResolveAssemblyReferenceBuildEventArgs> buildEventsArgsQueue)
        {
            // TODO: Perf improvement, LogBuildEvents() accounts for ~10% of RAR-aas overhead.
            // This is a result of logging on silent verbosities, triggering garbage collection,
            // and reconstruction of thousands of BuildEventArgs objects.

            foreach (ResolveAssemblyReferenceBuildEventArgs buildEventArgs in buildEventsArgsQueue)
            {
                DateTime eventTimestamp = new DateTime(buildEventArgs.EventTimestamp, DateTimeKind.Utc);

                switch (buildEventArgs.BuildEventArgsType)
                {
                    case BuildEventArgsType.Error:
                        var errorEventArgs = new BuildErrorEventArgs
                        (
                            buildEventArgs.Subcategory,
                            buildEventArgs.Code,
                            buildEventArgs.File,
                            buildEventArgs.LineNumber,
                            buildEventArgs.ColumnNumber,
                            buildEventArgs.EndLineNumber,
                            buildEventArgs.EndColumnNumber,
                            buildEventArgs.Message,
                            buildEventArgs.HelpKeyword,
                            buildEventArgs.SenderName
                        );

                        buildEngine.LogErrorEvent(errorEventArgs);
                        break;
                    case BuildEventArgsType.Message:
                        var messageEventArgs = new BuildMessageEventArgs
                        (
                            buildEventArgs.Subcategory,
                            buildEventArgs.Code,
                            buildEventArgs.File,
                            buildEventArgs.LineNumber,
                            buildEventArgs.ColumnNumber,
                            buildEventArgs.EndLineNumber,
                            buildEventArgs.EndColumnNumber,
                            buildEventArgs.Message,
                            buildEventArgs.HelpKeyword,
                            buildEventArgs.SenderName,
                            (MessageImportance)buildEventArgs.Importance,
                            eventTimestamp,
                            buildEventArgs.MessageArgs
                        );

                        buildEngine.LogMessageEvent(messageEventArgs);
                        break;
                    case BuildEventArgsType.Warning:
                        var warningEventArgs = new BuildWarningEventArgs
                        (
                            buildEventArgs.Subcategory,
                            buildEventArgs.Code,
                            buildEventArgs.File,
                            buildEventArgs.LineNumber,
                            buildEventArgs.ColumnNumber,
                            buildEventArgs.EndLineNumber,
                            buildEventArgs.EndColumnNumber,
                            buildEventArgs.Message,
                            buildEventArgs.HelpKeyword,
                            buildEventArgs.SenderName,
                            eventTimestamp,
                            buildEventArgs.MessageArgs
                        );

                        buildEngine.LogWarningEvent(warningEventArgs);
                        break;
                }
            }
        }
    }
}
