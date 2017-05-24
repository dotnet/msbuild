using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Config;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Macros;
using Microsoft.TemplateEngine.Utils;

[assembly:InternalsVisibleTo("dotnet-new3.UnitTests, PublicKey=0024000004800000940000000602000000240000525341310004000001000100f33a29044fa9d740c9b3213a93e57c84b472c84e0b8a0e1ae48e67a9f8f6de9d5f7f3d52ac23e48ac51801f1dc950abe901da34d2a9e3baadb141a17c77ef3c565dd5ee5054b91cf63bb3c6ab83f72ab3aafe93d0fc3c2348b764fafb0b1c0733de51459aeab46580384bf9d74c4e28164b7cde247f891ba07891c9d872ad2bb")]

namespace dotnet_new3
{
    public class Program
    {
        private const string HostIdentifier = "dotnetcli-preview";
        private const string HostVersion = "1.0.0";
        private const string CommandName = "new3";

        public static int Main(string[] args)
        {
            bool emitTimings = args.Any(x => string.Equals(x, "--debug:emit-timings", StringComparison.OrdinalIgnoreCase));
            return New3Command.Run(CommandName, CreateHost(emitTimings), new TelemetryLogger(null), FirstRun, args);
        }

        private static ITemplateEngineHost CreateHost(bool emitTimings)
        {
            var preferences = new Dictionary<string, string>
            {
                { "prefs:language", "C#" }
            };

            try
            {
                string versionString = Dotnet.Version().CaptureStdOut().Execute().StdOut;
                if (!string.IsNullOrWhiteSpace(versionString))
                {
                    preferences["dotnet-cli-version"] = versionString.Trim();
                }
            }
            catch
            { }

            var builtIns = new AssemblyComponentCatalog(new[]
            {
                typeof(RunnableProjectGenerator).GetTypeInfo().Assembly,
                typeof(ConditionalConfig).GetTypeInfo().Assembly
            });

            DefaultTemplateEngineHost host = new DefaultTemplateEngineHost(HostIdentifier, HostVersion, CultureInfo.CurrentCulture.Name, preferences, builtIns, new[] { "dotnetcli" });

            if (emitTimings)
            {
                host.OnLogTiming = (label, duration, depth) =>
                {
                    string indent = string.Join("", Enumerable.Repeat("  ", depth));
                    Console.WriteLine($"{indent} {label} {duration.TotalMilliseconds}");
                };
            }

            return host;
        }

        private static void FirstRun(IEngineEnvironmentSettings environmentSettings, IInstaller installer)
        {
            string baseDir = Environment.ExpandEnvironmentVariables("%DN3%");

            if (baseDir.Contains('%'))
            {
                Assembly a = typeof(Program).GetTypeInfo().Assembly;
                string path = new Uri(a.CodeBase, UriKind.Absolute).LocalPath;
                path = Path.GetDirectoryName(path);
                Environment.SetEnvironmentVariable("DN3", path);
            }

            string[] packageList;
            Paths paths = new Paths(environmentSettings);

            if (paths.FileExists(paths.Global.DefaultInstallPackageList))
            {
                packageList = paths.ReadAllText(paths.Global.DefaultInstallPackageList).Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                if (packageList.Length > 0)
                {
                    for (int i = 0; i < packageList.Length; ++i)
                    {
                        packageList[i] = packageList[i].Replace('\\', Path.DirectorySeparatorChar);
                    }

                    installer.InstallPackages(packageList);
                }
            }

            if (paths.FileExists(paths.Global.DefaultInstallTemplateList))
            {
                packageList = paths.ReadAllText(paths.Global.DefaultInstallTemplateList).Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                if (packageList.Length > 0)
                {
                    for (int i = 0; i < packageList.Length; ++i)
                    {
                        packageList[i] = packageList[i].Replace('\\', Path.DirectorySeparatorChar);
                    }

                    installer.InstallPackages(packageList);
                }
            }
        }
    }
}
