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
using Microsoft.Build.Experimental.BuildCheck;
using System.Configuration;
using Microsoft.Build.BuildCheck.Infrastructure.EditorConfig;

namespace Microsoft.Build.BuildCheck.Infrastructure;

// TODO: https://github.com/dotnet/msbuild/issues/9628
internal sealed class ConfigurationProvider
{
    private readonly EditorConfigParser s_editorConfigParser = new EditorConfigParser();

    // TODO: This module should have a mechanism for removing unneeded configurations
    //  (disabled rules and analyzers that need to run in different node)
    private readonly Dictionary<string, BuildAnalyzerConfiguration> _editorConfig = new Dictionary<string, BuildAnalyzerConfiguration>();

    private readonly Dictionary<string, CustomConfigurationData> _customConfigurationData = new Dictionary<string, CustomConfigurationData>();

    private readonly string[] _infrastructureConfigurationKeys = new string[] {
        nameof(BuildAnalyzerConfiguration.EvaluationAnalysisScope).ToLower(),
        nameof(BuildAnalyzerConfiguration.IsEnabled).ToLower(),
        nameof(BuildAnalyzerConfiguration.Severity).ToLower()
    };

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
    public CustomConfigurationData GetCustomConfiguration(string projectFullPath, string ruleId)
    {
        var configuration = GetConfiguration(projectFullPath, ruleId);

        if (configuration is null || !configuration.Any())
    {
        return CustomConfigurationData.Null;
    }

        // remove the infrastructure owned key names
        foreach (var infraConfigurationKey in _infrastructureConfigurationKeys)
        {
            configuration.Remove(infraConfigurationKey);
        }

        var data = new CustomConfigurationData(ruleId, configuration);

        if (!_customConfigurationData.ContainsKey(ruleId))
        {
            _customConfigurationData[ruleId] = data;
        }

        return data;
    }

    /// <summary>
    /// Verifies if previously fetched custom configurations are equal to current one. 
    /// </summary>
    /// <param name="projectFullPath"></param>
    /// <param name="ruleId"></param>
    /// <throws><see cref="BuildCheckConfigurationException"/> If CustomConfigurationData differs in a build for a same ruleId</throws>
    /// <returns></returns>
    internal void CheckCustomConfigurationDataValidity(string projectFullPath, string ruleId)
    {
        var configuration = GetCustomConfiguration(projectFullPath, ruleId);

        if (_customConfigurationData.TryGetValue(ruleId, out var storedConfiguration))
        {
            if (!storedConfiguration.Equals(configuration))
            {
                throw new BuildCheckConfigurationException("Custom configuration should be equal between projects");
    }
        }
    }

    internal BuildAnalyzerConfigurationInternal[] GetMergedConfigurations(
        string projectFullPath,
        BuildAnalyzer analyzer)
        => FillConfiguration(projectFullPath, analyzer.SupportedRules, GetMergedConfiguration);

    internal BuildAnalyzerConfiguration[] GetUserConfigurations(
        string projectFullPath,
        IReadOnlyList<string> ruleIds)
        => FillConfiguration(projectFullPath, ruleIds, GetUserConfiguration);

    public  CustomConfigurationData[] GetCustomConfigurations(
        string projectFullPath,
        IReadOnlyList<string> ruleIds)
        => FillConfiguration(projectFullPath, ruleIds, GetCustomConfiguration);

    internal BuildAnalyzerConfigurationInternal[] GetMergedConfigurations(
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

    private TConfig[] FillConfiguration<TConfig, TRule>(string projectFullPath, IReadOnlyList<TRule> ruleIds, Func<string, TRule, TConfig> configurationProvider)
    {
        TConfig[] configurations = new TConfig[ruleIds.Count];
        for (int i = 0; i < ruleIds.Count; i++)
        {
            configurations[i] = configurationProvider(projectFullPath, ruleIds[i]);
        }

        return configurations;
    }

    internal Dictionary<string, string> GetConfiguration(string projectFullPath, string ruleId)
    {
        Dictionary<string, string> config;
        try
        {
            config = s_editorConfigParser.Parse(projectFullPath);
        }
        catch (Exception exception)
        {
            throw new BuildCheckConfigurationException($"Parsing editorConfig data failed", exception, BuildCheckConfigurationErrorScope.EditorConfigParser);
        }

        var keyToSearch = $"build_check.{ruleId}.";
        var dictionaryConfig = new Dictionary<string, string>();

        foreach (var kv in config)
        {
            if (kv.Key.StartsWith(keyToSearch, StringComparison.OrdinalIgnoreCase))
            {
                var newKey = kv.Key.Substring(keyToSearch.Length);
                dictionaryConfig[newKey] = kv.Value;
            }
        }

        return dictionaryConfig;
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
    internal BuildAnalyzerConfiguration GetUserConfiguration(string projectFullPath, string ruleId)
    {
        var cacheKey = $"{ruleId}-{projectFullPath}";

        if (!_editorConfig.TryGetValue(cacheKey, out BuildAnalyzerConfiguration? editorConfig))
        {
            editorConfig = BuildAnalyzerConfiguration.Null;
        }

        var config = GetConfiguration(projectFullPath, ruleId);

        if (config.Any())
        {
            editorConfig = BuildAnalyzerConfiguration.Create(config);
        }

        _editorConfig[cacheKey] = editorConfig;

        return editorConfig;
    }

    /// <summary>
    /// Gets effective configuration for the given analyzer rule.
    /// The configuration values are guaranteed to be non-null upon this merge operation.
    /// </summary>
    /// <param name="projectFullPath"></param>
    /// <param name="analyzerRule"></param>
    /// <returns></returns>
    internal BuildAnalyzerConfigurationInternal GetMergedConfiguration(string projectFullPath, BuildAnalyzerRule analyzerRule)
        => GetMergedConfiguration(projectFullPath, analyzerRule.Id, analyzerRule.DefaultConfiguration);

    internal BuildAnalyzerConfigurationInternal MergeConfiguration(
        string ruleId,
        BuildAnalyzerConfiguration defaultConfig,
        BuildAnalyzerConfiguration editorConfig)
        => new BuildAnalyzerConfigurationInternal(
            ruleId: ruleId,
            evaluationAnalysisScope: GetConfigValue(editorConfig, defaultConfig, cfg => cfg.EvaluationAnalysisScope),
            isEnabled: GetConfigValue(editorConfig, defaultConfig, cfg => cfg.IsEnabled),
            severity: GetConfigValue(editorConfig, defaultConfig, cfg => cfg.Severity));

    private BuildAnalyzerConfigurationInternal GetMergedConfiguration(
        string projectFullPath,
        string ruleId,
        BuildAnalyzerConfiguration defaultConfig)
        => MergeConfiguration(ruleId, defaultConfig, GetUserConfiguration(projectFullPath, ruleId));

    private T GetConfigValue<T>(
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
