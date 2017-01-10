namespace Microsoft.DotNet.Cli.Utils
{
    internal class LocalizableStrings
    {
        public const string MalformedText = "Malformed command text '{0}'";

        public const string BuildOutputPathDoesNotExist = "outputpathresolver: {0} does not exist";

        public const string AttemptingToFindCommand = "{0}: attempting to find command {1} in {2}";

        public const string FailedToFindToolAssembly = "{0}: failed to find toolAssembly for {1}";

        public const string FailedToFindCommandPath = "{0}: failed to find commandPath {1}";

        public const string UnableToLocateDotnetMultiplexer = "Unable to locate dotnet multiplexer";

        public const string LookingForPreferCliRuntimeFile = "{0}: Looking for prefercliruntime file at `{1}`";

        public const string AttemptingToResolve = "{0}: attempting to resolve {1}";

        public const string DidNotFindAMatchingProject = "{0}: Did not find a matching project {1}.";

        public const string InvalidCommandResolverArguments = "{0}: invalid commandResolverArguments";

        public const string DoesNotExist = "{0}: {1} does not exist";

        public const string AmbiguousCommandName = "Ambiguous command name: {0}";

        public const string ToolLibraryFound = "{0}: tool library found {1}";

        public const string MSBuildExePath = "{0}: MSBUILD_EXE_PATH = {1}";

        public const string MSBuildProjectPath = "{0}: MSBuild project path = {1}";

        public const string MultipleProjectFilesFound = "Specify which project file to use because this '{0}' contains more than one project file.";

        public const string DidNotFindProject = "{0}: ProjectFactory did not find Project.";

        public const string ResolvingCommandSpec = "{0}: resolving commandspec from {1} Tool Libraries.";

        public const string FailedToResolveCommandSpec = "{0}: failed to resolve commandspec from library.";

        public const string AttemptingToResolveCommandSpec = "{0}: Attempting to resolve command spec from tool {1}";

        public const string NuGetPackagesRoot = "{0}: nuget packages root:\n{1}";

        public const string FoundToolLockFile = "{0}: found tool lockfile at : {1}";

        public const string LibraryNotFoundInLockFile = "{0}: library not found in lock file.";

        public const string AttemptingToCreateCommandSpec = "{0}: attempting to create commandspec";

        public const string CommandSpecIsNull = "{0}: commandSpec is null.";

        public const string ExpectDepsJsonAt = "{0}: expect deps.json at: {1}";

        public const string GeneratingDepsJson = "Generating deps.json at: {0}";

        public const string UnableToGenerateDepsJson = "unable to generate deps.json, it may have been already generated: {0}";

        public const string UnableToDeleteTemporaryDepsJson = "unable to delete temporary deps.json file: {0}";

        public const string VersionForPackageCouldNotBeResolved = "Version for package `{0}` could not be resolved.";

        public const string FileNotFound = "File not found `{0}`.";

        public const string ProjectNotRestoredOrRestoreFailed = "The project may not have been restored or restore failed - run `dotnet restore`";

        public const string NoExecutableFoundMatchingCommand = "No executable found matching command \"{0}\"";

        public const string WaitingForDebuggerToAttach = "Waiting for debugger to attach. Press ENTER to continue";

        public const string ProcessId = "Process ID: {0}";

        public const string CouldNotAccessAssetsFile = "Could not access assets file.";

        public const string DotNetCommandLineTools = ".NET Command Line Tools";

        public const string WriteLineForwarderSetPreviously = "WriteLine forwarder set previously";

        public const string AlreadyCapturingStream = "Already capturing stream!";

        public const string RunningFileNameArguments = "Running {0} {1}";

        public const string ProcessExitedWithCode = "< {0} exited with {1} in {2} ms.";

        public const string UnableToInvokeMemberNameAfterCommand = "Unable to invoke {0} after the command has been run";
    }
}