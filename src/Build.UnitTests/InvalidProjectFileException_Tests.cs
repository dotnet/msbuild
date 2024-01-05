// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Microsoft.Build.Exceptions;

using Xunit;
using Xunit.Abstractions;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    public class InvalidProjectFileExceptionTests
    {
        private readonly ITestOutputHelper _testOutput;

        public InvalidProjectFileExceptionTests(ITestOutputHelper output)
        {
            _testOutput = output;
        }

        /// <summary>
        /// Verify that nesting an IPFE copies the error code
        /// </summary>
        [Fact]
        public void ErrorCodeShouldAppearForCircularDependency()
        {
            string file = Path.GetTempPath() + Guid.NewGuid().ToString("N");

            try
            {
                File.WriteAllText(file, ObjectModelHelpers.CleanupFileContents(@"
                    <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                        <Target Name='Build'>
                            <CallTarget Targets='Build'/>
                        </Target>
                    </Project>
                "));

                MockLogger ml = new MockLogger(_testOutput);
                ObjectModelHelpers.BuildTempProjectFileExpectFailure(file, ml);

                // Make sure the log contains the error code and file/line/col for the circular dependency
                ml.AssertLogContains("MSB4006");
                ml.AssertLogContains("(4,29)");
                ml.AssertLogContains(file);
            }
            finally
            {
                File.Delete(file);
            }
        }

        /// <summary>
        /// Regression test for https://github.com/dotnet/msbuild/issues/1286
        /// </summary>
        [Fact]
        public void LogErrorShouldHavePathAndLocation()
        {
            string file = Path.GetTempPath() + Guid.NewGuid().ToString("N");

            try
            {
                File.WriteAllText(file, ObjectModelHelpers.CleanupFileContents(@"
                    <Project ToolsVersion='msbuilddefaulttoolsversion' xmlns='msbuildnamespace'>
                        <Target Name=[invalid] />
                    </Project>"));

                MockLogger logger = new MockLogger(_testOutput);
                ObjectModelHelpers.BuildTempProjectFileExpectFailure(file, logger);

                Assert.True(false, "Loading an invalid project should have thrown an InvalidProjectFileException.");
            }
            catch (InvalidProjectFileException e)
            {
                Assert.Equal(3, e.LineNumber);
                Assert.Equal(38, e.ColumnNumber);
                Assert.Equal(file, e.ProjectFile); // https://github.com/dotnet/msbuild/issues/1286
            }
            finally
            {
                File.Delete(file);
            }
        }
    }
}
