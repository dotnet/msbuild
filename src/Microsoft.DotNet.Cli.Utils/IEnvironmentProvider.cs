using System.Collections.Generic;

namespace Microsoft.DotNet.Cli.Utils
{
    public interface IEnvironmentProvider
    {
        IEnumerable<string> ExecutableExtensions { get; }

        string GetCommandPath(string commandName, params string[] extensions);

        string GetCommandPathFromRootPath(string rootPath, string commandName, params string[] extensions);

        string GetCommandPathFromRootPath(string rootPath, string commandName, IEnumerable<string> extensions);

        bool GetEnvironmentVariableAsBool(string name, bool defaultValue);

        string GetEnvironmentVariable(string name);
    }
}
