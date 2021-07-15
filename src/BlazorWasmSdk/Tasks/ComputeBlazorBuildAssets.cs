// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Sdk.BlazorWebAssembly
{
    // This task does the build work of processing the project inputs and producing a set of pseudo-static web assets
    // specific to Blazor.
    public class ComputeBlazorBuildAssets : Task
    {
        [Required]
        public ITaskItem[] Candidates { get; set; }

        [Required]
        public ITaskItem[] ProjectAssembly { get; set; }

        [Required]
        public ITaskItem[] ProjectDebugSymbols { get; set; }

        [Required]
        public ITaskItem[] SatelliteAssemblies { get; set; }

        [Required]
        public ITaskItem[] ProjectSatelliteAssemblies { get; set; }

        [Required]
        public string OutputPath { get; set; }

        [Required]
        public bool TimeZoneSupport { get; set; }

        [Required]
        public bool InvariantGlobalization { get; set; }

        [Required]
        public bool CopySymbols { get; set; }

        [Output]
        public ITaskItem[] AssetCandidates { get; set; }

        [Output]
        public ITaskItem[] FilesToRemove { get; set; }

        public override bool Execute()
        {
            var filesToRemove = new List<ITaskItem>();
            var assetCandidates = new List<ITaskItem>();

            try
            {
                if (ProjectAssembly.Length != 1)
                {
                    Log.LogError("Invalid number of project assemblies '{0}'", string.Join("," + Environment.NewLine, ProjectAssembly.Select(a => a.ItemSpec)));
                    return true;
                }

                if (ProjectDebugSymbols.Length > 1)
                {
                    Log.LogError("Invalid number of symbol assemblies '{0}'", string.Join("," + Environment.NewLine, ProjectDebugSymbols.Select(a => a.ItemSpec)));
                    return true;
                }

                for (int i = 0; i < Candidates.Length; i++)
                {
                    var candidate = Candidates[i];
                    if (ShouldFilterCandidate(candidate, TimeZoneSupport, InvariantGlobalization, CopySymbols, out var reason))
                    {
                        Log.LogMessage("Skipping asset '{0}' becasue '{1}'", candidate.ItemSpec, reason);
                        filesToRemove.Add(candidate);
                        continue;
                    }

                    var satelliteAssembly = SatelliteAssemblies.FirstOrDefault(s => s.ItemSpec == candidate.ItemSpec);
                    if (satelliteAssembly != null)
                    {
                        var inferredCulture = satelliteAssembly.GetMetadata("DestinationSubDirectory").Trim('\\', '/');
                        Log.LogMessage("Found satellite assembly '{0}' asset for candidate '{1}' with inferred culture '{2}'", satelliteAssembly.ItemSpec, candidate.ItemSpec, inferredCulture);

                        var assetCandidate = new TaskItem(satelliteAssembly);
                        assetCandidate.SetMetadata("AssetKind", "Build");
                        assetCandidate.SetMetadata("AssetRole", "Related");
                        assetCandidate.SetMetadata("AssetTraitName", "Culture");
                        assetCandidate.SetMetadata("AssetTraitValue", inferredCulture);
                        assetCandidate.SetMetadata("RelativePath", $"_framework/{inferredCulture}/{satelliteAssembly.GetMetadata("FileName")}{satelliteAssembly.GetMetadata("Extension")}");
                        assetCandidate.SetMetadata("RelatedAsset", Path.GetFullPath(Path.Combine(OutputPath, "wwwroot", "_framework", Path.GetFileName(assetCandidate.GetMetadata("ResolvedFrom")))));

                        assetCandidates.Add(assetCandidate);
                        continue;
                    }

                    var destinationSubPath = candidate.GetMetadata("DestinationSubPath");
                    if (string.IsNullOrEmpty(destinationSubPath))
                    {
                        var relativePath = candidate.GetMetadata("FileName") + candidate.GetMetadata("Extension");
                        candidate.SetMetadata("RelativePath", $"_framework/{relativePath}");
                    }
                    else
                    {
                        candidate.SetMetadata("RelativePath", $"_framework/{destinationSubPath}");
                    }

                    var culture = candidate.GetMetadata("Culture");
                    if (!string.IsNullOrEmpty(culture))
                    {
                        candidate.SetMetadata("AssetKind", "Build");
                        candidate.SetMetadata("AssetRole", "Related");
                        candidate.SetMetadata("AssetTraitName", "Culture");
                        candidate.SetMetadata("AssetTraitValue", culture);
                        var fileName = candidate.GetMetadata("FileName");
                        var suffixIndex = fileName.Length - ".resources".Length;
                        var relatedAssetPath = Path.GetFullPath(Path.Combine(
                            OutputPath,
                            "wwwroot",
                            "_framework",
                            fileName.Substring(0, suffixIndex) + ProjectAssembly[0].GetMetadata("Extension")));

                        candidate.SetMetadata("RelatedAsset", relatedAssetPath);

                        Log.LogMessage("Found satellite assembly '{0}' asset for inferred candidate '{1}' with culture '{2}'", candidate.ItemSpec, relatedAssetPath, culture);
                    }

                    assetCandidates.Add(candidate);
                }

                var intermediateAssembly = new TaskItem(ProjectAssembly[0]);
                intermediateAssembly.SetMetadata("RelativePath", $"_framework/{intermediateAssembly.GetMetadata("FileName")}{intermediateAssembly.GetMetadata("Extension")}");
                assetCandidates.Add(intermediateAssembly);

                if (ProjectDebugSymbols.Length > 0)
                {
                    var debugSymbols = new TaskItem(ProjectDebugSymbols[0]);
                    debugSymbols.SetMetadata("RelativePath", $"_framework/{debugSymbols.GetMetadata("FileName")}{debugSymbols.GetMetadata("Extension")}");
                    assetCandidates.Add(debugSymbols);
                }

                for (int i = 0; i < ProjectSatelliteAssemblies.Length; i++)
                {
                    var projectSatelliteAssembly = ProjectSatelliteAssemblies[i];
                    var candidateCulture = projectSatelliteAssembly.GetMetadata("Culture");
                    Log.LogMessage(
                        "Found satellite assembly '{0}' asset for project '{1}' with culture '{2}'",
                        projectSatelliteAssembly.ItemSpec,
                        intermediateAssembly.ItemSpec,
                        candidateCulture);

                    var assetCandidate = new TaskItem(Path.GetFullPath(projectSatelliteAssembly.ItemSpec), projectSatelliteAssembly.CloneCustomMetadata());
                    var projectAssemblyAssetPath = Path.GetFullPath(Path.Combine(
                        OutputPath,
                        "wwwroot",
                        "_framework",
                        ProjectAssembly[0].GetMetadata("FileName") + ProjectAssembly[0].GetMetadata("Extension")));

                    var normalizedPath = assetCandidate.GetMetadata("TargetPath").Replace('\\',  '/');

                    assetCandidate.SetMetadata("AssetKind", "Build");
                    assetCandidate.SetMetadata("AssetRole", "Related");
                    assetCandidate.SetMetadata("AssetTraitName", "Culture");
                    assetCandidate.SetMetadata("AssetTraitValue", candidateCulture);
                    assetCandidate.SetMetadata("RelativePath", Path.Combine("_framework", normalizedPath));
                    assetCandidate.SetMetadata("RelatedAsset", projectAssemblyAssetPath);

                    assetCandidates.Add(assetCandidate);
                }

                for (var i = 0; i < assetCandidates.Count; i++)
                {
                    var candidate = assetCandidates[i];
                    ApplyUniqueMetadataProperties(candidate);
                }
            }
            catch (Exception ex)
            {
                Log.LogError(ex.ToString());
                return false;
            }

            FilesToRemove = filesToRemove.ToArray();
            AssetCandidates = assetCandidates.ToArray();

            return !Log.HasLoggedErrors;
        }

        private static void ApplyUniqueMetadataProperties(ITaskItem candidate)
        {
            var extension = candidate.GetMetadata("Extension");
            var filename = candidate.GetMetadata("FileName");
            switch (extension)
            {
                case ".dll":
                    if (string.IsNullOrEmpty(candidate.GetMetadata("AssetTraitName")))
                    {
                        candidate.SetMetadata("AssetTraitName", "BlazorWebAssemblyResource");
                        candidate.SetMetadata("AssetTraitValue", "runtime");
                    }
                    break;
                case ".wasm":
                case ".blat":
                case ".dat" when filename.StartsWith("icudt"):
                    candidate.SetMetadata("AssetTraitName", "BlazorWebAssemblyResource");
                    candidate.SetMetadata("AssetTraitValue", "native");
                    break;
                case ".pdb":
                    candidate.SetMetadata("AssetTraitName", "BlazorWebAssemblyResource");
                    candidate.SetMetadata("AssetTraitValue", "symbol");
                    candidate.RemoveMetadata("OriginalItemSpec");
                    break;
                default:
                    break;
            }
        }

        public static bool ShouldFilterCandidate(
            ITaskItem candidate,
            bool timezoneSupport,
            bool invariantGlobalization,
            bool copySymbols,
            out string reason)
        {
            var extension = candidate.GetMetadata("Extension");
            var fileName = candidate.GetMetadata("FileName");
            var assetType = candidate.GetMetadata("AssetType");
            reason = extension switch
            {
                ".a" => "extension is .a is not supported.",
                ".c" => "extension is .c is not supported.",
                ".h" => "extension is .h is not supported.",
                ".rsp" => "extension is .rsp is not supported.",
                ".props" => "extension is .props is not supported.",
                ".blat" when !timezoneSupport => "timezone support is not enabled.",
                ".dat" when invariantGlobalization && fileName.StartsWith("icudt") => "invariant globalization is enabled",
                ".js" when fileName == "dotnet" => "dotnet js is already processed by Blazor",
                ".js" when assetType == "native" => "dotnet js is already processed by Blazor",
                ".pdb" when !copySymbols => "copying symbols is disabled",
                _ => null
            };

            return reason != null;
        }
    }
}
