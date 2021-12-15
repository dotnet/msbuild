// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.CommandLine.Parsing;
using System.CommandLine.Completions;
using System.IO;
using System.Linq;
using Microsoft.Build.Evaluation;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools;
using static System.Array;

namespace Microsoft.DotNet.Cli
{
    internal static class Complete
    {
        public static CompletionDelegate TargetFrameworksFromProjectFile => 
            (_context) =>
            {
                try
                {
                    return GetMSBuildProject()?.GetTargetFrameworks().Select(tf => new CompletionItem(tf.GetShortFolderName())) ?? Empty<CompletionItem>();
                }
                catch (Exception)
                {
                    return Empty<CompletionItem>();
                }
            };

        private static void Report(Exception e) =>
            Reporter.Verbose.WriteLine($"Exception occurred while getting completions: {e}");

        public static CompletionDelegate RunTimesFromProjectFile =>
            (context) =>
            {
                try
                {
                    return GetMSBuildProject()?.GetRuntimeIdentifiers().Select((rid) => new CompletionItem(rid)) ?? Empty<CompletionItem>();
                }
                catch (Exception)
                {
                    return Empty<CompletionItem>();
                }
            };

        public static CompletionDelegate ProjectReferencesFromProjectFile => 
            (context) =>
            {
                try
                {
                    return GetMSBuildProject()?.GetProjectToProjectReferences().Select(r => new CompletionItem(r.Include)) ?? Empty<CompletionItem>();
                }
                catch (Exception)
                {
                    return Empty<CompletionItem>();
                }
            };

        public static CompletionDelegate ConfigurationsFromProjectFileOrDefaults =>
            (context) =>
            {
                try
                {
                    return (GetMSBuildProject()?.GetConfigurations() ?? new[] { "Debug", "Release" }).Select(configuration => new CompletionItem(configuration));
                }
                catch (Exception)
                {
                    return Empty<CompletionItem>();
                }
            };

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
