// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli
{
    internal class CliEnvironment : IEnvironment
    {
        private const int DefaultBufferWidth = 160;
        private readonly IReadOnlyDictionary<string, string> _environmentVariables;

        public CliEnvironment()
        {
            Dictionary<string, string> variables = new(StringComparer.OrdinalIgnoreCase)
            {
                { "TEMPLATE_ENGINE_DISABLE_FILEWATCHER", "1" }
            };
            var env = Environment.GetEnvironmentVariables();
            foreach (string key in env.Keys.OfType<string>())
            {
                variables[key] = (env[key] as string) ?? string.Empty;
            }

            _environmentVariables = variables;
        }

        /// <inheritdoc/>
        public string NewLine { get; } = Environment.NewLine;

        /// <inheritdoc/>
        // Console.BufferWidth can throw if there's no console, such as when output is redirected, so
        // first check if it is redirected, and fall back to a default value if needed.
        public int ConsoleBufferWidth => Console.IsOutputRedirected ? DefaultBufferWidth : Console.BufferWidth;

        /// <inheritdoc/>
        public string ExpandEnvironmentVariables(string name)
        {
            return Environment.ExpandEnvironmentVariables(name);
        }

        /// <inheritdoc/>
        public string? GetEnvironmentVariable(string name)
        {
            _environmentVariables.TryGetValue(name, out string? result);
            return result;
        }

        /// <inheritdoc/>
        public IReadOnlyDictionary<string, string> GetEnvironmentVariables()
        {
            return _environmentVariables;
        }
    }
}
