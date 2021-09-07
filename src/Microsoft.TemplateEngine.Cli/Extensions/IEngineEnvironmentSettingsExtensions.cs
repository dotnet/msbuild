// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli.Extensions
{
    internal static class IEngineEnvironmentSettingsExtensions
    {
        internal static string? GetDefaultLanguage(this IEngineEnvironmentSettings settings)
        {
            if (!settings.Host.TryGetHostParamDefault("prefs:language", out string? defaultLanguage))
            {
                return null;
            }
            return defaultLanguage;
        }
    }
}
