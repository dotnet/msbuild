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
using System.Threading.Tasks;
using Microsoft.Build.Experimental;

namespace Microsoft.Build.Analyzers.Infrastructure;

// TODO: https://github.com/dotnet/msbuild/issues/9628
internal static class ConfigurationProvider
{
    // TODO: This module should have a mechanism for removing unneeded configurations
    //  (disabled rules and analyzers that need to run in different node)
    private static readonly Dictionary<string, BuildAnalyzerConfiguration> _editorConfig = LoadConfiguration();

    // This is just a testing implementation for quicker unblock of testing.
    // Real implementation will use .editorconfig file.
    // Sample json:
    /////*lang=json,strict*/
    ////"""
    ////    {
    ////        "ABC123": {
    ////            "IsEnabled": true,
    ////            "Severity": "Info"
    ////        },
    ////        "COND0543": {
    ////            "IsEnabled": false,
    ////            "Severity": "Error",
    ////    		"EvaluationAnalysisScope": "AnalyzedProjectOnly",
    ////    		"CustomSwitch": "QWERTY"
    ////        },
    ////        "BLA": {
    ////            "IsEnabled": false
    ////        }
    ////    }
    ////    """
    //
    // Plus there will need to be a mechanism of distinguishing different configs in different folders
    //  - e.g. - what to do if we analyze two projects (not sharing output path) and they have different .editorconfig files?
    private static Dictionary<string, BuildAnalyzerConfiguration> LoadConfiguration()
    {
        const string configFileName = "editorconfig.json";
        string configPath = configFileName;

        if (!File.Exists(configPath))
        {
            // TODO: pass the current project path
            var dir = Environment.CurrentDirectory;
            configPath = Path.Combine(dir, configFileName);

            if (!File.Exists(configPath))
            {
                return new Dictionary<string, BuildAnalyzerConfiguration>();
            }
        }

        var json = File.ReadAllText(configPath);
        var DeserializationOptions = new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } };
        return JsonSerializer.Deserialize<Dictionary<string, BuildAnalyzerConfiguration>>(json, DeserializationOptions) ??
               new Dictionary<string, BuildAnalyzerConfiguration>();
    }

    /// <summary>
    /// Gets effective configuration for the given analyzer rule.
    /// The configuration values are guaranteed to be non-null upon this merge operation.
    /// </summary>
    /// <param name="analyzerRule"></param>
    /// <returns></returns>
    public static BuildAnalyzerConfigurationInternal GetMergedConfiguration(BuildAnalyzerRule analyzerRule)
    {
        if (!_editorConfig.TryGetValue(analyzerRule.Id, out BuildAnalyzerConfiguration? editorConfig))
        {
            editorConfig = BuildAnalyzerConfiguration.Null;
        }

        var defaultConfig = analyzerRule.DefaultConfiguration;

        return new BuildAnalyzerConfigurationInternal()
        {
            SupportedInvocationConcurrency = GetConfigValue(editorConfig, defaultConfig, cfg => cfg.SupportedInvocationConcurrency),
            EvaluationAnalysisScope = GetConfigValue(editorConfig, defaultConfig, cfg => cfg.EvaluationAnalysisScope),
            IsEnabled = GetConfigValue(editorConfig, defaultConfig, cfg => cfg.IsEnabled),
            LifeTimeScope = GetConfigValue(editorConfig, defaultConfig, cfg => cfg.LifeTimeScope),
            PerformanceWeightClass = GetConfigValue(editorConfig, defaultConfig, cfg => cfg.PerformanceWeightClass),
            Severity = GetConfigValue(editorConfig, defaultConfig, cfg => cfg.Severity)
        };

        T GetConfigValue<T>(
            BuildAnalyzerConfiguration editorConfigValue,
            BuildAnalyzerConfiguration defaultValue,
            Func<BuildAnalyzerConfiguration, T?> propertyGetter) where T : struct
            => propertyGetter(editorConfigValue) ??
               propertyGetter(defaultValue) ??
               EnsureNonNull(propertyGetter(BuildAnalyzerConfiguration.Default));

        T EnsureNonNull<T>(T? value) where T : struct
        {
            if (value is null)
            {
                throw new InvalidOperationException("Value is null");
            }

            return value.Value;
        }
    }
}
