using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli.Utils;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;
using NuGet.Versioning;

namespace dotnet_new3
{
    public static class NuGetUtil
    {
        private static CachingSourceProvider _cachingSourceProvider;
        private static bool _inited;
        private static readonly object _sync = new object();
        private static readonly List<SourceRepository> Repos = new List<SourceRepository>();

        public static void Init()
        {
            if (_inited)
            {
                return;
            }

            lock (_sync)
            {
                if (_inited)
                {
                    return;
                }

                string basepath = NuGetEnvironment.GetFolderPath(NuGetFolderPath.MachineWideConfigDirectory);
                IEnumerable<Settings> settingses = Settings.LoadMachineWideSettings(basepath);

                foreach (Settings settings in settingses)
                {
                    Init(settings);
                }

                settingses = Settings.LoadMachineWideSettings(Paths.AppDir);

                foreach (Settings settings in settingses)
                {
                    Init(settings);
                }

                settingses = Settings.LoadMachineWideSettings(Paths.UserDir);

                foreach (Settings settings in settingses)
                {
                    Init(settings);
                }

                _inited = true;
            }
        }

        private static void Init(ISettings settings)
        {
            var sourceObjects = new Dictionary<string, PackageSource>(StringComparer.Ordinal);
            var packageSourceProvider = new PackageSourceProvider(settings);
            var packageSourcesFromProvider = packageSourceProvider.LoadPackageSources();

            // Use PackageSource objects from the provider when possible (since those will have credentials from nuget.config)
            foreach (var source in packageSourcesFromProvider)
            {
                if (source.IsEnabled && !sourceObjects.ContainsKey(source.Source))
                {
                    sourceObjects[source.Source] = source;
                }
            }

            // Create a shared caching provider if one does not exist already
            _cachingSourceProvider = _cachingSourceProvider ?? new CachingSourceProvider(packageSourceProvider);

            List<SourceRepository> repos = sourceObjects.Select(entry => _cachingSourceProvider.CreateRepository(entry.Value)).ToList();
            Repos.AddRange(repos);
        }

        public static async Task<string> GetCurrentVersionOfPackageAsync(string packageId, string currentVersion)
        {
            try
            {
                NuGetVersion maxVersion = NuGetVersion.Parse(currentVersion);
                bool updated = false;

                foreach (SourceRepository repo in Repos)
                {
                    FindPackageByIdResource resource = await repo.GetResourceAsync<FindPackageByIdResource>();

                    if(resource == null)
                    {
                        continue;
                    }

                    try
                    {
                        IReadOnlyList<NuGetVersion> versions = (await resource.GetAllVersionsAsync(packageId, CancellationToken.None))?.ToList();

                        if (versions == null || versions.Count == 0)
                        {
                            continue;
                        }

                        NuGetVersion maxVer = versions.Max();
                        if (maxVer.CompareTo(maxVersion) > 0)
                        {
                            updated = true;
                            maxVersion = maxVer;
                        }
                    }
                    catch (FatalProtocolException)
                    {
                    }
                }

                return updated ? maxVersion.ToString() : null;
            }
            catch
            {
                return null;
            }
        }

        public static void InstallPackage(IReadOnlyList<string> packages, bool installingTemplates, bool global, bool quiet, Func<string, bool> tryAddSource)
        {
            JObject dependenciesObject = new JObject();
            JObject projJson = new JObject
            {
                {"version", "1.0.0-*"},
                {"dependencies", dependenciesObject },
                {
                    "frameworks", new JObject
                    {
                        {
                            "netcoreapp1.0", new JObject
                            {
                                { "imports", "dnxcore50" }
                            }
                        }
                    }
                }
            };

            foreach (string value in packages)
            {
                string pkg = value.Trim();
                if (pkg.IndexOfAny(Path.GetInvalidPathChars()) < 0 && pkg.Exists())
                {
                    tryAddSource(pkg);
                }
                else
                {
                    dependenciesObject[pkg] = "*";
                }
            }

            if (dependenciesObject.Count > 0)
            {
                dependenciesObject["Microsoft.NETCore.App"] = new JObject
                {
                    {"version", "1.0.0-rc2-3002702" },
                    {"type", "platform" }
                };

                Paths.ScratchDir.CreateDirectory();
                string componentsDir = global ? Paths.GlobalComponentsDir : Paths.ComponentsDir;
                string templateCacheDir = global ? Paths.GlobalTemplateCacheDir : Paths.TemplateCacheDir;
                componentsDir.CreateDirectory();
                templateCacheDir.CreateDirectory();
                string projectFile = Path.Combine(Paths.ScratchDir, "project.json");
                File.WriteAllText(projectFile, projJson.ToString());

                if (!quiet)
                {
                    Reporter.Output.WriteLine("Downloading...");
                }

                Command.CreateDotNet("restore", new[] { "--ignore-failed-sources" }, NuGetFramework.AnyFramework).WorkingDirectory(Paths.ScratchDir).OnErrorLine(x => Reporter.Error.WriteLine(x.Red().Bold())).Execute();

                if (!quiet)
                {
                    Reporter.Output.WriteLine("Installing...");
                }

                Command.CreateDotNet("publish", new string[0], NuGetFramework.AnyFramework).WorkingDirectory(Paths.ScratchDir).OnErrorLine(x => Reporter.Error.WriteLine(x.Red().Bold())).Execute();

                if (!quiet)
                {
                    Reporter.Output.WriteLine("Finishing up...");
                }

                string publishDir = Path.Combine(Paths.ScratchDir, @"bin/debug/netcoreapp1.0/publish");
                publishDir.Copy(componentsDir);

                if (!quiet)
                {
                    Reporter.Output.WriteLine("Done.");
                }

                if (installingTemplates)
                {
                    foreach (string value in packages)
                    {
                        MoveTemplateToTemplatesCache(value.Trim(), global);
                    }
                }

                Paths.ScratchDir.Delete();
            }
        }

        private static void MoveTemplateToTemplatesCache(string name, bool global)
        {
            string templateSource = Path.Combine(Paths.PackageCache, name);
            string cacheDir = global ? Paths.GlobalTemplateCacheDir : Paths.TemplateCacheDir;

            foreach (string dir in Directory.GetDirectories(templateSource, "*", SearchOption.TopDirectoryOnly))
            {
                foreach (string file in Directory.GetFiles(dir, "*.nupkg", SearchOption.TopDirectoryOnly))
                {
                    File.Copy(file, Path.Combine(cacheDir, Path.GetFileName(file)), true);
                }
            }

            Directory.Delete(templateSource, true);
        }
    }
}
