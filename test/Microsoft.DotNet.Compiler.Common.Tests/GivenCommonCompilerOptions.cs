// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

namespace Microsoft.DotNet.Cli.Compiler.Common.Tests
{
    public class GivenCommonCompilerOptions : TestBase
    {
        [Fact]
        public void SimpleSerialize()
        {
            var options = new CommonCompilerOptions();
            options.AdditionalArguments = new[] { "-highentropyva+" };

            var args = options.SerializeToArgs();
            Assert.Equal(new [] { "--additional-argument:-highentropyva+" }, args);
        }

        [Fact]
        public void WithSpaces()
        {
            var options = new CommonCompilerOptions();
            options.AdditionalArguments = new[] { "-highentropyva+", "-addmodule:\"path with spaces\";\"after semicolon\"" };

            var args = options.SerializeToArgs();
            Assert.Equal(new [] {
                "--additional-argument:-highentropyva+",
                "--additional-argument:-addmodule:\"path with spaces\";\"after semicolon\""
                }, args);
        }
    }
}
