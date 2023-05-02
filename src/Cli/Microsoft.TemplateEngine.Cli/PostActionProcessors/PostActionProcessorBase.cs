// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Cli.PostActionProcessors
{
    public abstract class PostActionProcessorBase : IPostActionProcessor
    {
        public abstract Guid Id { get; }

        public bool Process(
            IEngineEnvironmentSettings environment,
            IPostAction action,
            ICreationEffects creationEffects,
            ICreationResult templateCreationResult,
            string outputBasePath)
        {
            if (string.IsNullOrWhiteSpace(outputBasePath))
            {
                throw new ArgumentException($"'{nameof(outputBasePath)}' cannot be null or whitespace.", nameof(outputBasePath));
            }
            outputBasePath = Path.GetFullPath(outputBasePath);
            return ProcessInternal(environment, action, creationEffects, templateCreationResult, outputBasePath);
        }

        /// <summary>
        /// Gets absolute normalized path for a target matching <paramref name="sourcePathGlob"/>.
        /// </summary>
        protected static IReadOnlyList<string> GetTargetForSource(ICreationEffects2 creationEffects, string sourcePathGlob, string outputBasePath)
        {
            Glob g = Glob.Parse(sourcePathGlob);
            List<string> results = new();

            if (creationEffects.FileChanges != null)
            {
                foreach (IFileChange2 change in creationEffects.FileChanges)
                {
                    if (g.IsMatch(change.SourceRelativePath))
                    {
                        results.Add(Path.GetFullPath(change.TargetRelativePath, outputBasePath));
                    }
                }
            }
            return results;
        }

        protected static IReadOnlyList<string>? GetConfiguredFiles(
            IReadOnlyDictionary<string, string> postActionArgs,
            ICreationEffects creationEffects,
            string argName,
            string outputBasePath,
            Func<string, bool>? matchCriteria = null)
        {
            if (creationEffects is not ICreationEffects2 creationEffects2)
            {
                return null;
            }
            if (!postActionArgs.TryGetValue(argName, out string? targetFiles))
            {
                return null;
            }
            if (string.IsNullOrWhiteSpace(targetFiles))
            {
                return null;
            }

            // try to parse the argument as json; if it is not valid json, use it as a string
            if (targetFiles.TryParse(out JToken? config))
            {
                if (config == null)
                {
                    return null;
                }

                if (config.Type == JTokenType.String)
                {
                    return ProcessPaths(config.ToString().Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
                }
                else if (config is JArray arr)
                {
                    List<string> parts = new();

                    foreach (JToken token in arr)
                    {
                        if (token.Type != JTokenType.String)
                        {
                            continue;
                        }
                        parts.Add(token.ToString());
                    }

                    if (parts.Count > 0)
                    {
                        return ProcessPaths(parts);
                    }
                }
            }

            return ProcessPaths(targetFiles.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));

            IReadOnlyList<string> ProcessPaths(IReadOnlyList<string> paths)
            {
                matchCriteria ??= p => true;
                return paths
                    .SelectMany(t => GetTargetForSource(creationEffects2, t, outputBasePath))
                    .Where(t => matchCriteria(t))
                    .ToArray();
            }
        }

        protected abstract bool ProcessInternal(
            IEngineEnvironmentSettings environment,
            IPostAction action,
            ICreationEffects creationEffects,
            ICreationResult templateCreationResult,
            string outputBasePath);
    }
}
