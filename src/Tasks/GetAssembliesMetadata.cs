// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.Build.Tasks.AssemblyDependency;
using Microsoft.Build.Utilities;

#nullable enable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Resolves metadata for the specified set of assemblies.
    /// </summary>
    [SupportedOSPlatform("windows")]
    [MSBuildMultiThreadableTask]
    public class GetAssembliesMetadata : TaskExtension, IMultiThreadableTask
    {
        /// <inheritdoc />
        public TaskEnvironment TaskEnvironment { get; set; } = TaskEnvironment.Fallback;

        /// <summary>
        /// Assembly paths.
        /// </summary>
        private string[] _assemblyPaths = [];

        /// <summary>
        /// Set of resolved assembly metadata.
        /// </summary>
        private ITaskItem[] _assembliesMetadata = Array.Empty<ITaskItem>();

        /// <summary>
        /// Execute the task.
        /// </summary>
        public override bool Execute()
        {
            var assembliesMetadata = new List<ITaskItem>();
            foreach (string assemblyPath in AssemblyPaths)
            {
                // Preserve original behavior: entries that are null, empty, or whitespace-only previously
                // fell through FileExists and were skipped silently. Skip them here so GetAbsolutePath is
                // never handed a value it could reject with an ArgumentException.
                if (string.IsNullOrWhiteSpace(assemblyPath))
                {
                    continue;
                }

                AbsolutePath absoluteAssemblyPath = TaskEnvironment.GetAbsolutePath(assemblyPath);

                // During DTB the referenced project may not has been built yet, so we need to check if the assembly already exists.
                if (FileSystems.Default.FileExists(absoluteAssemblyPath))
                {
                    using (AssemblyInformation assemblyInformation = new(absoluteAssemblyPath))
                    {
                        AssemblyAttributes attributes = assemblyInformation.GetAssemblyMetadata();

                        if (attributes != null)
                        {
                            // Preserve the original [Output] behavior: the resulting item's ItemSpec must be
                            // the exact path the caller supplied, not the absolutized form used to read metadata.
                            attributes.AssemblyFullPath = assemblyPath;
                            assembliesMetadata.Add(CreateItemWithMetadata(attributes));
                        }
                    }
                }
            }

            _assembliesMetadata = assembliesMetadata.ToArray();

            return true;
        }

        /// <summary>
        /// List of assembly paths.
        /// </summary>
        [Required]
        public string[] AssemblyPaths
        {
            get => _assemblyPaths;

            set
            {
                ArgumentNullException.ThrowIfNull(value, nameof(AssemblyPaths));
                _assemblyPaths = value;
            }
        }

        /// <summary>
        /// Gets a list of resolved assembly metadata.
        /// </summary>
        [Output]
        public ITaskItem[] AssembliesMetadata => _assembliesMetadata;

        /// <summary>
        /// Creates a TaskItem and populates it with the given assembly attributes.
        /// </summary>
        private TaskItem CreateItemWithMetadata(AssemblyAttributes attributes)
        {
            TaskItem referenceItem = new()
            {
                ItemSpec = attributes.AssemblyFullPath,
            };

            IMetadataContainer referenceItemAsMetadataContainer = referenceItem;
            referenceItemAsMetadataContainer.ImportMetadata(EnumerateCommonMetadata());

            return referenceItem;

            IEnumerable<KeyValuePair<string, string>> EnumerateCommonMetadata()
            {
                yield return new KeyValuePair<string, string>(nameof(attributes.AssemblyName), attributes.AssemblyName);
                yield return new KeyValuePair<string, string>(nameof(attributes.RuntimeVersion), attributes.RuntimeVersion);
                yield return new KeyValuePair<string, string>(nameof(attributes.RevisionNumber), attributes.RevisionNumber.ToString());
                yield return new KeyValuePair<string, string>(nameof(attributes.BuildNumber), attributes.BuildNumber.ToString());
                yield return new KeyValuePair<string, string>(nameof(attributes.Culture), attributes.Culture);
                yield return new KeyValuePair<string, string>(nameof(attributes.DefaultAlias), attributes.DefaultAlias);
                yield return new KeyValuePair<string, string>(nameof(attributes.Description), attributes.Description);
                yield return new KeyValuePair<string, string>(nameof(attributes.MajorVersion), attributes.MajorVersion.ToString());
                yield return new KeyValuePair<string, string>(nameof(attributes.MinorVersion), attributes.MinorVersion.ToString());
                yield return new KeyValuePair<string, string>(nameof(attributes.PeKind), attributes.PeKind.ToString());
                yield return new KeyValuePair<string, string>(nameof(attributes.PublicHexKey), attributes.PublicHexKey);
                yield return new KeyValuePair<string, string>(nameof(attributes.IsAssembly), attributes.IsAssembly.ToString());
                yield return new KeyValuePair<string, string>(nameof(attributes.TargetFrameworkMoniker), attributes.TargetFrameworkMoniker);
                yield return new KeyValuePair<string, string>(nameof(attributes.IsImportedFromTypeLib), attributes.IsImportedFromTypeLib.ToString());
            }
        }
    }
}
