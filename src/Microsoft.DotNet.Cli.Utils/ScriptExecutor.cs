using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.Cli.Utils.CommandParsing;
using Microsoft.DotNet.InternalAbstractions;
using Microsoft.DotNet.ProjectModel;

namespace Microsoft.DotNet.Cli.Utils
{
    public static class ScriptExecutor
    {
        public static ICommand CreateCommandForScript(Project project, string scriptCommandLine, IDictionary<string, string> variables)
        {
            return CreateCommandForScript(project, scriptCommandLine, WrapVariableDictionary(variables));
        }

        public static ICommand CreateCommandForScript(Project project, string scriptCommandLine, Func<string, string> getVariable)
        {
            var scriptArguments = ParseScriptArguments(project, scriptCommandLine, getVariable);
            if (scriptArguments == null)
            {
                throw new Exception($"ScriptExecutor: failed to parse script \"{scriptCommandLine}\"");
            }

            var inferredExtensions = DetermineInferredScriptExtensions();
            
            return Command
                    .CreateForScript(scriptArguments.First(), scriptArguments.Skip(1), project, inferredExtensions)
                    .WorkingDirectory(project.ProjectDirectory);
        }

        private static IEnumerable<string> ParseScriptArguments(Project project, string scriptCommandLine, Func<string, string> getVariable)
        {
            var scriptArguments = CommandGrammar.Process(
                scriptCommandLine,
                GetScriptVariable(project, getVariable),
                preserveSurroundingQuotes: false);

            scriptArguments = scriptArguments.Where(argument => !string.IsNullOrEmpty(argument)).ToArray();
            if (scriptArguments.Length == 0)
            {
                return null;
            }

            return scriptArguments;
        }

        private static string[] DetermineInferredScriptExtensions()
        {
            if (RuntimeEnvironment.OperatingSystemPlatform == Platform.Windows)
            {
                return new string[] { "", ".cmd" };
            }
            else
            {
                return new string[] { "", ".sh" };
            }
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
