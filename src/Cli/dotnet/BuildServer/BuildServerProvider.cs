// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.BuildServer
{
    internal class BuildServerProvider : IBuildServerProvider
    {
        public const string PidFileDirectoryVariableName = "DOTNET_BUILD_PIDFILE_DIRECTORY";
        private readonly IFileSystem _fileSystem;
        private readonly IEnvironmentProvider _environmentProvider;
        private readonly IReporter _reporter;

        public BuildServerProvider(
            IFileSystem fileSystem = null,
            IEnvironmentProvider environmentProvider = null,
            IReporter reporter = null)
        {
            _fileSystem = fileSystem ?? FileSystemWrapper.Default;
            _environmentProvider = environmentProvider ?? new EnvironmentProvider();
            _reporter = reporter ?? Reporter.Error;
        }

        public IEnumerable<IBuildServer> EnumerateBuildServers(ServerEnumerationFlags flags = ServerEnumerationFlags.All)
        {
            if ((flags & ServerEnumerationFlags.MSBuild) == ServerEnumerationFlags.MSBuild)
            {
                // Yield a single MSBuild server (handles server discovery itself)
                // TODO: use pid file enumeration when supported by the server (https://github.com/dotnet/cli/issues/9113)
                yield return new MSBuildServer();
            }

            if ((flags & ServerEnumerationFlags.VBCSCompiler) == ServerEnumerationFlags.VBCSCompiler)
            {
                // Yield a single VB/C# compiler (handles server discovery itself)
                // TODO: use pid file enumeration when supported by the server (https://github.com/dotnet/cli/issues/9112)
                yield return new VBCSCompilerServer();
            }

            // TODO: remove or amend this check when the following issues are resolved:
            // https://github.com/dotnet/cli/issues/9112
            // https://github.com/dotnet/cli/issues/9113
            if ((flags & ServerEnumerationFlags.Razor) != ServerEnumerationFlags.Razor)
            {
                yield break;
            }

            var directory = GetPidFileDirectory();

            if (!_fileSystem.Directory.Exists(directory.Value))
            {
                yield break;
            }

            foreach (var path in _fileSystem.Directory.EnumerateFiles(directory.Value))
            {
                if ((flags & ServerEnumerationFlags.Razor) == ServerEnumerationFlags.Razor &&
                    Path.GetFileName(path).StartsWith(RazorPidFile.FilePrefix))
                {
                    var file = ReadRazorPidFile(new FilePath(path));
                    if (file != null)
                    {
                        yield return new RazorServer(file);
                    }
                }
            }
        }

        public DirectoryPath GetPidFileDirectory()
        {
            var directory = _environmentProvider.GetEnvironmentVariable(PidFileDirectoryVariableName);
            if (!string.IsNullOrEmpty(directory))
            {
                return new DirectoryPath(directory);
            }

            return new DirectoryPath(
                Path.Combine(
                    CliFolderPathCalculator.DotnetUserProfileFolderPath,
                    "pids",
                    "build"));
        }

        private RazorPidFile ReadRazorPidFile(FilePath path)
        {
            try
            {
                return RazorPidFile.Read(path, _fileSystem);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                _reporter.WriteLine(
                    string.Format(
                        LocalizableStrings.FailedToReadPidFile,
                        path.Value,
                        ex.Message).Yellow());
                return null;
            }
        }
    }
}
