// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.TestFramework;

namespace Microsoft.DotNet.Tools.Test.Utilities
{

    /// <summary>
    /// Base class for all unit test classes.
    /// </summary>
    public abstract class TestBase : IDisposable
    {
        protected const string DefaultFramework = "netcoreapp1.0";
        protected const string DefaultLibraryFramework = "netstandard1.5";
        protected const string ConsoleLoggerOutputNormal = "--logger console;verbosity=normal";
        private TempRoot _temp;
        private static TestAssets s_testAssets;

        static TestBase()
        {
            // set culture of test process to match CLI sub-processes when the UI language is overriden.
            string overriddenUILanguage = Environment.GetEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE");
            if (overriddenUILanguage != null)
            {
                CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo(overriddenUILanguage);
            }
        }

        protected static string RepoRoot
        {
            get
            {
                return RepoDirectoriesProvider.RepoRoot;
            }
        }

        protected static TestAssets TestAssets
        {
            get
            {
                if (s_testAssets == null)
                {
                    var assetsRoot = Path.Combine(RepoRoot, "TestAssets");

                    s_testAssets = new TestAssets(
                        new DirectoryInfo(assetsRoot),
                        new FileInfo(new Muxer().MuxerPath));
                }

                return s_testAssets;
            }
        }

        protected TestBase()
        {
        }

        public static string GetUniqueName()
        {
            return Guid.NewGuid().ToString("D");
        }

        public TempRoot Temp
        {
            get
            {
                if (_temp == null)
                {
                    _temp = new TempRoot();
                }

                return _temp;
            }
        }

        public virtual void Dispose()
        {
            if (_temp != null && !PreserveTemp())
            {
                _temp.Dispose();
            }
        }

        // Quick-n-dirty way to allow the temp output to be preserved when running tests
        private bool PreserveTemp()
        {
            var val = Environment.GetEnvironmentVariable("DOTNET_TEST_PRESERVE_TEMP");
            return !string.IsNullOrEmpty(val) && (
                string.Equals("true", val, StringComparison.OrdinalIgnoreCase) ||
                string.Equals("1", val, StringComparison.OrdinalIgnoreCase) ||
                string.Equals("on", val, StringComparison.OrdinalIgnoreCase));
        }

        protected CommandResult TestExecutable(string outputDir,
            string executableName,
            string expectedOutput)
        {
            var executablePath = Path.Combine(outputDir, executableName);
            var args = new List<string>();

            if (IsPortable(executablePath))
            {
                args.Add("exec");
                args.Add(ArgumentEscaper.EscapeSingleArg(executablePath));

                var muxer = new Muxer();
                executablePath = muxer.MuxerPath;
            }

            var executableCommand = new TestCommand(executablePath);

            var result = executableCommand.ExecuteWithCapturedOutput(string.Join(" ", args));

            if (!string.IsNullOrEmpty(expectedOutput))
            { 
                result.Should().HaveStdOut(expectedOutput);
            }
            result.Should().NotHaveStdErr();
            result.Should().Pass();
            return result;
        }

        protected void TestOutputExecutable(
            string outputDir,
            string executableName,
            string expectedOutput,
            bool native = false)
        {
            TestExecutable(GetCompilationOutputPath(outputDir, native), executableName, expectedOutput);
        }

        protected void TestNativeOutputExecutable(string outputDir, string executableName, string expectedOutput)
        {
            TestOutputExecutable(outputDir, executableName, expectedOutput, true);
        }

        protected string GetCompilationOutputPath(string outputDir, bool native)
        {
            var executablePath = outputDir;
            if (native)
            {
                executablePath = Path.Combine(executablePath, "native");
            }

            return executablePath;
        }

        private bool IsPortable(string executablePath)
        {
            var commandDir = Path.GetDirectoryName(executablePath);

            var runtimeConfigPath = Directory.EnumerateFiles(commandDir)
                .FirstOrDefault(x => x.EndsWith("runtimeconfig.json"));

            if (runtimeConfigPath == null)
            {
                return false;
            }

            var runtimeConfig = new RuntimeConfig(runtimeConfigPath);
            Console.WriteLine(runtimeConfig.Framework);
            return runtimeConfig.IsPortable;
        }
    }
}
