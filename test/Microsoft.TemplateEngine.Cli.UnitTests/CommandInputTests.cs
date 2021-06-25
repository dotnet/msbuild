// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using FakeItEasy;
using Microsoft.TemplateEngine.Cli.CommandParsing;
using Xunit;

namespace Microsoft.TemplateEngine.Cli.UnitTests
{
    public class CommandInputTests
    {
        [Fact]
        public void GetTemplateParametersFromCommand_BasicTest()
        {
            var commandInput = A.Fake<INewCommandInput>();
            A.CallTo(() => commandInput.RemainingParameters).Returns(new[] { "--param", "paramValue" });

            var parameters = TemplateCommandInput.GetTemplateParametersFromCommand(commandInput);
            Assert.Equal(1, parameters.Count);
            Assert.Equal("paramValue", parameters["--param"]);
        }

        [Fact]
        public void GetTemplateParametersFromCommand_Boolean()
        {
            var commandInput = A.Fake<INewCommandInput>();
            A.CallTo(() => commandInput.RemainingParameters).Returns(new[] { "--bool", "--param", "paramValue", "--bool2" });

            var parameters = TemplateCommandInput.GetTemplateParametersFromCommand(commandInput);
            Assert.Equal(3, parameters.Count);
            Assert.Equal("paramValue", parameters["--param"]);
            Assert.Null(parameters["--bool"]);
            Assert.Null(parameters["--bool2"]);
        }

        [Fact]
        public void GetTemplateParametersFromCommand_MultipleArgs()
        {
            var commandInput = A.Fake<INewCommandInput>();
            A.CallTo(() => commandInput.RemainingParameters).Returns(new[] { "--param", "paramValue1", "paramValue2" });

            var parameters = TemplateCommandInput.GetTemplateParametersFromCommand(commandInput);
            Assert.Equal(1, parameters.Count);
            Assert.Equal("paramValue1,paramValue2", parameters["--param"]);
        }

        [Fact]
        public void GetTemplateParametersFromCommand_SkipsFirstValueWithoutParamName()
        {
            var commandInput = A.Fake<INewCommandInput>();
            A.CallTo(() => commandInput.RemainingParameters).Returns(new[] { "paramValue2", "--param", "paramValue1", });

            var parameters = TemplateCommandInput.GetTemplateParametersFromCommand(commandInput);
            Assert.Equal(1, parameters.Count);
            Assert.Equal("paramValue1", parameters["--param"]);
        }
    }
}
