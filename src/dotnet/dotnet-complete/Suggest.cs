using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Evaluation;
using Microsoft.DotNet.Tools;

namespace Microsoft.DotNet.Cli
{
    internal static class Suggest
    {
        public static IEnumerable<string> TargetFrameworksFromProjectFile()
        {
            var msbuildProj = MsbuildProject.FromFileOrDirectory(
                new ProjectCollection(),
                Directory.GetCurrentDirectory());

            foreach (var tfm in msbuildProj.GetTargetFrameworks())
            {
                yield return tfm.GetShortFolderName();
            }
        }

        public static IEnumerable<string> RunTimesFromProjectFile()
        {
            var msbuildProj = MsbuildProject.FromFileOrDirectory(
                new ProjectCollection(),
                Directory.GetCurrentDirectory());

            return msbuildProj.GetRuntimeIdentifiers();
        }

        public static IEnumerable<string> ProjectReferencesFromProjectFile()
        {
            var msbuildProj = MsbuildProject.FromFileOrDirectory(
                new ProjectCollection(),
                Directory.GetCurrentDirectory());

            return msbuildProj.GetProjectToProjectReferences()
                              .Select(r => r.Include);
        }
    }
}