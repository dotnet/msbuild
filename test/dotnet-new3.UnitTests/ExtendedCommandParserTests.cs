using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.TemplateEngine.Cli;

namespace dotnet_new3.UnitTests
{
    public class ExtendedCommandParserTests
    {
        // This is based on the input args for Program.cs, but doesn't need to be. 
        // The tests in this class are isolated to testing ExtendedCommandParser. Any setup here is specific to the tests.
        // Do NOT expect the args to be the same as the real args.
        private static void SetupTestCommands(ExtendedCommandParser appExt)
        {
            // visible
            appExt.InternalOption("-l|--list", "--list", "List templates containing the specified name.", CommandOptionType.NoValue);
            appExt.InternalOption("-n|--name", "--name", "The name for the output being created. If no name is specified, the name of the current directory is used.", CommandOptionType.SingleValue);
            appExt.InternalOption("-h|--help", "--help", "Display help for the indicated template's parameters.", CommandOptionType.NoValue);

            // hidden
            appExt.HiddenInternalOption("-d|--dir", "--dir", CommandOptionType.NoValue);
            appExt.HiddenInternalOption("-a|--alias", "--alias", CommandOptionType.SingleValue);
            appExt.HiddenInternalOption("-x|--extra-args", "--extra-args", CommandOptionType.MultipleValue);
            appExt.HiddenInternalOption("--locale", "--locale", CommandOptionType.SingleValue);
            appExt.HiddenInternalOption("--quiet", "--quiet", CommandOptionType.NoValue);
            appExt.HiddenInternalOption("-i|--install", "--install", CommandOptionType.MultipleValue);

            // reserved but not currently used
            appExt.HiddenInternalOption("-up|--update", "--update", CommandOptionType.MultipleValue);
            appExt.HiddenInternalOption("-u|--uninstall", "--uninstall", CommandOptionType.MultipleValue);
            appExt.HiddenInternalOption("--skip-update-check", "--skip-update-check", CommandOptionType.NoValue);
        }

        private static ExtendedCommandParser OneArgStandardSetupAndParse(string inputCommand, out CommandArgument theArg)
        {
            string[] inputArgArray = inputCommand.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            ExtendedCommandParser app = new ExtendedCommandParser()
            {
                Name = "TestApp",
                FullName = "Testing ExtendedCommandParser"
            };
            SetupTestCommands(app);
            theArg = app.Argument("theArg", "arg description");

            app.OnExecute(() =>
            {
                app.ParseArgs();
                return Task.FromResult(0);
            });
            app.Execute(inputArgArray);

            return app;
        }

        [Fact]
        public void ExtraParameterParseTest()
        {
            string inputCommand = "blah --zaxxar";
            ExtendedCommandParser app = OneArgStandardSetupAndParse(inputCommand, out CommandArgument theArg);

            Assert.Equal(theArg.Value, "blah");
            Assert.True(app.RemainingParameters.ContainsKey("--zaxxar"), "extra parameter '--zaxxar' not properly parsed");
        }

        [Fact]
        public void ArgParseTest()
        {
            string inputArgs = "test --help --locale fr-FR";
            ExtendedCommandParser app = OneArgStandardSetupAndParse(inputArgs, out CommandArgument theArg);

            Assert.Equal(theArg.Value, "test");
            Assert.True(app.InternalParamHasValue("--help"));
            Assert.True(app.InternalParamHasValue("--locale"));
            Assert.Equal(app.InternalParamValue("--locale"), "fr-FR");
            Assert.False(app.RemainingParameters.Any(), string.Format("Unknown extra args: {0}", string.Join("|", app.RemainingParameters.Keys)));
        }
    }
}
