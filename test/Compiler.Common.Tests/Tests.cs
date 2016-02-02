// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

namespace Microsoft.DotNet.Cli.Compiler.Common
{
    public class Tests : TestBase
    {
        private static void EqualAfterDeserialize(IEnumerable<string> args, CommonCompilerOptions original)
        {
            CommonCompilerOptions newOptions = null;

            ArgumentSyntax.Parse(args, syntax =>
            {
                newOptions = CommonCompilerOptionsExtensions.Parse(syntax);
            });

            Assert.Equal(original, newOptions);

        }

        [Fact]
        public void SimpleSerialize()
        {
            var options = new CommonCompilerOptions();
            options.AdditionalArguments = new[] { "-highentropyva+" };

            var args = options.SerializeToArgs();
            Assert.Equal(new [] { "--additional-argument:-highentropyva+" }, args);

            EqualAfterDeserialize(args, options);
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

            EqualAfterDeserialize(args, options);
        }
    }
}
