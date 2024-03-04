// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json;
using Microsoft.Build.Experimental.BuildCop;
using System.Configuration;
using Microsoft.Build.BuildCop.Infrastructure.EditorConfig;

namespace Microsoft.Build.BuildCop.Infrastructure;


// TODO: https://github.com/dotnet/msbuild/issues/9628
// Let's flip form statics to instance, with exposed interface (so that we can easily swap implementations)
internal static class ConfigurationProvider
{
    private static IEditorConfigParser s_editorConfigParser = new EditorConfigParser();
    // TODO: This module should have a mechanism for removing unneeded configurations
    //  (disabled rules and analyzers that need to run in different node)
    private static readonly Dictionary<string, BuildAnalyzerConfiguration> _editorConfig = new Dictionary<string, BuildAnalyzerConfiguration>();

    /// <summary>
    /// Gets the user specified unrecognized configuration for the given analyzer rule.
    /// 
    /// The configuration module should as well check that CustomConfigurationData
    ///  for a particular rule is equal across the whole build (for all projects)  - otherwise it should error out.
    /// This should apply to all rules for which the configuration is fetched.
    /// </summary>
    /// <param name="projectFullPath"></param>
    /// <param name="ruleId"></param>
    /// <returns></returns>
    public static CustomConfigurationData GetCustomConfiguration(string projectFullPath, string ruleId)
    {
        return CustomConfigurationData.Null;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="projectFullPath"></param>
    /// <param name="ruleId"></param>
    /// <throws><see cref="BuildCopConfigurationException"/> If CustomConfigurationData differs in a build for a same ruleId</throws>
    /// <returns></returns>
    public static void CheckCustomConfigurationDataValidity(string projectFullPath, string ruleId)
    {
        // TBD
    }

    public static BuildAnalyzerConfigurationInternal[] GetMergedConfigurations(
        string projectFullPath,
        BuildAnalyzer analyzer)
        => FillConfiguration(projectFullPath, analyzer.SupportedRules, GetMergedConfiguration);

    public static BuildAnalyzerConfiguration[] GetUserConfigurations(
        string projectFullPath,
        IReadOnlyList<string> ruleIds)
        => FillConfiguration(projectFullPath, ruleIds, GetUserConfiguration);

    public static CustomConfigurationData[] GetCustomConfigurations(
        string projectFullPath,
        IReadOnlyList<string> ruleIds)
        => FillConfiguration(projectFullPath, ruleIds, GetCustomConfiguration);

    public static BuildAnalyzerConfigurationInternal[] GetMergedConfigurations(
        BuildAnalyzerConfiguration[] userConfigs,
        BuildAnalyzer analyzer)
    {
        var configurations = new BuildAnalyzerConfigurationInternal[userConfigs.Length];

        for (int idx = 0; idx < userConfigs.Length; idx++)
        {
            configurations[idx] = MergeConfiguration(
                analyzer.SupportedRules[idx].Id,
                analyzer.SupportedRules[idx].DefaultConfiguration,
                userConfigs[idx]);
        }

        return configurations;
    }

    private static TConfig[] FillConfiguration<TConfig, TRule>(string projectFullPath, IReadOnlyList<TRule> ruleIds, Func<string, TRule, TConfig> configurationProvider)
    {
        TConfig[] configurations = new TConfig[ruleIds.Count];
        for (int i = 0; i < ruleIds.Count; i++)
        {
            configurations[i] = configurationProvider(projectFullPath, ruleIds[i]);
        }

        return configurations;
    }

    /// <summary>
    /// Gets effective user specified (or default) configuration for the given analyzer rule.
    /// The configuration values CAN be null upon this operation.
    /// 
    /// The configuration module should as well check that BuildAnalyzerConfigurationInternal.EvaluationAnalysisScope
    ///  for all rules is equal - otherwise it should error out.
    /// </summary>
    /// <param name="projectFullPath"></param>
    /// <param name="ruleId"></param>
    /// <returns></returns>
    public static BuildAnalyzerConfiguration GetUserConfiguration(string projectFullPath, string ruleId)
    {
        if (!_editorConfig.TryGetValue(ruleId, out BuildAnalyzerConfiguration? editorConfig))
        {
            editorConfig = BuildAnalyzerConfiguration.Null;
        }

        var config = s_editorConfigParser.Parse(projectFullPath);
        var keyTosearch = $"msbuild_analyzer.{ruleId}.";
        var dictionaryConfig = new Dictionary<string, string>();

        foreach (var kv in config)
        {
            if (kv.Key.StartsWith(keyTosearch, StringComparison.OrdinalIgnoreCase))
            {
                dictionaryConfig[kv.Key.Replace(keyTosearch.ToLower(), "")] = kv.Value;
            }
        }

        if (dictionaryConfig.Any())
        {
            return BuildAnalyzerConfiguration.Create(dictionaryConfig);
        }

        return editorConfig;
    }

    /// <summary>
    /// Gets effective configuration for the given analyzer rule.
    /// The configuration values are guaranteed to be non-null upon this merge operation.
    /// </summary>
    /// <param name="projectFullPath"></param>
    /// <param name="analyzerRule"></param>
    /// <returns></returns>
    public static BuildAnalyzerConfigurationInternal GetMergedConfiguration(string projectFullPath, BuildAnalyzerRule analyzerRule)
        => GetMergedConfiguration(projectFullPath, analyzerRule.Id, analyzerRule.DefaultConfiguration);

    public static BuildAnalyzerConfigurationInternal MergeConfiguration(
        string ruleId,
        BuildAnalyzerConfiguration defaultConfig,
        BuildAnalyzerConfiguration editorConfig)
        => new BuildAnalyzerConfigurationInternal(
            ruleId: ruleId,
            evaluationAnalysisScope: GetConfigValue(editorConfig, defaultConfig, cfg => cfg.EvaluationAnalysisScope),
            isEnabled: GetConfigValue(editorConfig, defaultConfig, cfg => cfg.IsEnabled),
            severity: GetConfigValue(editorConfig, defaultConfig, cfg => cfg.Severity));

    private static BuildAnalyzerConfigurationInternal GetMergedConfiguration(
        string projectFullPath,
        string ruleId,
        BuildAnalyzerConfiguration defaultConfig)
        => MergeConfiguration(ruleId, defaultConfig, GetUserConfiguration(projectFullPath, ruleId));

    private static T GetConfigValue<T>(
        BuildAnalyzerConfiguration editorConfigValue,
        BuildAnalyzerConfiguration defaultValue,
        Func<BuildAnalyzerConfiguration, T?> propertyGetter) where T : struct
        => propertyGetter(editorConfigValue) ??
           propertyGetter(defaultValue) ??
           EnsureNonNull(propertyGetter(BuildAnalyzerConfiguration.Default));

    private static T EnsureNonNull<T>(T? value) where T : struct
    {
        if (value is null)
        {
            throw new InvalidOperationException("Value is null");
        }

        return value.Value;
    }
}
