// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.NETCore.Build.Tasks
{
    /// <summary>
    /// Read raised lock file items for content assets and process them to handle
    /// preprocessing tokens, identify items that should be copied to output, and
    /// other filtering on content assets, including whether they match the active 
    /// project language.
    /// </summary>
    public sealed class ProduceContentAssets : Task
    {
        private readonly Dictionary<string, string> _resolvedPaths = new Dictionary<string, string>();
        private readonly List<ITaskItem> _contentItems = new List<ITaskItem>();
        private readonly List<ITaskItem> _fileWrites = new List<ITaskItem>();
        private readonly List<ITaskItem> _copyLocalItems = new List<ITaskItem>();

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
        /// File Definitions, including metadata for the resolved path.
        /// </summary>
        [Required]
        public ITaskItem[] ContentFileDefinitions { get; set; }

        /// <summary>
        /// Subset of File Dependencies that are content files, including metadata 
        /// such as buildAction, ppOutputPath etc.
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

        #endregion

        #region Test Support

        public ProduceContentAssets()
        {
        }

        public ProduceContentAssets(IContentAssetPreprocessor assetPreprocessor)
            : this()
        {
            AssetPreprocessor = assetPreprocessor;
        }

        #endregion

        /// <summary>
        /// Resource for reading, processing and writing content assets
        /// </summary>
        public IContentAssetPreprocessor AssetPreprocessor { get; private set; }

        public override bool Execute()
        {
            try
            {
                ExecuteCore();
                return true;
            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e, showStackTrace: true);
                return false;
            }
        }

        private void ExecuteCore()
        {
            MapContentFileDefinitions();
            ProcessContentFileInputs();
        }
        
        private void MapContentFileDefinitions()
        {
            foreach (var item in ContentFileDefinitions ?? Enumerable.Empty<ITaskItem>())
            {
                _resolvedPaths.Add(item.ItemSpec, item.GetMetadata(MetadataKeys.ResolvedPath));
            }
        }

        private void ProcessContentFileInputs()
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
                    Log.LogWarning(
                        $"The preprocessor token &apos;{duplicatedPreprocessorKey}&apos; has been given more than one value." + 
                        $" Choosing &apos;{preprocessorValues[duplicatedPreprocessorKey]}&apos; as the value.");
                }

                if (AssetPreprocessor == null)
                {
                    AssetPreprocessor = new NugetContentAssetPreprocessor(ContentPreprocessorOutputDirectory, preprocessorValues);
                }
            }

            var contentFileDeps = ContentFileDependencies ?? Enumerable.Empty<ITaskItem>();
            var contentFileGroups = contentFileDeps.GroupBy(t => t.GetMetadata(MetadataKeys.ParentPackage));
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
                    if (grouping.Any(t => t.GetMetadata("codeLanguage") == projectLanguage))
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

                    if (contentFile.GetMetadata("codeLanguage") == codeLanguageToSelect)
                    {
                        ProduceContentAsset(contentFile);
                    }
                }
            }
        }

        private void ProduceContentAsset(ITaskItem contentFile)
        {
            string resolvedPath;
            if (!_resolvedPaths.TryGetValue(contentFile.ItemSpec, out resolvedPath))
            {
                Log.LogWarning($"Unable to find resolved path for {contentFile.ItemSpec}");
                return;
            }

            string pathToFinalAsset = resolvedPath;
            string ppOutputPath = contentFile.GetMetadata("ppOutputPath");
            string parentPackage = contentFile.GetMetadata(MetadataKeys.ParentPackage);

            if (ppOutputPath != null)
            {
                if (AssetPreprocessor == null)
                {
                    throw new Exception($"The {nameof(ContentPreprocessorOutputDirectory)} property must be set in order to consume preprocessed content");
                }

                string [] parts = parentPackage?.Split('/');
                if (parts == null)
                {
                    throw new Exception($"Content File {contentFile.ItemSpec} does not contain expected parent package information");
                }

                // We need the preprocessed output, so let's run the preprocessor here
                string relativeOutputPath = Path.Combine(parts[0], parts[1], ppOutputPath);
                if (AssetPreprocessor.Process(resolvedPath, relativeOutputPath, out pathToFinalAsset))
                {
                    _fileWrites.Add(new TaskItem(pathToFinalAsset));
                }
            }

            if (string.Equals(contentFile.GetMetadata("copyToOutput"), "True", StringComparison.OrdinalIgnoreCase))
            {
                string outputPath = contentFile.GetMetadata("outputPath") ?? ppOutputPath;

                if (outputPath != null)
                {
                    var item = new TaskItem(pathToFinalAsset);
                    item.SetMetadata("TargetPath", outputPath);
                    item.SetMetadata(MetadataKeys.ParentPackage, parentPackage);

                    _copyLocalItems.Add(item);
                }
            }

            // TODO if build action is none do we even need to write the processed file above?
            string buildAction = contentFile.GetMetadata("buildAction");
            if (!string.Equals(buildAction, "none", StringComparison.OrdinalIgnoreCase))
            {
                var item = new TaskItem(pathToFinalAsset);
                item.SetMetadata(MetadataKeys.ParentPackage, parentPackage);

                // We'll put additional metadata on the item so we can convert it back to the real item group in our targets
                item.SetMetadata("buildAction", buildAction);

                // TODO is this needed for .NETCore?
                // If this is XAML, the build targets expect Link metadata to construct the relative path
                if (string.Equals(buildAction, "Page", StringComparison.OrdinalIgnoreCase))
                {
                    item.SetMetadata("Link", Path.Combine("NuGet", parentPackage, Path.GetFileName(resolvedPath)));
                }

                _contentItems.Add(item);
            }
        }
    }
}