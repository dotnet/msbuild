using System;
using System.Collections.Generic;
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
        public static IEnumerable<string> TargetFrameworksFromProjectFile()
        {
            var msBuildProject = GetMSBuildProject();

            if (msBuildProject == null)
            {
                yield break;
            }

            foreach (var tfm in msBuildProject.GetTargetFrameworks())
            {
                yield return tfm.GetShortFolderName();
            }
        }

        private static void Report(Exception e) =>
            Reporter.Verbose.WriteLine($"Exception occurred while getting suggestions: {e}");

        public static IEnumerable<string> RunTimesFromProjectFile() =>
            GetMSBuildProject()
                .GetRuntimeIdentifiers() ??
            Empty<string>();

        public static IEnumerable<string> ProjectReferencesFromProjectFile() =>
            GetMSBuildProject()
                ?.GetProjectToProjectReferences()
                .Select(r => r.Include) ??
            Empty<string>();

        public static IEnumerable<string> ConfigurationsFromProjectFileOrDefaults() =>
            GetMSBuildProject()
                ?.GetConfigurations() ??
            new[] { "Debug", "Release" };

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
