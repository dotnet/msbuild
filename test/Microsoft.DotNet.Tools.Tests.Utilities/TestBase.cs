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
            // set culture of test process to match CLI sub-processes when the UI language is overridden.
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
                        new FileInfo(DotnetUnderTest.FullName),
                        new RepoDirectoriesProvider().TestWorkingFolder); 
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
    }
}
