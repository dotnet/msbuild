namespace Microsoft.DotNet.Tools.Test
{
    internal class LocalizableStrings
    {
        public const string AppFullName = ".NET Test Driver";

        public const string AppDescription = "Test Driver for the .NET Platform";

        public const string CmdArgProject = "PROJECT";

        public const string CmdArgDescription = "The project to test, defaults to the current directory.";

        public const string CmdSettingsFile = "SettingsFile";

        public const string CmdSettingsDescription = "Settings to use when running tests.";

        public const string CmdListTestsDescription = @"Lists discovered tests";

        public const string CmdTestCaseFilterExpression = "Expression";

        public const string CmdTestCaseFilterDescription = @"Run tests that match the given expression.
                                        Examples:
                                        Run tests with priority set to 1: --filter ""Priority = 1""
                                        Run a test with the specified full name: --filter ""FullyQualifiedName=Namespace.ClassName.MethodName""
                                        Run tests that contain the specified name: --filter ""FullyQualifiedName~Namespace.Class""
                                        More info on filtering support: https://aka.ms/vstest-filtering
                                        ";

        public const string CmdTestAdapterPathDescription = @"Use custom adapters from the given path in the test run.
                                        Example: --test-adapter-path:<pathToCustomAdapters>";

        public const string CmdLoggerOption = "LoggerUri/FriendlyName";

        public const string CmdLoggerDescription = @"Specify a logger for test results. 
                                        Example: --logger:trx";

        public const string CmdConfiguration = "configuration";

        public const string CmdConfigDescription = @"Configuration under which to build, i.e. Debug/Release";

        public const string CmdFramework = "FrameworkVersion";

        public const string CmdFrameworkDescription = @"Looks for test binaries for a specific framework";

        public const string CmdOutputDir = "OutputDir";

        public const string CmdOutputDescription = @"Directory in which to find the binaries to be run";

        public const string CmdPathToLogFile = "PathToLogFile";

        public const string CmdPathTologFileDescription = @"Enable verbose logs for test platform.
                                        Logs are written to the provided file.";

        public const string CmdNoBuildDescription = @"Do not build project before testing.";
    }
}
