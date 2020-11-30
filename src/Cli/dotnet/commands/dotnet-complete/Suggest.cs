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
            try
            {
                return GetMSBuildProject()?.GetTargetFrameworks().Select(tf => tf.GetShortFolderName()) ?? Empty<string>();
            }
            catch (Exception)
            {
                return Empty<string>();
            }
        }

        private static void Report(Exception e) =>
            Reporter.Verbose.WriteLine($"Exception occurred while getting suggestions: {e}");

        public static IEnumerable<string> RunTimesFromProjectFile()
        {
            try
            {
                return GetMSBuildProject()?.GetRuntimeIdentifiers() ?? Empty<string>();
            }
            catch (Exception)
            {
                return Empty<string>();
            }
        }
            

        public static IEnumerable<string> ProjectReferencesFromProjectFile()
        {
            try
            {
                return GetMSBuildProject()?.GetProjectToProjectReferences().Select(r => r.Include) ?? Empty<string>();
            }
            catch (Exception)
            {
                return Empty<string>();
            }
        }

        public static IEnumerable<string> ConfigurationsFromProjectFileOrDefaults()
        {
            try
            {
                return GetMSBuildProject()?.GetConfigurations() ?? new[] { "Debug", "Release" };
            }
            catch (Exception)
            {
                return Empty<string>();
            }
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
