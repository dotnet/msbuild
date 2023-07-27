// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks
{
    // Handles the logic for resolving configurations and embedding rules during
    // single targeting builds and multi-targeting builds.
    // In single targeting builds, passing the TargetFramework is needed. We determine
    // which configurations are applicable to a given target framework based on the rules.
    // In multi-targeting builds, passing the TargetFrameworks is needed. We determine
    // which rules are applicable, and for those configurations we determine are applicable
    // we remove them from the list of FilteredCrossTargetingBuildConfigurations.
    // We filter the build configurations in cross targeting to reflect the dependencies between
    // TFMs, and we avoid building them in the multi-targeted build.

    public class ResolveStaticWebAssetsEmbeddedProjectConfiguration : Task
    {
        [Required]
        public ITaskItem[] StaticWebAssetProjectConfiguration { get; set; }

        [Required]
        public ITaskItem[] EmbeddingConfiguration { get; set; }

        public string TargetFramework { get; set; }

        public ITaskItem[] TargetFrameworks { get; set; } = Array.Empty<ITaskItem>();

        public ITaskItem[] CrossTargetingBuildConfigurations { get; set; } = Array.Empty<ITaskItem>();

        [Output]
        public ITaskItem[] EmbeddedProjectAssetConfigurations { get; set; }

        [Output]
        public ITaskItem[] FilteredCrossTargetingBuildConfigurations { get; set; }

        public override bool Execute()
        {
            var embeddedProjectAssetConfigurations = new EmbeddedStaticWebAssetProjectConfiguration[StaticWebAssetProjectConfiguration.Length];
            for (var i = 0; i < StaticWebAssetProjectConfiguration.Length; i++)
            {
                embeddedProjectAssetConfigurations[i] = EmbeddedStaticWebAssetProjectConfiguration.FromTaskItem(StaticWebAssetProjectConfiguration[i]);
            }

            var embeddingRules = new StaticWebAssetEmbeddingConfiguration[EmbeddingConfiguration.Length];
            for (var i = 0; i < EmbeddingConfiguration.Length; i++)
            {
                embeddingRules[i] = StaticWebAssetEmbeddingConfiguration.FromTaskItem(EmbeddingConfiguration[i]);
            }

            var targetFrameworks = TargetFrameworks.Any() ? TargetFrameworks.Select(t => t.ItemSpec).ToArray() : new[] { TargetFramework };

            var matchingConfigurations = new List<EmbeddedStaticWebAssetProjectConfiguration>();
            foreach (var targetFramework in targetFrameworks)
            {
                Log.LogMessage("Evaluating rules for target framework: '{0}'", targetFramework);
                foreach (var rule in embeddingRules)
                {
                    Log.LogMessage("Evaluate rule: '{0}'", rule.Id);
                    if (string.Equals(rule.Id, targetFramework, StringComparison.Ordinal))
                    {
                        Log.LogMessage("Rule matches target framework: '{0}'", rule.Id);
                        foreach (var configuration in embeddedProjectAssetConfigurations)
                        {
                            if (Matches(configuration, rule))
                            {
                                matchingConfigurations.Add(configuration);
                            }
                        }
                    }
                }
            }

            if (CrossTargetingBuildConfigurations.Any())
            {
                var filteredConfigurations = new List<ITaskItem>();
                foreach (var configurationsToRemove in CrossTargetingBuildConfigurations)
                {
                    foreach (var embeddedConfiguration in matchingConfigurations)
                    {
                        if (configurationsToRemove.GetMetadata("AdditionalProperties")?
                            .Contains($"TargetFramework={embeddedConfiguration.TargetFramework}") == true)
                        {
                            Log.LogMessage($"Removing configuration '{configurationsToRemove.GetMetadata("AdditonalProperties")}' because it is embedded.");
                            // We remove the configuration because there is a rule to embed it.
                            filteredConfigurations.Add(configurationsToRemove);
                        }
                    }
                }

                FilteredCrossTargetingBuildConfigurations = CrossTargetingBuildConfigurations.Except(filteredConfigurations).ToArray();
            }

            EmbeddedProjectAssetConfigurations = matchingConfigurations.Select(c => c.ToTaskItem()).ToArray();

            return !Log.HasLoggedErrors;
        }

        private bool Matches(EmbeddedStaticWebAssetProjectConfiguration configuration, StaticWebAssetEmbeddingConfiguration rule)
        {
            // If the rule specifies a concrete target framework, it must match.
            if (!string.IsNullOrWhiteSpace(rule.TargetFramework) &&
                !string.Equals(rule.TargetFramework, configuration.TargetFramework, StringComparison.Ordinal))
            {
                Log.LogMessage("Project configuration not applicable due to framework mismatch: '{0}' != '{1}'", rule.TargetFramework, configuration.TargetFramework);
                return false;
            }

            // If the rule specifies a concrete target framework identifier, it must match.
            if (!string.IsNullOrWhiteSpace(rule.TargetFrameworkIdentifier) &&
                !string.Equals(rule.TargetFrameworkIdentifier, configuration.TargetFrameworkIdentifier, StringComparison.Ordinal))
            {
                Log.LogMessage("Project configuration not applicable due to framework identifier mismatch: '{0}' != '{1}'", rule.TargetFrameworkIdentifier, configuration.TargetFrameworkIdentifier);
                return false;
            }

            // If the rule specifies a concrete target framework version, it must match.
            if (!string.IsNullOrWhiteSpace(rule.TargetFrameworkVersion) &&
                !string.Equals(rule.TargetFrameworkVersion, configuration.TargetFrameworkVersion, StringComparison.Ordinal))
            {
                Log.LogMessage("Project configuration not applicable due to framework version mismatch: '{0}' != '{1}'", rule.TargetFrameworkVersion, configuration.TargetFrameworkVersion);
                return false;
            }

            // If the rule specifies a concrete platform, it must match.
            if (!string.IsNullOrWhiteSpace(rule.Platform) &&
                !string.Equals(rule.Platform, configuration.Platform, StringComparison.Ordinal))
            {
                Log.LogMessage("Project configuration not applicable due to platform mismatch: '{0}' != '{1}'", rule.Platform, configuration.Platform);
                return false;
            }

            // If the rule specifies a concrete platform version, it must match.
            if (!string.IsNullOrWhiteSpace(rule.PlatformVersion) &&
                !string.Equals(rule.PlatformVersion, configuration.PlatformVersion, StringComparison.Ordinal))
            {
                Log.LogMessage("Project configuration not applicable due to platform version mismatch: '{0}' != '{1}'", rule.PlatformVersion, configuration.PlatformVersion);
                return false;
            }

            Log.LogMessage("Project configuration applicable: '{0}'", configuration.TargetFramework);
            return true;
        }
    }

    public class EmbeddedStaticWebAssetProjectConfiguration
    {
        public string Id { get; set; }
        public string Version { get; set; }
        public string Source { get; set; }
        public string GetEmbeddedBuildAssetsTargets { get; set; }
        public string AdditionalEmbeddedBuildProperties { get; set; }
        public string AdditionalEmbeddedBuildPropertiesToRemove { get; set; }
        public string GetEmbeddedPublishAssetsTargets { get; set; }
        public string AdditionalEmbeddedPublishProperties { get; set; }
        public string AdditionalEmbeddedPublishPropertiesToRemove { get; set; }
        public string TargetFramework { get; set; }
        public string TargetFrameworkIdentifier { get; set; }
        public string TargetFrameworkVersion { get; set; }
        public string Platform { get; set; }
        public string PlatformVersion { get; set; }

        public ITaskItem2 ToTaskItem()
        {
            var result = new TaskItem(Id);
            result.SetMetadata(nameof(Version), Version);
            result.SetMetadata(nameof(Source), Source);
            result.SetMetadata(nameof(GetEmbeddedBuildAssetsTargets), GetEmbeddedBuildAssetsTargets);
            result.SetMetadata(nameof(AdditionalEmbeddedBuildProperties), AdditionalEmbeddedBuildProperties);
            result.SetMetadata(nameof(AdditionalEmbeddedBuildPropertiesToRemove), AdditionalEmbeddedBuildPropertiesToRemove);
            result.SetMetadata(nameof(GetEmbeddedPublishAssetsTargets), GetEmbeddedPublishAssetsTargets);
            result.SetMetadata(nameof(AdditionalEmbeddedPublishProperties), AdditionalEmbeddedPublishProperties);
            result.SetMetadata(nameof(AdditionalEmbeddedPublishPropertiesToRemove), AdditionalEmbeddedPublishPropertiesToRemove);
            result.SetMetadata(nameof(TargetFramework), TargetFramework);

            return result;
        }

        public static EmbeddedStaticWebAssetProjectConfiguration FromTaskItem(ITaskItem source)
        {
            return new EmbeddedStaticWebAssetProjectConfiguration
            {
                Id = source.ItemSpec,
                Version = source.GetMetadata(nameof(Version)),
                Source = source.GetMetadata(nameof(Source)),
                GetEmbeddedBuildAssetsTargets = source.GetMetadata(nameof(GetEmbeddedBuildAssetsTargets)),
                AdditionalEmbeddedBuildProperties = source.GetMetadata(nameof(AdditionalEmbeddedBuildProperties)),
                AdditionalEmbeddedBuildPropertiesToRemove = source.GetMetadata(nameof(AdditionalEmbeddedBuildPropertiesToRemove)),
                GetEmbeddedPublishAssetsTargets = source.GetMetadata(nameof(GetEmbeddedPublishAssetsTargets)),
                AdditionalEmbeddedPublishProperties = source.GetMetadata(nameof(AdditionalEmbeddedPublishProperties)),
                AdditionalEmbeddedPublishPropertiesToRemove = source.GetMetadata(nameof(AdditionalEmbeddedPublishPropertiesToRemove)),
                TargetFramework = source.GetMetadata(nameof(TargetFramework)),
                TargetFrameworkIdentifier = source.GetMetadata(nameof(TargetFrameworkIdentifier)),
                TargetFrameworkVersion = source.GetMetadata(nameof(TargetFrameworkVersion)),
                Platform = source.GetMetadata(nameof(Platform)),
                PlatformVersion = source.GetMetadata(nameof(PlatformVersion))
            };
        }
    }

    // Defines the rules for which other TFM's assets should be embedded into the current TFM.
    // The rules are defined as an embeddedConfiguration group as follows
    // <StaticWebAssetsEmbeddingConfiguration Include="<<target-tfm>>">
    //    <Platform>browser</Platform>
    // <StaticWebAssetsEmbeddingConfiguration>
    // For example, to embed wasm asssets within a non wasm project, you would have the following
    // <StaticWebAssetsEmbeddingConfiguration Include="$(TargetFramework)"
    // Condition="'$([MSBuild]::GetTargetPlatformIdentifier($(TargetFramework)))' == ''">
    //    <Platform>browser</Platform>
    // <StaticWebAssetsEmbeddingConfiguration>
    // This essentially enables the rule when we are targeting for example, net8.0, but does not
    // require the rule to be defined for net8.0 specifically.
    // It also means that if in the future you are targeting net9.0, you don't need to update the
    // browser to net9.0-browser.
    // During the evaluation, we match the rule ID (in the Include attribute) with the current TFM
    // and if it matches, we evaluate all the other metadata against the available project configurations
    // and determine all the ones that match, which we return.
    public class StaticWebAssetEmbeddingConfiguration
    {
        public string Id { get; set; }

        public string TargetFramework { get; set; }

        public string Platform { get; set; }

        public string PlatformVersion { get; set; }

        public string TargetFrameworkIdentifier { get; set; }

        public string TargetFrameworkVersion { get; set; }

        public static StaticWebAssetEmbeddingConfiguration FromTaskItem(ITaskItem item)
        {
            return new StaticWebAssetEmbeddingConfiguration
            {
                Id = item.ItemSpec,
                TargetFramework = item.GetMetadata("TargetFramework"),
                Platform = item.GetMetadata("Platform"),
                PlatformVersion = item.GetMetadata("PlatformVersion"),
                TargetFrameworkIdentifier = item.GetMetadata("TargetFrameworkIdentifier"),
                TargetFrameworkVersion = item.GetMetadata("TargetFrameworkVersion"),
            };
        }

        public static ITaskItem2 ToTaskItem(StaticWebAssetEmbeddingConfiguration configuration)
        {
            var item = new TaskItem(configuration.Id);
            item.SetMetadata("TargetFramework", configuration.TargetFramework);
            item.SetMetadata("Platform", configuration.Platform);
            item.SetMetadata("PlatformVersion", configuration.PlatformVersion);
            item.SetMetadata("TargetFrameworkIdentifier", configuration.TargetFrameworkIdentifier);
            item.SetMetadata("TargetFrameworkVersion", configuration.TargetFrameworkVersion);
            return item;
        }
    }
}

