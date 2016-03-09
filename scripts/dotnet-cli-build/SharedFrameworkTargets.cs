using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli.Build.Framework;
using Microsoft.Extensions.PlatformAbstractions;

using static Microsoft.DotNet.Cli.Build.Framework.BuildHelpers;

namespace Microsoft.DotNet.Cli.Build
{
    public class SharedFrameworkTargets
    {
        public const string SharedFrameworkName = "NETStandard.Library";

        private const string CoreHostBaseName = "corehost";

        [Target]
        public static BuildTargetResult PublishSharedFramework(BuildTargetContext c)
        {
            string SharedFrameworkPublishRoot = Path.Combine(Dirs.Output, "obj", "sharedframework");
            string SharedFrameworkSourceRoot = Path.Combine(Dirs.RepoRoot, "src", "sharedframework", "framework");
            string SharedFrameworkNugetVersion = GetVersionFromProjectJson(Path.Combine(Path.Combine(Dirs.RepoRoot, "src", "sharedframework", "framework"), "project.json"));

            if (Directory.Exists(SharedFrameworkPublishRoot))
            {
                Directory.Delete(SharedFrameworkPublishRoot, true);
            }

            // We publish to a sub folder of the PublishRoot so tools like heat and zip can generate folder structures easier.
            string SharedFrameworkNameAndVersionRoot = Path.Combine(SharedFrameworkPublishRoot, "shared", SharedFrameworkName, SharedFrameworkNugetVersion);

            DotNetCli.Stage0.Publish("--output", SharedFrameworkNameAndVersionRoot, SharedFrameworkSourceRoot).Execute().EnsureSuccessful();

            c.BuildContext["SharedFrameworkPublishRoot"] = SharedFrameworkPublishRoot;
            c.BuildContext["SharedFrameworkNugetVersion"] = SharedFrameworkNugetVersion;

            // Clean up artifacts that dotnet-publish generates which we don't need
            File.Delete(Path.Combine(SharedFrameworkNameAndVersionRoot, $"framework{Constants.ExeSuffix}"));
            File.Delete(Path.Combine(SharedFrameworkNameAndVersionRoot, "framework.dll"));
            File.Delete(Path.Combine(SharedFrameworkNameAndVersionRoot, "framework.pdb"));

            // Rename the .deps file
            File.Move(Path.Combine(SharedFrameworkNameAndVersionRoot, "framework.deps"), Path.Combine(SharedFrameworkNameAndVersionRoot, $"{SharedFrameworkName}.deps"));

            // corehost will be renamed to dotnet at some point and then this can be removed.
            File.Move(Path.Combine(SharedFrameworkNameAndVersionRoot, $"{CoreHostBaseName}{Constants.ExeSuffix}"), Path.Combine(SharedFrameworkNameAndVersionRoot, $"dotnet{Constants.ExeSuffix}"));

            // hostpolicy will be renamed to dotnet at some point and then this can be removed.
            File.Move(Path.Combine(SharedFrameworkNameAndVersionRoot, $"{Constants.DynamicLibPrefix}hostpolicy{Constants.DynamicLibSuffix}"), Path.Combine(SharedFrameworkNameAndVersionRoot, $"{Constants.DynamicLibPrefix}dotnethostimpl{Constants.DynamicLibSuffix}"));

            return c.Success();
        }

        [Target]
        public static BuildTargetResult PublishSharedHost(BuildTargetContext c)
        {
            string SharedHostPublishRoot = Path.Combine(Dirs.Output, "obj", "sharedhost");

            if (Directory.Exists(SharedHostPublishRoot))
            {
                Directory.Delete(SharedHostPublishRoot, true);
            }

            DotNetCli.Stage0.Publish("--output", SharedHostPublishRoot, Path.Combine(Dirs.RepoRoot, "src", "sharedframework", "host")).Execute().EnsureSuccessful();

            // For the shared host, we only want corerun and not any of the other artifacts in the package (like the hostpolicy)
            foreach (var filePath in Directory.GetFiles(SharedHostPublishRoot))
            {
                if (Path.GetFileName(filePath) != $"{CoreHostBaseName}{Constants.ExeSuffix}")
                {
                    File.Delete(filePath);
                }
            }

            // corehost will be renamed to dotnet at some point and then this can be removed.
            File.Move(Path.Combine(SharedHostPublishRoot, $"{CoreHostBaseName}{Constants.ExeSuffix}"), Path.Combine(SharedHostPublishRoot, $"dotnet{Constants.ExeSuffix}"));

            c.BuildContext["SharedHostPublishRoot"] = SharedHostPublishRoot;

            return c.Success();
        }

        private static string GetVersionFromProjectJson(string pathToProjectJson)
        {
            Regex r = new Regex($"\"{Regex.Escape(SharedFrameworkName)}\"\\s*:\\s*\"(?'version'[^\"]*)\"");

            foreach(var line in File.ReadAllLines(pathToProjectJson))
            {
                var m = r.Match(line);

                if (m.Success)
                {
                    return m.Groups["version"].Value;
                }
            }

            return null;
        }
    }
}