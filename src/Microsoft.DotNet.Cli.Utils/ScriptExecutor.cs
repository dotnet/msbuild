using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli.Utils.CommandParsing;
using Microsoft.DotNet.ProjectModel;

namespace Microsoft.DotNet.Cli.Utils
{
    internal static class ScriptExecutor
    {
        public static Command CreateCommandForScript(Project project, string scriptCommandLine, IDictionary<string, string> variables)
        {
            return CreateCommandForScript(project, scriptCommandLine, WrapVariableDictionary(variables));
        }

        public static Command CreateCommandForScript(Project project, string scriptCommandLine, Func<string, string> getVariable)
        {
            // Preserve quotation marks around arguments since command is about to be passed to a shell. May need
            // the quotes to ensure the shell groups arguments correctly.
            var scriptArguments = CommandGrammar.Process(
                scriptCommandLine,
                GetScriptVariable(project, getVariable),
                preserveSurroundingQuotes: true);

            // Ensure the array won't be empty and the elements won't be null or empty strings.
            scriptArguments = scriptArguments.Where(argument => !string.IsNullOrEmpty(argument)).ToArray();
            if (scriptArguments.Length == 0)
            {
                return null;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Only forward slashes are used in script blocks. Replace with backslashes to correctly
                // locate the script. The directory separator is platform-specific.
                scriptArguments[0] = scriptArguments[0].Replace(
                    Path.AltDirectorySeparatorChar,
                    Path.DirectorySeparatorChar);

                // Command-lines on Windows are executed via "cmd /S /C" in order to support batch files, &&,
                // built-in commands like echo, et cetera. /S allows quoting the command as well as the arguments.
                // ComSpec is Windows-specific, and contains the full path to cmd.exe
                var comSpec = Environment.GetEnvironmentVariable("ComSpec");
                if (!string.IsNullOrEmpty(comSpec))
                {
                    scriptArguments =
                        new[] { comSpec, "/S", "/C", "\"" }
                        .Concat(scriptArguments)
                        .Concat(new[] { "\"" })
                        .ToArray();
                }
            }
            else
            {
                // Special-case a script name that, perhaps with added .sh, matches an existing file.
                var surroundWithQuotes = false;
                var scriptCandidate = scriptArguments[0];
                if (scriptCandidate.StartsWith("\"", StringComparison.Ordinal) &&
                    scriptCandidate.EndsWith("\"", StringComparison.Ordinal))
                {
                    // Strip surrounding quotes; they were required in project.json to keep the script name
                    // together but confuse File.Exists() e.g. "My Script", lacking ./ prefix and .sh suffix.
                    surroundWithQuotes = true;
                    scriptCandidate = scriptCandidate.Substring(1, scriptCandidate.Length - 2);
                }

                if (!scriptCandidate.EndsWith(".sh", StringComparison.Ordinal))
                {
                    scriptCandidate = scriptCandidate + ".sh";
                }

                if (File.Exists(Path.Combine(project.ProjectDirectory, scriptCandidate)))
                {
                    // scriptCandidate may be a path relative to the project root. If so, likely will not be found
                    // in the $PATH; add ./ to let bash know where to look.
                    var prefix = Path.IsPathRooted(scriptCandidate) ? string.Empty : "./";
                    var quote = surroundWithQuotes ? "\"" : string.Empty;
                    scriptArguments[0] = $"{ quote }{ prefix }{ scriptCandidate }{ quote }";
                }

                // Always use /usr/bin/env bash -c in order to support redirection and so on; similar to Windows case.
                // Unlike Windows, must escape quotation marks within the newly-quoted string.
                scriptArguments = new[] { "/usr/bin/env", "bash", "-c", "\"" }
                    .Concat(scriptArguments.Select(argument => argument.Replace("\"", "\\\"")))
                    .Concat(new[] { "\"" })
                    .ToArray();
            }

            return Command.Create(scriptArguments.FirstOrDefault(), string.Join(" ", scriptArguments.Skip(1)))
                .WorkingDirectory(project.ProjectDirectory);
        }
        private static Func<string, string> WrapVariableDictionary(IDictionary<string, string> contextVariables)
        {
            return key =>
            {
                string value;
                contextVariables.TryGetValue(key, out value);
                return value;
            };
        }

        private static Func<string, string> GetScriptVariable(Project project, Func<string, string> getVariable)
        {
            var keys = new Dictionary<string, Func<string>>(StringComparer.OrdinalIgnoreCase)
            {
                { "project:Directory", () => project.ProjectDirectory },
                { "project:Name", () => project.Name },
                { "project:Version", () => project.Version.ToString() },
            };

            return key =>
            {
                // try returning key from dictionary
                Func<string> valueFactory;
                if (keys.TryGetValue(key, out valueFactory))
                {
                    return valueFactory();
                }

                // try returning command-specific key
                var value = getVariable(key);
                if (!string.IsNullOrEmpty(value))
                {
                    return value;
                }

                // try returning environment variable
                return Environment.GetEnvironmentVariable(key);
            };
        }
    }
}
