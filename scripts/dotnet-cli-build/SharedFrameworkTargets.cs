using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli.Build.Framework;
using Microsoft.Extensions.PlatformAbstractions;

using static Microsoft.DotNet.Cli.Build.Framework.BuildHelpers;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Cli.Build
{
    public class SharedFrameworkTargets
    {
        public const string SharedFrameworkName = "Microsoft.NETCore.App";

        private const string CoreHostBaseName = "corehost";
        private const string DotnetHostFxrBaseName = "hostfxr";
        private const string HostPolicyBaseName = "hostpolicy";

        [Target(nameof(PackageSharedFramework), nameof(CrossGenAllManagedAssemblies))]
        public static BuildTargetResult PublishSharedFramework(BuildTargetContext c)
        {
            return c.Success();
        }

        [Target]
        public static BuildTargetResult PackageSharedFramework(BuildTargetContext c)
        {
            string SharedFrameworkPublishRoot = Path.Combine(Dirs.Output, "obj", "sharedframework");
            string SharedFrameworkSourceRoot = Path.Combine(Dirs.RepoRoot, "src", "sharedframework", "framework");
            string SharedFrameworkNugetVersion = GetVersionFromProjectJson(Path.Combine(Path.Combine(Dirs.RepoRoot, "src", "sharedframework", "framework"), "project.json"));

            if (Directory.Exists(SharedFrameworkPublishRoot))
            {
                Utils.DeleteDirectory(SharedFrameworkPublishRoot);
            }

            // We publish to a sub folder of the PublishRoot so tools like heat and zip can generate folder structures easier.
            string SharedFrameworkNameAndVersionRoot = Path.Combine(SharedFrameworkPublishRoot, "shared", SharedFrameworkName, SharedFrameworkNugetVersion);

            string publishFramework = "dnxcore50"; // Temporary, use "netcoreapp" when we update nuget.
            string publishRuntime;
            if (PlatformServices.Default.Runtime.OperatingSystemPlatform == Platform.Windows)
            {
                publishRuntime = $"win7-{PlatformServices.Default.Runtime.RuntimeArchitecture}";
            }
            else
            {
                publishRuntime = PlatformServices.Default.Runtime.GetRuntimeIdentifier();
            }

            DotNetCli.Stage2.Publish(
                "--output", SharedFrameworkNameAndVersionRoot,
                "-r", publishRuntime,
                "-f", publishFramework,
                SharedFrameworkSourceRoot).Execute().EnsureSuccessful();

            c.BuildContext["SharedFrameworkPublishRoot"] = SharedFrameworkPublishRoot;
            c.BuildContext["SharedFrameworkNugetVersion"] = SharedFrameworkNugetVersion;

            // Clean up artifacts that dotnet-publish generates which we don't need
            File.Delete(Path.Combine(SharedFrameworkNameAndVersionRoot, $"framework{Constants.ExeSuffix}"));
            File.Delete(Path.Combine(SharedFrameworkNameAndVersionRoot, "framework.dll"));
            File.Delete(Path.Combine(SharedFrameworkNameAndVersionRoot, "framework.pdb"));
            File.Delete(Path.Combine(SharedFrameworkNameAndVersionRoot, "framework.runtimeconfig.json"));

            // Rename the .deps file
            var destinationDeps = Path.Combine(SharedFrameworkNameAndVersionRoot, $"{SharedFrameworkName}.deps.json");
            File.Move(Path.Combine(SharedFrameworkNameAndVersionRoot, "framework.deps"), Path.Combine(SharedFrameworkNameAndVersionRoot, $"{SharedFrameworkName}.deps"));
            File.Move(Path.Combine(SharedFrameworkNameAndVersionRoot, "framework.deps.json"), destinationDeps);

            // Merge in the RID fallback graph
            var fallbackFileName = PlatformServices.Default.Runtime.OperatingSystemPlatform.ToString().ToLowerInvariant() + ".json";
            var fallbackFile = Path.Combine(Dirs.RepoRoot, "src", "sharedframework", "rid-fallbacks", fallbackFileName);
            if (File.Exists(fallbackFile))
            {
                c.Info($"Merging in RID fallback graph: {fallbackFile}");
                var deps = JObject.Parse(File.ReadAllText(destinationDeps));
                var ridfallback = JObject.Parse(File.ReadAllText(fallbackFile));
                deps["runtimes"] = ridfallback["runtimes"];
                File.WriteAllText(destinationDeps, deps.ToString(Formatting.Indented));
            }
            else
            {
                c.Warn($"RID fallback graph file not found: {fallbackFile}");
            }

            // corehost will be renamed to dotnet at some point and then we will not need to rename it here.
            File.Copy(
                Path.Combine(Dirs.Corehost, $"{CoreHostBaseName}{Constants.ExeSuffix}"),
                Path.Combine(SharedFrameworkNameAndVersionRoot, $"dotnet{Constants.ExeSuffix}"));
            File.Copy(
                Path.Combine(Dirs.Corehost, $"{Constants.DynamicLibPrefix}{HostPolicyBaseName}{Constants.DynamicLibSuffix}"),
                Path.Combine(SharedFrameworkNameAndVersionRoot, $"{Constants.DynamicLibPrefix}{HostPolicyBaseName}{Constants.DynamicLibSuffix}"), true);

            if (File.Exists(Path.Combine(SharedFrameworkNameAndVersionRoot, "mscorlib.ni.dll")))
            {
                // Publish already places the crossgen'd version of mscorlib into the output, so we can
                // remove the IL version
                File.Delete(Path.Combine(SharedFrameworkNameAndVersionRoot, "mscorlib.dll"));
                c.BuildContext["SharedFrameworkNameAndVersionRoot"] = SharedFrameworkNameAndVersionRoot;
            }

            return c.Success();
        }

        [Target]
        public static BuildTargetResult PublishSharedHost(BuildTargetContext c)
        {
            string SharedHostPublishRoot = Path.Combine(Dirs.Output, "obj", "sharedhost");

            if (Directory.Exists(SharedHostPublishRoot))
            {
                Utils.DeleteDirectory(SharedHostPublishRoot);
            }
            Directory.CreateDirectory(SharedHostPublishRoot);

            // corehost will be renamed to dotnet at some point and then this can be removed.
            File.Copy(
                Path.Combine(Dirs.Corehost, $"{CoreHostBaseName}{Constants.ExeSuffix}"),
                Path.Combine(SharedHostPublishRoot, $"dotnet{Constants.ExeSuffix}"));
            File.Copy(
                Path.Combine(Dirs.Corehost, $"{Constants.DynamicLibPrefix}{DotnetHostFxrBaseName}{Constants.DynamicLibSuffix}"),
                Path.Combine(SharedHostPublishRoot, $"{Constants.DynamicLibPrefix}{DotnetHostFxrBaseName}{Constants.DynamicLibSuffix}"));

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

            throw new InvalidOperationException("Unable to match the version name from " + pathToProjectJson);
        }

        [Target]
        [Environment("CROSSGEN_SHAREDFRAMEWORK", "1", "true")]
        public static BuildTargetResult CrossGenAllManagedAssemblies(BuildTargetContext c)
        {
            string pathToAssemblies = c.BuildContext.Get<string>("SharedFrameworkNameAndVersionRoot");

            foreach (var file in Directory.GetFiles(pathToAssemblies))
            {
                string fileName = Path.GetFileName(file);

                if (fileName == "mscorlib.dll" || fileName == "mscorlib.ni.dll" || !HasMetadata(file))
                {
                    continue;
                }

                string tempPathName = Path.ChangeExtension(file, "readytorun");

                // This is not always correct. The version of crossgen we need to pick up is whatever one was restored as part
                // of the Microsoft.NETCore.Runtime.CoreCLR package that is part of the shared library. For now, the version hardcoded
                // in CompileTargets and the one in the shared library project.json match and are updated in lock step, but long term
                // we need to be able to look at the project.lock.json file and figure out what version of Microsoft.NETCore.Runtime.CoreCLR
                // was used, and then select that version.
                ExecSilent(Crossgen.GetCrossgenPathForVersion(CompileTargets.CoreCLRVersion),
                    "-readytorun", "-in", file, "-out", tempPathName, "-platform_assemblies_paths", pathToAssemblies);                  

                File.Delete(file);
                File.Move(tempPathName, file);
            }

            return c.Success();
        }

        private static bool HasMetadata(string pathToFile)
        {
            try
            {
                using (var inStream = File.OpenRead(pathToFile))
                {
                    using (var peReader = new PEReader(inStream))
                    {
                        return peReader.HasMetadata;
                    }
                }
            } catch (BadImageFormatException) { }

            return false;
        }
    }
}
