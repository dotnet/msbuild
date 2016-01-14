// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Test.Utilities
{
    
    /// <summary>
    /// Base class for all unit test classes.
    /// </summary>
    public abstract class TestBase : IDisposable
    {
        private TempRoot _temp;

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

        protected void TestOutputExecutable(string outputDir, string executableName, string expectedOutput)
        {
            var executablePath = Path.Combine(outputDir, executableName);

            var executableCommand = new TestCommand(executablePath);

            var result = executableCommand.ExecuteWithCapturedOutput("");

            result.Should().HaveStdOut(expectedOutput);
            result.Should().NotHaveStdErr();
            result.Should().Pass();
        }
    }
}
