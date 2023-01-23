// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.



#nullable disable

using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.Debugging;
using Shouldly;
using Xunit;
using Xunit.Abstractions;
namespace Microsoft.Build.UnitTests
{
    public class DebugUtils_Tests
    {
        private readonly ITestOutputHelper _testOutput;

        public DebugUtils_Tests(ITestOutputHelper testOutput)
        {
            _testOutput = testOutput;
        }

        [Fact]
        public void DumpExceptionToFileShouldWriteInTempPathByDefault()
        {
            var exceptionFilesBefore = Directory.GetFiles(FileUtilities.TempFileDirectory, "MSBuild_*failure.txt");

            string[] exceptionFiles = null;

            try
            {
                ExceptionHandling.DumpExceptionToFile(new Exception("hello world"));
                exceptionFiles = Directory.GetFiles(FileUtilities.TempFileDirectory, "MSBuild_*failure.txt");
            }
            finally
            {
                _testOutput.WriteLine($"DebugUtils.DebugPath: {DebugUtils.DebugPath}");
                _testOutput.WriteLine($"Environment.GetEnvironmentVariable(\"MSBUILDDEBUGPATH\"): {Environment.GetEnvironmentVariable("MSBUILDDEBUGPATH")}");

                exceptionFilesBefore.ShouldNotBeNull();
                exceptionFiles.ShouldNotBeNull();
                (exceptionFiles.Length - exceptionFilesBefore.Length).ShouldBe(1);

                var exceptionFile = exceptionFiles.Except(exceptionFilesBefore).Single();
                File.ReadAllText(exceptionFile).ShouldContain("hello world");
                File.Delete(exceptionFile);
            }
        }
    }
}
