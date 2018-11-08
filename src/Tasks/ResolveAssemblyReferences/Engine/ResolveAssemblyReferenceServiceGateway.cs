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
                var readOnlyTaskItem = new ReadOnlyTaskItem(taskItem.ItemSpec);

                // TODO: Perf improvement, copying metadata accounts for a significant percentage of overhead
                taskItem.CopyMetadataTo(readOnlyTaskItem);
                readOnlyTaskItems[i] = readOnlyTaskItem;
            }

            return readOnlyTaskItems;
        }

        private static ResolveAssemblyReferenceTaskOutput ConvertResponseToTaskOutput(ResolveAssemblyReferenceResponse resp)
        {
            var taskItems = resp.TaskItems;

            ITaskItem[] copyLocalFiles = ExtractTaskItemsForField(taskItems, TaskItemField.CopyLocalFiles);
            ITaskItem[] filesWritten = ExtractTaskItemsForField(taskItems, TaskItemField.FilesWritten);
            ITaskItem[] relatedFiles = ExtractTaskItemsForField(taskItems, TaskItemField.RelatedFiles);
            ITaskItem[] resolvedDependencyFiles = ExtractTaskItemsForField(taskItems, TaskItemField.ResolvedDependencyFiles);
            ITaskItem[] resolvedFiles = ExtractTaskItemsForField(taskItems, TaskItemField.ResolvedFiles);
            ITaskItem[] satelliteFiles = ExtractTaskItemsForField(taskItems, TaskItemField.SatelliteFiles);
            ITaskItem[] scatterFiles = ExtractTaskItemsForField(taskItems, TaskItemField.ScatterFiles);
            ITaskItem[] serializationAssemblyFiles = ExtractTaskItemsForField(taskItems, TaskItemField.SerializationAssemblyFiles);
            ITaskItem[] suggestedRedirects = ExtractTaskItemsForField(taskItems, TaskItemField.SuggestedRedirects);

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

        private static ITaskItem[] ExtractTaskItemsForField
        (
            List<ReadOnlyTaskItem> taskItemPayloadList,
            TaskItemField field
        )
        {
            int taskItemCount = 0;

            foreach (ReadOnlyTaskItem taskItemPayload in taskItemPayloadList)
            {
                if (taskItemPayload.IsResponseField(field))
                {
                    taskItemCount++;
                }
            }

            var taskItems = new ITaskItem[taskItemCount];
            int nextTaskItemIndex = 0;

            foreach (ReadOnlyTaskItem taskItemPayload in taskItemPayloadList)
            {
                if (taskItemPayload.IsResponseField(field))
                {
                    // TODO: Perf improvement, constructing engine TaskItems accounts for a significant percentage of overhead
                    taskItems[nextTaskItemIndex] = new TaskItem(taskItemPayload);
                    nextTaskItemIndex++;
                }
            }

            return taskItems;
        }

        private static void LogBuildEvents(IBuildEngine buildEngine, List<ResolveAssemblyReferenceBuildEventArgs> buildEventsArgsQueue)
        {
            // TODO: Perf improvement, the garbage collector seems to get consistently triggered somewhere here and
            // ends up costing more time than the actual build engine logging

            foreach (ResolveAssemblyReferenceBuildEventArgs buildEventArgs in buildEventsArgsQueue)
            {
                DateTime eventTimestamp = DateTime.FromFileTimeUtc(buildEventArgs.EventTimestamp);

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
