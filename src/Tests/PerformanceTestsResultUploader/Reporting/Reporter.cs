// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using PerformanceTestsResultUploader;
using RuntimeEnvironment = Microsoft.DotNet.PlatformAbstractions.RuntimeEnvironment;

namespace Reporting
{
    public class Reporter
    {
        private Run run;
        private Os os;
        private Build build;
        private List<Test> tests = new List<Test>();
        protected IEnvironment environment;

        private Reporter() { }

        /// <summary>
        ///     Get a Reporter. Relies on environment variables.
        /// </summary>
        /// <param name="environment">Optional environment variable provider</param>
        /// <returns>A Reporter instance or null if the environment is incorrect.</returns>
        public static Reporter CreateReporter(DirectoryInfo repositoryRoot, IEnvironment environment = null)
        {
            Reporter ret = new Reporter {environment = environment ?? new EnvironmentProvider()};

            ret.Init(repositoryRoot);
            return ret;
        }

        private void Init(DirectoryInfo repositoryRoot)
        {
            run = new Run
            {
                CorrelationId = environment.GetEnvironmentVariable("HELIX_CORRELATION_ID"),
                PerfRepoHash = "place holder", // sdk does not use perf repo
                Name = environment.GetEnvironmentVariable("TestRunName"), // no use for now.
                Queue = environment.GetEnvironmentVariable("HelixTargetQueues")
            };
            run.Hidden = false;
            run.Configurations.Add("Configuration", environment.GetEnvironmentVariable("configuration"));
            run.Configurations.Add("TestFullMSBuild",
                environment.GetEnvironmentVariableAsBool("TestFullMSBuild", false).ToString());

            os = new Os
            {
                Name = $"{RuntimeEnvironment.OperatingSystem} {RuntimeEnvironment.OperatingSystemVersion}",
                // ToLower to match the existing record casing
                Architecture = RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant(),
                Locale = CultureInfo.CurrentUICulture.ToString()
            };

            string gitHash = environment.GetEnvironmentVariable("GIT_COMMIT");
            build = new Build
            {
                Repo = "dotnet/sdk",
                Branch = environment.GetEnvironmentVariable("GIT_BRANCH"),
                Architecture = environment.GetEnvironmentVariable("architecture"),
                Locale = "en-us",
                GitHash = gitHash,
                BuildName = environment.GetEnvironmentVariable("BuildNumber"),
                TimeStamp = GetCommitTimestamp(gitHash, repositoryRoot.FullName)
            };

            tests = XunitPerformanceResultConverter.BatchGenerateTests(
                new DirectoryInfo(environment.GetEnvironmentVariable("perfWorkingDirectory")));
        }

        public static DateTime GetCommitTimestamp(string gitHash, string directoryUnderGit)
        {
            ProcessStartInfo gitInfo =
                new ProcessStartInfo
                {
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    FileName = "git",
                    Arguments = $"show -s --format=%cI {gitHash}",
                    WorkingDirectory = directoryUnderGit
                };

            using (Process process = Process.Start(gitInfo))
            {
                string stderrStr = process.StandardError.ReadToEnd();
                string stdoutStr = process.StandardOutput.ReadToEnd();

                process.Start();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    throw new PerformanceTestsResultUploaderException(
                        $"Cannot get commit time stamp from git exitcode {process.ExitCode}, " +
                        $"StandardOutput {stdoutStr}, " +
                        $"StandardError {stderrStr}.");
                }

                return DateTime.Parse(stdoutStr.Trim());
            }
        }

        public string GetJson()
        {
            var jsonObject = new {build, os, run, tests};
            JsonSerializerSettings settings = new JsonSerializerSettings();
            DefaultContractResolver resolver = new DefaultContractResolver();
            resolver.NamingStrategy = new CamelCaseNamingStrategy {ProcessDictionaryKeys = false};
            settings.ContractResolver = resolver;
            return JsonConvert.SerializeObject(jsonObject, Formatting.Indented, settings);
        }
    }
}
