// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks
{
    [DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
    public class StaticWebAsset
    {
        public string Identity { get; set; }

        public string SourceId { get; set; }

        public string SourceType { get; set; }

        public string ContentRoot { get; set; }

        public string BasePath { get; set; }

        public string RelativePath { get; set; }

        public string AssetKind { get; set; }

        public string AssetMode { get; set; }

        public string AssetRole { get; set; }

        public string AssetMergeBehavior { get; set; }

        public string AssetMergeSource { get; set; }

        public string RelatedAsset { get; set; }

        public string AssetTraitName { get; set; }

        public string AssetTraitValue { get; set; }

        public string CopyToOutputDirectory { get; set; }

        public string CopyToPublishDirectory { get; set; }

        public string OriginalItemSpec { get; set; }

        public static StaticWebAsset FromTaskItem(ITaskItem item)
        {
            var result = FromTaskItemCore(item);

            result.Normalize();
            result.Validate();

            return result;
        }

        // Iterate over the list of assets with the same Identity and choose the one closest to the asset kind we've been given:
        // The given asset kind here will always be Build or Publish.
        // We need to iterate over the assets, the moment we detect one asset for our specific kind, we return that asset
        // While we iterate over the list of assets we keep any asset of the `All` kind we find on a variable.
        // * If we find a more specific asset, we will ignore it in favor of the specific one.
        // * If we don't find a more specfic (Build or Publish) asset we will return the `All` asset.
        // We assume that the manifest is correct and don't try to deal with errors at this level, if for some reason we find more
        // than one type of asset we will just return all of them.
        // One exception to this is the `All` kind of assets, where we will just return the first two we find. The reason for it is
        // to avoid having to allocate a buffer to collect all the `All` assets.
        internal static IEnumerable<StaticWebAsset> ChooseNearestAssetKind(IEnumerable<StaticWebAsset> group, string assetKind)
        {
            StaticWebAsset allKindAssetCandidate = null;

            var ignoreAllKind = false;
            foreach (var item in group)
            {
                if (item.HasKind(assetKind))
                {
                    ignoreAllKind = true;

                    yield return item;
                }
                else if (!ignoreAllKind && item.IsBuildAndPublish())
                {
                    if (allKindAssetCandidate != null)
                    {
                        yield return allKindAssetCandidate;
                        yield return item;
                        yield break;
                    }
                    allKindAssetCandidate = item;
                }
            }

            if (!ignoreAllKind)
            {
                yield return allKindAssetCandidate;
            }
        }

        internal static bool ValidateAssetGroup(string path, IReadOnlyList<StaticWebAsset> group, out string reason)
        {
            StaticWebAsset prototypeItem = null;
            StaticWebAsset build = null;
            StaticWebAsset publish = null;
            StaticWebAsset all = null;
            foreach (var item in group)
            {
                prototypeItem ??= item;
                if (!prototypeItem.HasSourceId(item.SourceId))
                {
                    reason = $"Conflicting assets with the same target path '{path}'. For assets '{prototypeItem}' and '{item}' from different projects.";
                    return false;
                }

                build ??= item.IsBuildOnly() ? item : build;
                if (build != null && item.IsBuildOnly() && !ReferenceEquals(build, item))
                {
                    reason = $"Conflicting assets with the same target path '{path}'. For 'Build' assets '{build}' and '{item}'.";
                    return false;
                }

                publish ??= item.IsPublishOnly() ? item : publish;
                if (publish != null && item.IsPublishOnly() && !ReferenceEquals(publish, item))
                {
                    reason = $"Conflicting assets with the same target path '{path}'. For 'Publish' assets '{publish}' and '{item}'.";
                    return false;
                }

                all ??= item.IsBuildAndPublish() ? item : all;
                if (all != null && item.IsBuildAndPublish() && !ReferenceEquals(all, item))
                {
                    reason = $"Conflicting assets with the same target path '{path}'. For 'All' assets '{all}' and '{item}'.";
                    return false;
                }
            }
            reason = null;
            return true;
        }

        private bool HasKind(string assetKind) =>
            AssetKinds.IsKind(AssetKind, assetKind);

        public static StaticWebAsset FromV1TaskItem(ITaskItem item)
        {
            var result = FromTaskItemCore(item);
            result.ApplyDefaults();
            result.OriginalItemSpec = item.GetMetadata("FullPath");

            result.Normalize();
            result.Validate();

            return result;
        }

        private static StaticWebAsset FromTaskItemCore(ITaskItem item) =>
            new StaticWebAsset
            {
                // Register the identity as the full path since assets might have come
                // from packages and other sources and the identity (which is typically
                // just the relative path from the project) is not enough to locate them.
                Identity = item.GetMetadata("FullPath"),
                SourceType = item.GetMetadata(nameof(SourceType)),
                SourceId = item.GetMetadata(nameof(SourceId)),
                ContentRoot = item.GetMetadata(nameof(ContentRoot)),
                BasePath = item.GetMetadata(nameof(BasePath)),
                RelativePath = item.GetMetadata(nameof(RelativePath)),
                AssetKind = item.GetMetadata(nameof(AssetKind)),
                AssetMode = item.GetMetadata(nameof(AssetMode)),
                AssetRole = item.GetMetadata(nameof(AssetRole)),
                AssetMergeSource = item.GetMetadata(nameof(AssetMergeSource)),
                AssetMergeBehavior = item.GetMetadata(nameof(AssetMergeBehavior)),
                RelatedAsset = item.GetMetadata(nameof(RelatedAsset)),
                AssetTraitName = item.GetMetadata(nameof(AssetTraitName)),
                AssetTraitValue = item.GetMetadata(nameof(AssetTraitValue)),
                CopyToOutputDirectory = item.GetMetadata(nameof(CopyToOutputDirectory)),
                CopyToPublishDirectory = item.GetMetadata(nameof(CopyToPublishDirectory)),
                OriginalItemSpec = item.GetMetadata(nameof(OriginalItemSpec)),
            };

        public void ApplyDefaults()
        {
            CopyToOutputDirectory = string.IsNullOrEmpty(CopyToOutputDirectory) ? AssetCopyOptions.Never : CopyToOutputDirectory;
            CopyToPublishDirectory = string.IsNullOrEmpty(CopyToPublishDirectory) ? AssetCopyOptions.PreserveNewest : CopyToPublishDirectory;
            AssetKind = !string.IsNullOrEmpty(AssetKind) ? AssetKind : !ShouldCopyToPublishDirectory() ? AssetKinds.Build : AssetKinds.All;
            AssetMode = string.IsNullOrEmpty(AssetMode) ? AssetModes.All : AssetMode;
            AssetRole = string.IsNullOrEmpty(AssetRole) ? AssetRoles.Primary : AssetRole;
        }

        public string ComputeTargetPath(string pathPrefix, char separator)
        {
            var prefix = pathPrefix != null ? Normalize(pathPrefix) : "";
            // These have been normalized already, so only contain forward slashes
            string computedBasePath = IsDiscovered() || IsComputed() ? "" : BasePath;
            if (computedBasePath == "/")
            {
                // We need to special case the base path "/" to make sure it gets correctly combined with the prefix
                computedBasePath = "";
            }
            return Path.Combine(prefix, computedBasePath, RelativePath)
                .Replace('/', separator)
                .Replace('\\', separator)
                .TrimStart(separator);
        }

        public ITaskItem ToTaskItem()
        {
            var result = new TaskItem(Identity);
            result.SetMetadata(nameof(SourceType), SourceType);
            result.SetMetadata(nameof(SourceId), SourceId);
            result.SetMetadata(nameof(ContentRoot), ContentRoot);
            result.SetMetadata(nameof(BasePath), BasePath);
            result.SetMetadata(nameof(RelativePath), RelativePath);
            result.SetMetadata(nameof(AssetKind), AssetKind);
            result.SetMetadata(nameof(AssetMode), AssetMode);
            result.SetMetadata(nameof(AssetRole), AssetRole);
            result.SetMetadata(nameof(AssetMergeSource), AssetMergeSource);
            result.SetMetadata(nameof(AssetMergeBehavior), AssetMergeBehavior);
            result.SetMetadata(nameof(RelatedAsset), RelatedAsset);
            result.SetMetadata(nameof(AssetTraitName), AssetTraitName);
            result.SetMetadata(nameof(AssetTraitValue), AssetTraitValue);
            result.SetMetadata(nameof(CopyToOutputDirectory), CopyToOutputDirectory);
            result.SetMetadata(nameof(CopyToPublishDirectory), CopyToPublishDirectory);
            result.SetMetadata(nameof(OriginalItemSpec), OriginalItemSpec);
            return result;
        }

        public void Validate()
        {
            switch (SourceType)
            {
                case SourceTypes.Discovered:
                case SourceTypes.Computed:
                case SourceTypes.Project:
                case SourceTypes.Package:
                    break;
                default:
                    throw new InvalidOperationException($"Unknown mergeTarget type '{SourceType}' for '{Identity}'.");
            };

            if (string.IsNullOrEmpty(SourceId))
            {
                throw new InvalidOperationException($"The '{nameof(SourceId)}' for the asset must be defined for '{Identity}'.");
            }

            if (string.IsNullOrEmpty(ContentRoot))
            {
                throw new InvalidOperationException($"The '{nameof(ContentRoot)}' for the asset must be defined for '{Identity}'.");
            }

            if (string.IsNullOrEmpty(BasePath))
            {
                throw new InvalidOperationException($"The '{nameof(BasePath)}' for the asset must be defined for '{Identity}'.");
            }

            if (string.IsNullOrEmpty(RelativePath))
            {
                throw new InvalidOperationException($"The '{nameof(RelativePath)}' for the asset must be defined for '{Identity}'.");
            }

            if (string.IsNullOrEmpty(OriginalItemSpec))
            {
                throw new InvalidOperationException($"The '{nameof(OriginalItemSpec)}' for the asset must be defined for '{Identity}'.");
            }

            switch (AssetKind)
            {
                case AssetKinds.All:
                case AssetKinds.Build:
                case AssetKinds.Publish:
                    break;
                default:
                    throw new InvalidOperationException($"Unknown Asset kind '{AssetKind}' for '{Identity}'.");
            };

            switch (AssetMode)
            {
                case AssetModes.All:
                case AssetModes.CurrentProject:
                case AssetModes.Reference:
                    break;
                default:
                    throw new InvalidOperationException($"Unknown Asset mode '{AssetMode}' for '{Identity}'.");
            };

            switch (AssetRole)
            {
                case AssetRoles.Primary:
                case AssetRoles.Related:
                case AssetRoles.Alternative:
                    break;
                default:
                    throw new InvalidOperationException($"Unknown Asset role '{AssetRole}' for '{Identity}'.");
            };

            if (!IsPrimaryAsset() && string.IsNullOrEmpty(RelatedAsset))
            {
                throw new InvalidOperationException($"Related asset for '{AssetRole}' asset '{Identity}' is not defined.");
            }

            if (IsAlternativeAsset() && (string.IsNullOrEmpty(AssetTraitName) || string.IsNullOrEmpty(AssetTraitValue)))
            {
                throw new InvalidOperationException($"Alternative asset '{Identity}' does not define an asset trait name or value.");
            }
        }

        internal static StaticWebAsset FromProperties(
            string identity,
            string sourceId,
            string sourceType,
            string basePath,
            string relativePath,
            string contentRoot,
            string assetKind,
            string assetMode,
            string assetRole,
            string assetMergeSource,
            string relatedAsset,
            string assetTraitName,
            string assetTraitValue,
            string copyToOutputDirectory,
            string copyToPublishDirectory,
            string originalItemSpec)
        {
            var result = new StaticWebAsset
            {
                Identity = identity,
                SourceId = sourceId,
                SourceType = sourceType,
                ContentRoot = contentRoot,
                BasePath = basePath,
                RelativePath = relativePath,
                AssetKind = assetKind,
                AssetMode = assetMode,
                AssetRole = assetRole,
                AssetMergeSource = assetMergeSource,
                RelatedAsset = relatedAsset,
                AssetTraitName = assetTraitName,
                AssetTraitValue = assetTraitValue,
                CopyToOutputDirectory = copyToOutputDirectory,
                CopyToPublishDirectory = copyToPublishDirectory,
                OriginalItemSpec = originalItemSpec
            };

            result.ApplyDefaults();

            result.Normalize();
            result.Validate();

            return result;
        }

        internal bool HasSourceId(string source) =>
            StaticWebAsset.HasSourceId(SourceId, source);

        public void Normalize()
        {
            ContentRoot = !string.IsNullOrEmpty(ContentRoot) ? NormalizeContentRootPath(ContentRoot) : ContentRoot;
            BasePath = Normalize(BasePath);
            RelativePath = Normalize(RelativePath, allowEmpyPath: true);
            RelatedAsset = !string.IsNullOrEmpty(RelatedAsset) ? Path.GetFullPath(RelatedAsset) : RelatedAsset;
        }

        // Normalizes the given path to a content root path in the way we expect it:
        // * Converts the path to absolute with Path.GetFullPath(path) which takes care of normalizing
        //   the directory separators to use Path.DirectorySeparator
        // * Appends a trailing directory separator at the end.
        public static string NormalizeContentRootPath(string path)
            => Path.GetFullPath(path) +
            // We need to do .ToString because there is no EndsWith overload for chars in .net472
            (path.EndsWith(Path.DirectorySeparatorChar.ToString()), path.EndsWith(Path.AltDirectorySeparatorChar.ToString())) switch
            {
                (true, _) => "",
                (false, true) => "", // Path.GetFullPath will have normalized it to Path.DirectorySeparatorChar.
                (false, false) => Path.DirectorySeparatorChar
            };

        public bool IsComputed()
            => string.Equals(SourceType, SourceTypes.Computed, StringComparison.Ordinal);

        public bool IsDiscovered()
            => string.Equals(SourceType, SourceTypes.Discovered, StringComparison.Ordinal);

        public bool IsProject()
            => string.Equals(SourceType, SourceTypes.Project, StringComparison.Ordinal);

        public bool IsPackage()
            => string.Equals(SourceType, SourceTypes.Package, StringComparison.Ordinal);

        public bool IsBuildOnly()
            => string.Equals(AssetKind, AssetKinds.Build, StringComparison.Ordinal);

        public bool IsPublishOnly()
            => string.Equals(AssetKind, AssetKinds.Publish, StringComparison.Ordinal);

        public bool IsBuildAndPublish()
            => string.Equals(AssetKind, AssetKinds.All, StringComparison.Ordinal);

        public bool IsForCurrentProjectOnly()
            => string.Equals(AssetMode, AssetModes.CurrentProject, StringComparison.Ordinal);

        public bool IsForReferencedProjectsOnly()
            => string.Equals(AssetMode, AssetModes.Reference, StringComparison.Ordinal);

        public bool IsForCurrentAndReferencedProjects()
            => string.Equals(AssetMode, AssetModes.All, StringComparison.Ordinal);

        public bool IsPrimaryAsset()
            => string.Equals(AssetRole, AssetRoles.Primary, StringComparison.Ordinal);

        public bool IsRelatedAsset()
            => string.Equals(AssetRole, AssetRoles.Related, StringComparison.Ordinal);

        public bool IsAlternativeAsset()
            => string.Equals(AssetRole, AssetRoles.Alternative, StringComparison.Ordinal);

        public bool ShouldCopyToOutputDirectory()
            => !string.Equals(CopyToOutputDirectory, AssetCopyOptions.Never, StringComparison.Ordinal);

        public bool ShouldCopyToPublishDirectory()
            => !string.Equals(CopyToPublishDirectory, AssetCopyOptions.Never, StringComparison.Ordinal);

        public bool HasContentRoot(string path) =>
            string.Equals(ContentRoot, NormalizeContentRootPath(path), StringComparison.Ordinal);

        public static string Normalize(string path, bool allowEmpyPath = false)
        {
            var normalizedPath = path.Replace('\\', '/').Trim('/');
            return !allowEmpyPath && normalizedPath.Equals("") ? "/" : normalizedPath;
        }

        public static string ComputeAssetRelativePath(ITaskItem asset, out string metadataProperty)
        {
            var relativePath = asset.GetMetadata("RelativePath");
            if (!string.IsNullOrEmpty(relativePath))
            {
                metadataProperty = "RelativePath";
                return relativePath;
            }

            var targetPath = asset.GetMetadata("TargetPath");
            if (!string.IsNullOrEmpty(targetPath))
            {
                metadataProperty = "TargetPath";
                return targetPath;
            }

            var linkPath = asset.GetMetadata("Link");
            if (!string.IsNullOrEmpty(linkPath))
            {
                metadataProperty = "Link";
                return linkPath;
            }

            metadataProperty = null;
            return asset.ItemSpec;
        }

        public override bool Equals(object obj)
        {
            return obj is StaticWebAsset asset &&
                   Identity == asset.Identity &&
                   SourceType == asset.SourceType &&
                   SourceId == asset.SourceId &&
                   ContentRoot == asset.ContentRoot &&
                   BasePath == asset.BasePath &&
                   RelativePath == asset.RelativePath &&
                   AssetKind == asset.AssetKind &&
                   AssetMode == asset.AssetMode &&
                   AssetRole == asset.AssetRole &&
                   AssetMergeSource == asset.AssetMergeSource &&
                   AssetMergeBehavior == asset.AssetMergeBehavior &&
                   RelatedAsset == asset.RelatedAsset &&
                   AssetTraitName == asset.AssetTraitName &&
                   AssetTraitValue == asset.AssetTraitValue &&
                   CopyToOutputDirectory == asset.CopyToOutputDirectory &&
                   CopyToPublishDirectory == asset.CopyToPublishDirectory &&
                   OriginalItemSpec == asset.OriginalItemSpec;
        }

        public static class AssetModes
        {
            public const string CurrentProject = nameof(CurrentProject);
            public const string Reference = nameof(Reference);
            public const string All = nameof(All);
        }

        public static class AssetKinds
        {
            public const string Build = nameof(Build);
            public const string Publish = nameof(Publish);
            public const string All = nameof(All);

            public static bool IsPublish(string assetKind) => string.Equals(Publish, assetKind, StringComparison.Ordinal);
            public static bool IsBuild(string assetKind) => string.Equals(Build, assetKind, StringComparison.Ordinal);
            internal static bool IsKind(string candidate, string assetKind) => string.Equals(candidate, assetKind, StringComparison.Ordinal);
            internal static bool IsAll(string assetKind) => string.Equals(All, assetKind, StringComparison.Ordinal);
        }

        public static class SourceTypes
        {
            public const string Discovered = nameof(Discovered);
            public const string Computed = nameof(Computed);
            public const string Project = nameof(Project);
            public const string Package = nameof(Package);

            public static bool IsPackage(string sourceType) => string.Equals(Package, sourceType, StringComparison.Ordinal);
        }

        public static class AssetCopyOptions
        {
            public const string Never = nameof(Never);
            public const string PreserveNewest = nameof(PreserveNewest);
            public const string Always = nameof(Always);
        }

        public static class AssetRoles
        {
            public const string Primary = nameof(Primary);
            public const string Related = nameof(Related);
            public const string Alternative = nameof(Alternative);

            internal static bool IsPrimary(string assetRole)
                => string.Equals(assetRole, Primary, StringComparison.Ordinal);
        }

        public static class MergeBehaviors
        {
            public const string Exclude = nameof(Exclude);
            public const string PreferTarget = nameof(PreferTarget);
            public const string PreferSource = nameof(PreferSource);
            public const string None = nameof(None);
        }

        internal static bool HasSourceId(ITaskItem asset, string source) =>
            string.Equals(asset.GetMetadata(nameof(SourceId)), source, StringComparison.Ordinal);

        internal static bool HasSourceId(string candidate, string source) =>
            string.Equals(candidate, source, StringComparison.Ordinal);

        private string GetDebuggerDisplay()
        {
            return ToString();
        }

        public override string ToString() =>
            $"Identity: {Identity}, " +
            $"SourceType: {SourceType}, " +
            $"SourceId: {SourceId}, " +
            $"ContentRoot: {ContentRoot}, " +
            $"BasePath: {BasePath}, " +
            $"RelativePath: {RelativePath}, " +
            $"AssetKind: {AssetKind}, " +
            $"AssetMode: {AssetMode}, " +
            $"AssetRole: {AssetRole}, " +
            $"AssetRole: {AssetMergeSource}, " +
            $"AssetRole: {AssetMergeBehavior}, " +
            $"RelatedAsset: {RelatedAsset}, " +
            $"AssetTraitName: {AssetTraitName}, " +
            $"AssetTraitValue: {AssetTraitValue}, " +
            $"CopyToOutputDirectory: {CopyToOutputDirectory}, " +
            $"CopyToPublishDirectory: {CopyToPublishDirectory}, " +
            $"OriginalItemSpec: {OriginalItemSpec}";

        public override int GetHashCode()
        {
#if NET6_0_OR_GREATER
            HashCode hash = new HashCode();
            hash.Add(Identity);
            hash.Add(SourceType);
            hash.Add(SourceId);
            hash.Add(ContentRoot);
            hash.Add(BasePath);
            hash.Add(RelativePath);
            hash.Add(AssetKind);
            hash.Add(AssetMode);
            hash.Add(AssetRole);
            hash.Add(AssetMergeSource);
            hash.Add(AssetMergeBehavior);
            hash.Add(RelatedAsset);
            hash.Add(AssetTraitName);
            hash.Add(AssetTraitValue);
            hash.Add(CopyToOutputDirectory);
            hash.Add(CopyToPublishDirectory);
            hash.Add(OriginalItemSpec);
            return hash.ToHashCode();
#else
            int hashCode = 1447485498;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Identity);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(SourceType);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(SourceId);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(ContentRoot);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(BasePath);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(RelativePath);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(AssetKind);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(AssetMode);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(AssetRole);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(AssetMergeSource);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(AssetMergeBehavior);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(RelatedAsset);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(AssetTraitName);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(AssetTraitValue);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(CopyToOutputDirectory);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(CopyToPublishDirectory);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(OriginalItemSpec);
            return hashCode;
#endif
        }
    }
}
