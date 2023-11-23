// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
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
    public class GetAssembliesMetadata : TaskExtension
    {
        /// <summary>
        /// Assembly paths.
        /// </summary>
        private string[] _assemblyPaths = Array.Empty<string>();

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
                // During DTB the referenced project may not has been built yet, so we need to check if the assembly already exists.
                if (File.Exists(assemblyPath))
                {
                    AssemblyInformation assemblyInformation = new(assemblyPath);
                    AssemblyAttributes attributes = assemblyInformation.GetAssemblyMetadata();

                    if (attributes != null)
                    {
                        assembliesMetadata.Add(CreateItemWithMetadata(attributes));
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
                ErrorUtilities.VerifyThrowArgumentNull(value, nameof(AssemblyPaths));
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
