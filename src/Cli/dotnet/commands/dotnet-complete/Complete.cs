// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.CommandLine;
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

        private static CompletionItem ToCompletionItem (string s) => new CompletionItem(s);

        public static CompletionDelegate TargetFrameworksFromProjectFile =>
            (_context) =>
            {
                try
                {
                    return GetMSBuildProject()?.GetTargetFrameworks().Select(tf => tf.GetShortFolderName()).Select(ToCompletionItem) ?? Empty<CompletionItem>();
                }
                catch (Exception)
                {
                    return Empty<CompletionItem>();
                }
            };

        private static void Report(Exception e) =>
            Reporter.Verbose.WriteLine($"Exception occurred while getting completions: {e}");

        public static CompletionDelegate RunTimesFromProjectFile =>
            (_context) =>
            {
                try
                {
                    return GetMSBuildProject()?.GetRuntimeIdentifiers().Select(ToCompletionItem) ?? Empty<CompletionItem>();
                }
                catch (Exception)
                {
                    return Empty<CompletionItem>();
                }
            };

        public static CompletionDelegate ProjectReferencesFromProjectFile =>
            (_context) =>
            {
                try
                {
                    return GetMSBuildProject()?.GetProjectToProjectReferences().Select(r => ToCompletionItem(r.Include)) ?? Empty<CompletionItem>();
                }
                catch (Exception)
                {
                    return Empty<CompletionItem>();
                }
            };

        public static CompletionDelegate ConfigurationsFromProjectFileOrDefaults => 
            (_context) =>
            {
                try
                {
                    return (GetMSBuildProject()?.GetConfigurations() ?? new[] { "Debug", "Release" }).Select(ToCompletionItem);
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
