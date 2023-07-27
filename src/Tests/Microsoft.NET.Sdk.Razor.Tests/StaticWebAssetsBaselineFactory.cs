// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.StaticWebAssets.Tasks;

namespace Microsoft.NET.Sdk.Razor.Tests;
public class StaticWebAssetsBaselineFactory
{
    public static StaticWebAssetsBaselineFactory Instance { get; } = new();

    public IList<string> KnownExtensions { get; } = new List<string>()
    {
        // Keep this list of most specific to less specific
        ".dll.gz",
        ".dll.br",
        ".dll",
        ".wasm.gz",
        ".wasm.br",
        ".wasm",
        ".js.gz",
        ".js.br",
        ".js",
        ".html",
        ".pdb",
    };

    public IList<string> KnownFilePrefixesWithHashOrVersion { get; } = new List<string>()
    {
        "dotnet"
    };

    public void ToTemplate(
        StaticWebAssetsManifest manifest,
        string projectRoot,
        string restorePath,
        string runtimeIdentifier)
    {
        manifest.Hash = "__hash__";
        var assetsByIdentity = manifest.Assets.ToDictionary(a => a.Identity);
        foreach (var asset in manifest.Assets)
        {
            TemplatizeAsset(projectRoot, restorePath, runtimeIdentifier, asset);
            if (asset.AssetTraitName == "Content-Encoding")
            {
                var relativePath = asset.RelativePath.Replace('/', Path.DirectorySeparatorChar);
                var identity = asset.Identity.Replace('\\', Path.DirectorySeparatorChar);
                var originalItemSpec = asset.OriginalItemSpec.Replace('\\', Path.DirectorySeparatorChar);

                asset.Identity = Path.Combine(Path.GetDirectoryName(identity), relativePath);
                asset.Identity = asset.Identity.Replace(Path.DirectorySeparatorChar, '\\');
                asset.OriginalItemSpec = Path.Combine(Path.GetDirectoryName(originalItemSpec), relativePath);
                asset.OriginalItemSpec = asset.OriginalItemSpec.Replace(Path.DirectorySeparatorChar, '\\');
            }
            else if ((asset.Identity.EndsWith(".gz") || asset.Identity.EndsWith(".br"))
                && asset.AssetTraitName == "" && asset.RelatedAsset == "")
            {
                // Old .NET 5.0 implementation
                var identity = asset.Identity.Replace('\\', Path.DirectorySeparatorChar);
                var originalItemSpec = asset.OriginalItemSpec.Replace('\\', Path.DirectorySeparatorChar);

                asset.Identity = Path.Combine(Path.GetDirectoryName(identity), Path.GetFileName(originalItemSpec) + Path.GetExtension(identity))
                    .Replace(Path.DirectorySeparatorChar, '\\');
            }
        }

        foreach (var discovery in manifest.DiscoveryPatterns)
        {
            discovery.ContentRoot = discovery.ContentRoot.Replace(projectRoot, "${ProjectPath}");
            discovery.ContentRoot = discovery.ContentRoot.Replace(Path.DirectorySeparatorChar, '\\');

            discovery.Name = discovery.Name.Replace(Path.DirectorySeparatorChar, '\\');
            discovery.Pattern = discovery.Pattern.Replace(Path.DirectorySeparatorChar, '\\');
        }

        foreach (var relatedManifest in manifest.ReferencedProjectsConfiguration)
        {
            relatedManifest.Identity = relatedManifest.Identity.Replace(projectRoot, "${ProjectPath}").Replace(Path.DirectorySeparatorChar, '\\');
        }

        // Sor everything now to ensure we produce stable baselines independent of the machine they were generated on.
        Array.Sort(manifest.DiscoveryPatterns, (l, r) => StringComparer.Ordinal.Compare(l.Name, r.Name));
        Array.Sort(manifest.Assets, (l, r) => StringComparer.Ordinal.Compare(l.Identity, r.Identity));
        Array.Sort(manifest.ReferencedProjectsConfiguration, (l, r) => StringComparer.Ordinal.Compare(l.Identity, r.Identity));
    }

    private void TemplatizeAsset(string projectRoot, string restorePath, string runtimeIdentifier, StaticWebAsset asset)
    {
        asset.Identity = TemplatizeFilePath(
            asset.Identity,
            restorePath,
            projectRoot,
            null,
            null,
            runtimeIdentifier);

        asset.RelativePath = TemplatizeFilePath(
            asset.RelativePath,
            null,
            null,
            null,
            null,
            null).Replace('\\', '/');

        asset.ContentRoot = TemplatizeFilePath(
            asset.ContentRoot,
            restorePath,
            projectRoot,
            null,
            null,
            runtimeIdentifier);

        asset.RelatedAsset = TemplatizeFilePath(
            asset.RelatedAsset,
            restorePath,
            projectRoot,
            null,
            null,
            null);

        asset.OriginalItemSpec = TemplatizeFilePath(
            asset.OriginalItemSpec,
            restorePath,
            projectRoot,
            null,
            null,
            runtimeIdentifier);
    }

