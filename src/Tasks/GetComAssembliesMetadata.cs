// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if FEATURE_APPDOMAIN

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.Build.Tasks.AssemblyDependency;
using Microsoft.Build.Utilities;

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Resolves metadata for the specified set of COM assemblies.
    /// </summary>
    public class GetComAssembliesMetadata : TaskExtension
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
            if (!NativeMethodsShared.IsWindows)
            {
                Log.LogErrorWithCodeFromResources("General.TaskRequiresWindows", nameof(GetComAssembliesMetadata));
                return false;
            }

            var assembliesMetadata = new List<ITaskItem>();
            foreach (string assemblyPath in AssembyPaths)
            {
                AssemblyInformation assemblyInformation = new(assemblyPath);
                AssemblyAttributes attributes = assemblyInformation.GetAssemblyMetadata();

                if (attributes != null)
                {
                    assembliesMetadata.Add(SetItemMetadata(attributes));
                }
            }

            _assembliesMetadata = assembliesMetadata.ToArray();

            return true;
        }

        /// <summary>
        /// List of assembly paths.
        /// </summary>
        [Required]
        public string[] AssembyPaths
        {
            get => _assemblyPaths;

            set
            {
                ErrorUtilities.VerifyThrowArgumentNull(value, nameof(AssembyPaths));
                _assemblyPaths = value;
            }
        }

        /// <summary>
        /// Gets a list of resolved assembly metadata.
        /// </summary>
        [Output]
        public ITaskItem[] AssembliesMetadata => _assembliesMetadata;

        /// <summary>
        /// Sets metadata on the assembly path.
        /// </summary>
        private TaskItem SetItemMetadata(AssemblyAttributes attributes)
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
                yield return new KeyValuePair<string, string>(nameof(attributes.Guid), attributes.Guid);
                yield return new KeyValuePair<string, string>(nameof(attributes.MajorVersion), attributes.MajorVersion.ToString());
                yield return new KeyValuePair<string, string>(nameof(attributes.MinorVersion), attributes.MinorVersion.ToString());
                yield return new KeyValuePair<string, string>(nameof(attributes.PeKind), attributes.PeKind.ToString());
                yield return new KeyValuePair<string, string>(nameof(attributes.PublicKey), attributes.PublicKey);
                yield return new KeyValuePair<string, string>(nameof(attributes.PublicKeyLength), attributes.PublicKeyLength.ToString());
                yield return new KeyValuePair<string, string>(nameof(attributes.IsAssembly), attributes.IsAssembly.ToString());
                yield return new KeyValuePair<string, string>(nameof(attributes.TargetFrameworkMoniker), attributes.TargetFrameworkMoniker);
                yield return new KeyValuePair<string, string>(nameof(attributes.IsImportedFromTypeLib), attributes.IsImportedFromTypeLib.ToString());
            }
        }
    }
}
#endif
