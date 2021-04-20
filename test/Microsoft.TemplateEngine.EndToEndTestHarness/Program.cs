// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.PhysicalFileSystem;
using Microsoft.TemplateEngine.Cli;
using Microsoft.TemplateEngine.Cli.PostActionProcessors;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.EndToEndTestHarness
{
    class Program
    {
        private const string HostIdentifier = "endtoendtestharness";
        private const string HostVersion = "v1.0.0";
        private const string CommandName = "test-test";
        private static readonly Dictionary<string, Func<IPhysicalFileSystem, JObject, string, bool>> VerificationLookup = new Dictionary<string, Func<IPhysicalFileSystem, JObject, string, bool>>(StringComparer.OrdinalIgnoreCase);

        static int Main(string[] args)
        {
            VerificationLookup["dir_exists"] = CheckDirectoryExists;
            VerificationLookup["file_exists"] = CheckFileExists;
            VerificationLookup["dir_does_not_exist"] = CheckDirectoryDoesNotExist;
            VerificationLookup["file_does_not_exist"] = CheckFileDoesNotExist;
            VerificationLookup["file_contains"] = CheckFileContains;
            VerificationLookup["file_does_not_contain"] = CheckFileDoesNotContain;

            int batteryCount = int.Parse(args[0], CultureInfo.InvariantCulture);
            string[] passthroughArgs = new string[args.Length - 2 - batteryCount];
            string outputPath = args[batteryCount + 1];

            for(int i = 0; i < passthroughArgs.Length; ++i)
            {
                passthroughArgs[i] = args[i + 2 + batteryCount];
            }

            string home = "%USERPROFILE%";

            if (Path.DirectorySeparatorChar == '/')
            {
                home = "%HOME%";
            }

            ITemplateEngineHost host = CreateHost();
            string profileDir = Environment.ExpandEnvironmentVariables(home);

            if (string.IsNullOrWhiteSpace(profileDir))
            {
                Console.Error.WriteLine("Could not determine home directory");
                return 0;
            }

            string hivePath = Path.Combine(profileDir, ".tetestharness");
            host.VirtualizeDirectory(hivePath);
            host.VirtualizeDirectory(outputPath);

            int result = New3Command.Run(CommandName, host, new TelemetryLogger(null), new New3Callbacks(), passthroughArgs, hivePath);
            bool verificationsPassed = false;

            for (int i = 0; i < batteryCount; ++i)
            {
                string verificationsFile = args[i + 1];
                string verificationsFileContents = File.ReadAllText(verificationsFile);
                JArray verifications = JArray.Parse(verificationsFileContents);

                try
                {
                    verificationsPassed = RunVerifications(verifications, host.FileSystem, outputPath);
                }
                catch (Exception ex)
                {
                    verificationsPassed = false;
                    Console.Error.WriteLine(ex.ToString());
                }
            }

            Console.Error.WriteLine(" ");
            Console.Error.WriteLine("Output Files:");
            foreach (string fileName in host.FileSystem.EnumerateFiles(outputPath, "*", SearchOption.AllDirectories))
            {
                Console.Error.WriteLine(fileName.Substring(outputPath.Length));
            }

            return result != 0 ? result : batteryCount == 0 ? 0 : verificationsPassed ? 0 : 1;
        }

        private static bool CheckFileDoesNotContain(IPhysicalFileSystem fs, JObject config, string outputPath)
        {
            string path = Path.Combine(outputPath, config["path"].ToString());
            string text = fs.ReadAllText(path);
            if (!text.Contains(config["text"].ToString()))
            {
                return true;
            }

            Console.Error.WriteLine($"Expected {path} to not contain {config["text"].ToString()} but it did");
            return false;
        }

        private static bool CheckFileContains(IPhysicalFileSystem fs, JObject config, string outputPath)
        {
            string path = Path.Combine(outputPath, config["path"].ToString());
            string text = fs.ReadAllText(path);
            string expectedText = config["text"].ToString();
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                expectedText = expectedText.Replace("\r\n", "\n");
            }

            if (text.Contains(expectedText))
            {
                return true;
            }

            Console.Error.WriteLine($"Expected {path} to contain {expectedText} but it did not");
            Console.Error.WriteLine($"Actual content = {text}");

            return false;
        }

        private static bool CheckFileExists(IPhysicalFileSystem fs, JObject config, string outputPath)
        {
            string path = Path.Combine(outputPath, config["path"].ToString());
            if(fs.FileExists(path))
            {
                return true;
            }

            Console.Error.WriteLine($"Expected a file {path} to exist but it did not");
            return false;
        }

        private static bool CheckDirectoryExists(IPhysicalFileSystem fs, JObject config, string outputPath)
        {
            string path = Path.Combine(outputPath, config["path"].ToString());
            if (fs.DirectoryExists(path))
            {
                return true;
            }

            Console.Error.WriteLine($"Expected a directory {path} to exist but it did not");
            return false;
        }

        private static bool CheckFileDoesNotExist(IPhysicalFileSystem fs, JObject config, string outputPath)
        {
            string path = Path.Combine(outputPath, config["path"].ToString());
            if (!fs.FileExists(path))
            {
                return true;
            }

            Console.Error.WriteLine($"Expected a file {path} to not exist but it did");
            return false;
        }

        private static bool CheckDirectoryDoesNotExist(IPhysicalFileSystem fs, JObject config, string outputPath)
        {
            string path = Path.Combine(outputPath, config["path"].ToString());
            if (!fs.DirectoryExists(path))
            {
                return true;
            }

            Console.Error.WriteLine($"Expected a directory {path} to not exist but it did");
            return false;
        }

        private static bool RunVerifications(JArray verifications, IPhysicalFileSystem fs, string outputPath)
        {
            bool success = true;
            foreach(JObject verification in verifications)
            {
                string kind = verification["kind"].ToString();
                if (!VerificationLookup.TryGetValue(kind, out Func<IPhysicalFileSystem, JObject, string, bool> func))
                {
                    Console.Error.WriteLine($"Unable to find a verification handler for {kind}");
                    return false;
                }

                success &= func(fs, verification, outputPath);
            }
            return success;
        }

        private static ITemplateEngineHost CreateHost()
        {
            var preferences = new Dictionary<string, string>
            {
                { "prefs:language", "C#" }
            };

            try
            {
                string versionString = GetCLIVersion();
                if (!string.IsNullOrWhiteSpace(versionString))
                {
                    preferences["dotnet-cli-version"] = versionString.Trim();
                }
            }
            catch
            { }

            var builtIns = new AssemblyComponentCatalog(new[]
            {
                typeof(Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions.IMacro).GetTypeInfo().Assembly,            // for assembly: Microsoft.TemplateEngine.Orchestrator.RunnableProjects
                typeof(Microsoft.TemplateEngine.Edge.Paths).GetTypeInfo().Assembly,   // for assembly: Microsoft.TemplateEngine.Edge
                typeof(New3Command).GetTypeInfo().Assembly,    // for assembly: Microsoft.TemplateEngine.Cli
                typeof(Microsoft.TemplateSearch.Common.NuGetSearchCacheConfig).GetTypeInfo().Assembly,// for assembly: Microsoft.TemplateSearch.Common
                typeof(Program).GetTypeInfo().Assembly
            });

            return new DefaultTemplateEngineHost(HostIdentifier, HostVersion, preferences, builtIns, new[] { "dotnetcli" });
        }

        /// <summary>
        /// Gets dotnet CLI version.
        /// </summary>
        /// <remarks>
        /// do not move to TestHelper, unless absolutely needed - this project will be deprecated soon.
        /// </remarks>
        private static string GetCLIVersion()
        {
            ProcessStartInfo processInfo = new ProcessStartInfo("dotnet", "--version")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };
            StringBuilder version = new StringBuilder();
            Process p = Process.Start(processInfo);
            if (p != null)
            {
                p.BeginOutputReadLine();
                p.OutputDataReceived += (sender, e) => version.AppendLine(e.Data);
                p.WaitForExit();
            }
            return version.ToString();
        }
    }
}
