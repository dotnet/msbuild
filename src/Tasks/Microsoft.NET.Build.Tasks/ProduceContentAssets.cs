// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// Read raised lock file items for content assets and process them to handle
    /// preprocessing tokens, identify items that should be copied to output, and
    /// other filtering on content assets, including whether they match the active 
    /// project language.
    /// </summary>
    public sealed class ProduceContentAssets : TaskBase
    {
        private readonly List<ITaskItem> _contentItems = new List<ITaskItem>();
        private readonly List<ITaskItem> _fileWrites = new List<ITaskItem>();
        private readonly List<ITaskItem> _copyLocalItems = new List<ITaskItem>();
        private IContentAssetPreprocessor _assetPreprocessor;

        #region Output Items

        /// <summary>
        /// Content items that are marked copy to output with resolved path
        /// </summary>
        [Output]
        public ITaskItem[] CopyLocalItems => _copyLocalItems.ToArray();

        /// <summary>
        /// All content items produced with content item metadata.
        /// </summary>
        [Output]
        public ITaskItem[] ProcessedContentItems => _contentItems.ToArray();

        /// <summary>
        /// Files written to during the generation process.
        /// </summary>
        [Output]
        public ITaskItem[] FileWrites => _fileWrites.ToArray();

        #endregion

        #region Inputs

        /// <summary>
        /// Resolved paths to content file assets with metadata such as BuildAction, PPOutputPath etc.
        /// </summary>
        [Required]
        public ITaskItem[] ContentFileDependencies { get; set; }

        /// <summary>
        /// Items specifying the tokens that can be substituted into preprocessed 
        /// content files. The ItemSpec of each item is the name of the token, 
        /// without the surrounding $$, and the Value metadata should specify the 
        /// replacement value.
        /// </summary>
        public ITaskItem[] ContentPreprocessorValues
        {
            get; set;
        }

        /// <summary>
        /// The base output directory where the temporary, preprocessed files should be written to.
        /// </summary>
        public string ContentPreprocessorOutputDirectory
        {
            get; set;
        }

        /// <summary>
        /// Optional the Project Language (E.g. C#, VB)
        /// </summary>
        public string ProjectLanguage
        {
            get; set;
        }

        /// <summary>
        /// Optionally filter the operation of this task to just preprocessor files
        /// </summary>
        public bool ProduceOnlyPreprocessorFiles
        {
            get; set;
        }

        #endregion

        public ProduceContentAssets()
        {
        }

        #region Test Support

        internal ProduceContentAssets(IContentAssetPreprocessor assetPreprocessor)
            : this()
        {
            _assetPreprocessor = assetPreprocessor;
        }

        #endregion

        /// <summary>
        /// Resource for reading, processing and writing content assets
        /// </summary>
        internal IContentAssetPreprocessor AssetPreprocessor
        {
            get
            {
                if (_assetPreprocessor == null)
                {
                    _assetPreprocessor = new NugetContentAssetPreprocessor();
                }
                return _assetPreprocessor;
            }
        }

        protected override void ExecuteCore()
        {
            var preprocessorValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // If a preprocessor directory isn't set, then we won't have a place to generate.
            if (!string.IsNullOrEmpty(ContentPreprocessorOutputDirectory))
            {
                // Assemble the preprocessor values up-front
                var duplicatedPreprocessorKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var preprocessorValueItem in ContentPreprocessorValues ?? Enumerable.Empty<ITaskItem>())
                {
                    if (preprocessorValues.ContainsKey(preprocessorValueItem.ItemSpec))
                    {
                        duplicatedPreprocessorKeys.Add(preprocessorValueItem.ItemSpec);
                    }

                    preprocessorValues[preprocessorValueItem.ItemSpec] = preprocessorValueItem.GetMetadata("Value");
                }

                foreach (var duplicatedPreprocessorKey in duplicatedPreprocessorKeys)
                {
                    Log.LogWarning(Strings.DuplicatePreprocessorToken, duplicatedPreprocessorKey, preprocessorValues[duplicatedPreprocessorKey]);
                }

                AssetPreprocessor.ConfigurePreprocessor(ContentPreprocessorOutputDirectory, preprocessorValues);
            }

            var contentFileDeps = ContentFileDependencies ?? Enumerable.Empty<ITaskItem>();
            var contentFileGroups = contentFileDeps
                .Where(f => !ProduceOnlyPreprocessorFiles || IsPreprocessorFile(f))
                .GroupBy(t => t.GetMetadata(MetadataKeys.NuGetPackageId));
            foreach (var grouping in contentFileGroups)
            {
                // Is there an asset with our exact language? If so, we use that. Otherwise we'll simply collect "any" assets.
                string codeLanguageToSelect;

                if (string.IsNullOrEmpty(ProjectLanguage))
                {
                    codeLanguageToSelect = "any";
                }
                else
                {
                    string projectLanguage = NuGetUtils.GetLockFileLanguageName(ProjectLanguage);
                    if (grouping.Any(t => t.GetMetadata(MetadataKeys.CodeLanguage) == projectLanguage))
                    {
                        codeLanguageToSelect = projectLanguage;
                    }
                    else
                    {
                        codeLanguageToSelect = "any";
                    }
                }

                foreach (var contentFile in grouping)
                {
                    // Ignore magic _._ placeholder files. We couldn't ignore them during the project language
                    // selection, since you could imagine somebody might have a package that puts assets under
                    // "any" but then uses _._ to opt some languages out of it
                    if (NuGetUtils.IsPlaceholderFile(contentFile.ItemSpec))
                    {
                        continue;
                    }

                    if (contentFile.GetMetadata(MetadataKeys.CodeLanguage) == codeLanguageToSelect)
                    {
                        ProduceContentAsset(contentFile);
                    }
                }
            }
        }

        private bool IsPreprocessorFile(ITaskItem contentFile) =>
            !string.IsNullOrEmpty(contentFile.GetMetadata(MetadataKeys.PPOutputPath));

        private void ProduceContentAsset(ITaskItem contentFile)
        {
            string resolvedPath = contentFile.ItemSpec;
            string pathToFinalAsset = resolvedPath;
            string ppOutputPath = contentFile.GetMetadata(MetadataKeys.PPOutputPath);
            string packageName = contentFile.GetMetadata(MetadataKeys.NuGetPackageId);
            string packageVersion = contentFile.GetMetadata(MetadataKeys.NuGetPackageVersion);

            if (!string.IsNullOrEmpty(ppOutputPath))
            {
                if (string.IsNullOrEmpty(ContentPreprocessorOutputDirectory))
                {
                    throw new BuildErrorException(Strings.ContentPreproccessorParameterRequired, nameof(ProduceContentAssets), nameof(ContentPreprocessorOutputDirectory));
                }

                // We need the preprocessed output, so let's run the preprocessor here
                string relativeOutputPath = Path.Combine(packageName, packageVersion, ppOutputPath);
                if (AssetPreprocessor.Process(resolvedPath, relativeOutputPath, out pathToFinalAsset))
                {
                    _fileWrites.Add(new TaskItem(pathToFinalAsset));
                }
            }

            if (contentFile.GetBooleanMetadata(MetadataKeys.CopyToOutput) == true)
            {
                string outputPath = contentFile.GetMetadata(MetadataKeys.OutputPath);
                outputPath = string.IsNullOrEmpty(outputPath) ? ppOutputPath : outputPath;

                if (!string.IsNullOrEmpty(outputPath))
                {
                    var item = new TaskItem(pathToFinalAsset);
                    item.SetMetadata("TargetPath", outputPath);
                    item.SetMetadata(MetadataKeys.NuGetPackageId, packageName);
                    item.SetMetadata(MetadataKeys.NuGetPackageVersion, packageVersion);

                    _copyLocalItems.Add(item);
                }
                else
                {
                    Log.LogWarning(Strings.ContentItemDoesNotProvideOutputPath, pathToFinalAsset, MetadataKeys.CopyToOutput, MetadataKeys.OutputPath, MetadataKeys.PPOutputPath);
                }
            }

            // TODO if build action is none do we even need to write the processed file above?
            string buildAction = contentFile.GetMetadata(MetadataKeys.BuildAction);
            if (!string.Equals(buildAction, "none", StringComparison.OrdinalIgnoreCase))
            {
                var item = new TaskItem(pathToFinalAsset);
                item.SetMetadata(MetadataKeys.NuGetPackageId, packageName);
                item.SetMetadata(MetadataKeys.NuGetPackageVersion, packageVersion);

                // We'll put additional metadata on the item so we can convert it back to the real item group in our targets
                item.SetMetadata("ProcessedItemType", buildAction);

                // TODO is this needed for .NETCore?
                // If this is XAML, the build targets expect Link metadata to construct the relative path
                if (string.Equals(buildAction, "Page", StringComparison.OrdinalIgnoreCase))
                {
                    item.SetMetadata("Link", Path.Combine("NuGet", packageName, packageVersion, Path.GetFileName(resolvedPath)));
                }

                _contentItems.Add(item);
            }
        }
    }
}
