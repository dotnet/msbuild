// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.PhysicalFileSystem;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
using Microsoft.TemplateEngine.Cli;
using Microsoft.TemplateEngine.Cli.Commands;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.EndToEndTestHarness
{
    internal class Program
    {
        private const string HostIdentifier = "endtoendtestharness";
        private const string HostVersion = "v1.0.0";
        private const string CommandName = "test-test";
#pragma warning disable SA1308 // Variable names should not be prefixed
#pragma warning disable SA1311 // Static readonly fields should begin with upper-case letter
        private static readonly Dictionary<string, Func<IPhysicalFileSystem, JObject, string, bool>> s_verificationLookup = new(StringComparer.OrdinalIgnoreCase);
#pragma warning restore SA1311 // Static readonly fields should begin with upper-case letter
#pragma warning restore SA1308 // Variable names should not be prefixed

        private static int Main(string[] args)
        {
            s_verificationLookup["dir_exists"] = CheckDirectoryExists;
            s_verificationLookup["file_exists"] = CheckFileExists;
            s_verificationLookup["dir_does_not_exist"] = CheckDirectoryDoesNotExist;
            s_verificationLookup["file_does_not_exist"] = CheckFileDoesNotExist;
            s_verificationLookup["file_contains"] = CheckFileContains;
            s_verificationLookup["file_does_not_contain"] = CheckFileDoesNotContain;

            int batteryCount = int.Parse(args[0], CultureInfo.InvariantCulture);
            string outputPath = args[batteryCount + 1];

            List<string> passThroughArgs = new();
            passThroughArgs.AddRange(args.Skip(3 + batteryCount));
            passThroughArgs.Add("--debug:ephemeral-hive");

            string testAssetsRoot = args[batteryCount + 2];
            CliTemplateEngineHost host = null;

            Command newCommand = NewCommandFactory.Create(
       CommandName,
               (ParseResult parseResult) =>
               {
                   FileInfo outputPath = parseResult.GetValueForOption(SharedOptions.OutputOption);
                   host = CreateHost(testAssetsRoot, outputPath?.FullName);
                   return host;
               });

            int result = ParserFactory.CreateParser(newCommand).Parse(passThroughArgs.ToArray()).Invoke();

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
                Console.Error.WriteLine(fileName.AsSpan(outputPath.Length));
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

            Console.Error.WriteLine($"Expected {path} to not contain {config["text"]} but it did");
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
            if (fs.FileExists(path))
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
            foreach (JObject verification in verifications.Cast<JObject>())
            {
                string kind = verification["kind"].ToString();
                if (!s_verificationLookup.TryGetValue(kind, out Func<IPhysicalFileSystem, JObject, string, bool> func))
                {
                    Console.Error.WriteLine($"Unable to find a verification handler for {kind}");
                    return false;
                }

                success &= func(fs, verification, outputPath);
            }
            return success;
        }

        private static CliTemplateEngineHost CreateHost(string testAssetsRoot, string outputPath)
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

            var builtIns = new List<(Type, IIdentifiedComponent)>();
            builtIns.AddRange(Edge.Components.AllComponents);
            builtIns.AddRange(Orchestrator.RunnableProjects.Components.AllComponents);
            builtIns.AddRange(Cli.Components.AllComponents);
            builtIns.Add((typeof(ITemplatePackageProviderFactory), new BuiltInTemplatePackagesProviderFactory(testAssetsRoot)));

            var host = new CliTemplateEngineHost(
                HostIdentifier,
                HostVersion,
                preferences,
                builtIns,
                new[] { "dotnetcli" },
                outputPath);

            host.VirtualizeDirectory(outputPath);
            return host;
        }

        /// <summary>
        /// Gets dotnet CLI version.
        /// </summary>
        /// <remarks>
        /// do not move to TestHelper, unless absolutely needed - this project will be deprecated soon.
        /// </remarks>
        private static string GetCLIVersion()
        {
            ProcessStartInfo processInfo = new("dotnet", "--version")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };
            StringBuilder version = new();
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
