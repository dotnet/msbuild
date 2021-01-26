// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.CommandLine.Parsing;
using System.CommandLine.Suggestions;
using System.IO;
using System.Linq;
using Microsoft.Build.Evaluation;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools;
using static System.Array;

namespace Microsoft.DotNet.Cli
{
    internal static class Suggest
    {
        public static SuggestDelegate TargetFrameworksFromProjectFile()
        {
            return (ParseResult parseResult, string textToMatch) =>
            {
                try
                {
                    return GetMSBuildProject()?.GetTargetFrameworks().Select(tf => tf.GetShortFolderName()) ?? Empty<string>();
                }
                catch (Exception)
                {
                    return Empty<string>();
                }
            };

        }

        private static void Report(Exception e) =>
            Reporter.Verbose.WriteLine($"Exception occurred while getting suggestions: {e}");

        public static SuggestDelegate RunTimesFromProjectFile()
        {
            return (ParseResult parseResult, string textToMatch) =>
            {
                try
                {
                    return GetMSBuildProject()?.GetRuntimeIdentifiers() ?? Empty<string>();
                }
                catch (Exception)
                {
                    return Empty<string>();
                }
            };
        }
            

        public static SuggestDelegate ProjectReferencesFromProjectFile()
        {
            return (ParseResult parseResult, string textToMatch) =>
            {
                try
                {
                    return GetMSBuildProject()?.GetProjectToProjectReferences().Select(r => r.Include) ?? Empty<string>();
                }
                catch (Exception)
                {
                    return Empty<string>();
                }
            };
        }

        public static SuggestDelegate ConfigurationsFromProjectFileOrDefaults()
        {
            return (ParseResult parseResult, string textToMatch) =>
            {
                try
                {
                    return GetMSBuildProject()?.GetConfigurations() ?? new[] { "Debug", "Release" };
                }
                catch (Exception)
                {
                    return Empty<string>();
                }
            };
        }

        private static MsbuildProject GetMSBuildProject()
        {
            try
            {
                return MsbuildProject.FromFileOrDirectory(
                    new ProjectCollection(),
                    Directory.GetCurrentDirectory(), interactive: false);
            }
            catch (Exception e)
            {
                Report(e);
                return null;
            }
        }
    }
}