    internal IEnumerable<string> TemplatizeExpectedFiles(
        IEnumerable<string> files,
        string restorePath,
        string projectPath,
        string intermediateOutputPath,
        string buildOrPublishFolder)
    {
        foreach (var file in files)
        {
            var updated = TemplatizeFilePath(
                file,
                restorePath,
                projectPath,
                intermediateOutputPath,
                buildOrPublishFolder,
                null);

            yield return updated;
        }
    }

    public string TemplatizeFilePath(
        string file,
        string restorePath,
        string projectPath,
        string intermediateOutputPath,
        string buildOrPublishFolder,
        string runtimeIdentifier)
    {
        var updated = file switch
        {
            var processed when file.StartsWith("$") => processed,
            var fromBuildOrPublishPath when buildOrPublishFolder is not null && file.StartsWith(buildOrPublishFolder) =>
                TemplatizeBuildOrPublishPath(buildOrPublishFolder, fromBuildOrPublishPath),
            var fromIntermediateOutputPath when intermediateOutputPath is not null && file.StartsWith(intermediateOutputPath) =>
                TemplatizeIntermediatePath(intermediateOutputPath, fromIntermediateOutputPath),
            var fromPackage when restorePath is not null && file.StartsWith(restorePath) =>
                TemplatizeNugetPath(restorePath, fromPackage),
            var fromProject when projectPath is not null && file.StartsWith(projectPath) =>
                TemplatizeProjectPath(projectPath, fromProject, runtimeIdentifier),
            _ =>
                ReplaceSegments(file, (i, segments) => i switch
                {
                    2 when segments[0] is "obj" or "bin" => "${Tfm}",
                    var last when i == segments.Length - 1 => RemovePossibleHash(segments[last]),
                    _ => segments[i]
                })
        };

        return updated.Replace('/', '\\');
    }

    private string TemplatizeBuildOrPublishPath(string outputPath, string file)
    {
        file = file.Replace(outputPath, "${OutputPath}")
            .Replace('\\', '/');

        file = ReplaceSegments(file, (i, segments) => i switch
        {
            _ when i == segments.Length - 1 => RemovePossibleHash(segments[i]),
            _ => segments[i],
        });

        return file;
    }

    private string TemplatizeIntermediatePath(string intermediatePath, string file)
    {
        file = file.Replace(intermediatePath, "${IntermediateOutputPath}")
            .Replace('\\', '/');

        file = ReplaceSegments(file, (i, segments) => i switch
        {
            3 when segments[1] is "obj" or "bin" => "${Tfm}",
            _ when i == segments.Length - 1 => RemovePossibleHash(segments[i]),
            _ => segments[i]
        });

        return file;
    }

    private string TemplatizeProjectPath(string projectPath, string file, string runtimeIdentifier)
    {
        file = file.Replace(projectPath, "${ProjectPath}")
            .Replace('\\', '/');

        file = ReplaceSegments(file, (i, segments) => i switch
        {
            3 when segments[1] is "obj" or "bin" => "${Tfm}",
            4 when segments[2] is "obj" or "bin" => "${Tfm}",
            4 when segments[1] is "obj" or "bin" && segments[4] == runtimeIdentifier => "${Rid}",
            5 when segments[2] is "obj" or "bin" && segments[5] == runtimeIdentifier => "${Rid}",
            _ when i == segments.Length - 1 => RemovePossibleHash(segments[i]),
            _ => segments[i]
        });

        return file;
    }

    private string TemplatizeNugetPath(string restorePath, string file)
    {
        file = file.Replace(restorePath, "${RestorePath}")
            .Replace('\\', '/');
        if (file.Contains("runtimes"))
        {
            file = ReplaceSegments(file, (i, segments) => i switch
            {
                2 => "${RuntimeVersion}",
                6 when !file.Contains("native") => "${PackageTfm}",
                _ when i == segments.Length - 1 => RemovePossibleHash(segments[i]),
                _ => segments[i],
            });
        }
        else
        {
            file = ReplaceSegments(file, (i, segments) => i switch
            {
                2 => "${PackageVersion}",
                4 => "${PackageTfm}",
                _ when i == segments.Length - 1 => RemovePossibleHash(segments[i]),
                _ => segments[i],
            });
        }

        return file;
    }

    private static string ReplaceSegments(string file, Func<int, string[], string> selector)
    {
        var segments = file.Split('\\', '/');
        var newSegments = new List<string>();

        // Segments have the following shape `${RestorePath}/PackageName/PackageVersion/lib/Tfm/dll`.
        // We want to replace PackageVersion and Tfm with tokens so that they do not cause issues.
        for (var i = 0; i < segments.Length; i++)
        {
            newSegments.Add(selector(i, segments));
        }

        return string.Join(Path.DirectorySeparatorChar, newSegments);
    }

    private string RemovePossibleHash(string fileNameAndExtension)
    {
        var filename = KnownFilePrefixesWithHashOrVersion.FirstOrDefault(p => fileNameAndExtension.StartsWith(p));
        var extension = KnownExtensions.FirstOrDefault(f => fileNameAndExtension.EndsWith(f, StringComparison.OrdinalIgnoreCase));
        if (filename != null && extension != null)
        {
            fileNameAndExtension = filename + extension;
        }

        return fileNameAndExtension;
    }
}
