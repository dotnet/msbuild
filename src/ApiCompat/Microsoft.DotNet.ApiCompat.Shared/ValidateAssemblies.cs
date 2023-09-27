// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ApiCompatibility;
using Microsoft.DotNet.ApiCompatibility.Logging;
using Microsoft.DotNet.ApiCompatibility.Rules;
using Microsoft.DotNet.ApiCompatibility.Runner;

namespace Microsoft.DotNet.ApiCompat
{
    internal static class ValidateAssemblies
    {
        public static void Run(Func<ISuppressionEngine, ISuppressibleLog> logFactory,
            bool generateSuppressionFile,
            bool preserveUnnecessarySuppressions,
            bool permitUnnecessarySuppressions,
            string[]? suppressionFiles,
            string? suppressionOutputFile,
            string? noWarn,
            bool respectInternals,
            bool enableRuleAttributesMustMatch,
            string[]? excludeAttributesFiles,
            bool enableRuleCannotChangeParameterName,
            string[] leftAssemblies,
            string[] rightAssemblies,
            bool enableStrictMode,
            string[][]? leftAssembliesReferences,
            string[][]? rightAssembliesReferences,
            bool createWorkItemPerAssembly,
            (string CaptureGroupPattern, string ReplacementString)[]? leftAssembliesTransformationPatterns,
            (string CaptureGroupPattern, string ReplacementString)[]? rightAssembliesTransformationPatterns)
        {
            // Initialize the service provider
            ApiCompatServiceProvider serviceProvider = new(logFactory,
                () => SuppressionFileHelper.CreateSuppressionEngine(suppressionFiles, noWarn, generateSuppressionFile),
                (log) => new RuleFactory(log,
                    enableRuleAttributesMustMatch,
                    enableRuleCannotChangeParameterName),
                respectInternals,
                excludeAttributesFiles);

            IApiCompatRunner apiCompatRunner = serviceProvider.ApiCompatRunner;
            ApiCompatRunnerOptions apiCompatOptions = new(enableStrictMode);

            // Optionally provide a string transformer if a transformation pattern is passed in.
            RegexStringTransformer? leftAssembliesStringTransformer = leftAssembliesTransformationPatterns != null ? new RegexStringTransformer(leftAssembliesTransformationPatterns) : null;
            RegexStringTransformer? rightAssembliesStringTransformer = rightAssembliesTransformationPatterns != null ? new RegexStringTransformer(rightAssembliesTransformationPatterns) : null;

            if (createWorkItemPerAssembly)
            {
                if (leftAssemblies.Length != rightAssemblies.Length)
                {
                    throw new Exception(CommonResources.CreateWorkItemPerAssemblyAssembliesNotEqual);
                }

                for (int i = 0; i < leftAssemblies.Length; i++)
                {
                    IReadOnlyList<MetadataInformation> leftMetadataInformation = GetMetadataInformation(leftAssemblies[i], GetAssemblyReferences(leftAssembliesReferences, i), leftAssembliesStringTransformer);
                    IReadOnlyList<MetadataInformation> rightMetadataInformation = GetMetadataInformation(rightAssemblies[i], GetAssemblyReferences(rightAssembliesReferences, i), rightAssembliesStringTransformer);

                    // Enqueue the work item
                    ApiCompatRunnerWorkItem workItem = new(leftMetadataInformation, apiCompatOptions, rightMetadataInformation);
                    apiCompatRunner.EnqueueWorkItem(workItem);
                }
            }
            else
            {
                // Create the work item that corresponds to the passed in left assembly.
                List<MetadataInformation> leftAssembliesMetadataInformation = new(leftAssemblies.Length);
                for (int i = 0; i < leftAssemblies.Length; i++)
                {
                    leftAssembliesMetadataInformation.AddRange(GetMetadataInformation(leftAssemblies[i], GetAssemblyReferences(leftAssembliesReferences, i), leftAssembliesStringTransformer));
                }

                List<MetadataInformation> rightAssembliesMetadataInformation = new(rightAssemblies.Length);
                for (int i = 0; i < rightAssemblies.Length; i++)
                {
                    rightAssembliesMetadataInformation.AddRange(GetMetadataInformation(rightAssemblies[i], GetAssemblyReferences(rightAssembliesReferences, i), rightAssembliesStringTransformer));
                }

                // Enqueue the work item
                ApiCompatRunnerWorkItem workItem = new(leftAssembliesMetadataInformation, apiCompatOptions, rightAssembliesMetadataInformation);
                apiCompatRunner.EnqueueWorkItem(workItem);
            }

            // Execute the enqueued work item(s).
            apiCompatRunner.ExecuteWorkItems();

            SuppressionFileHelper.LogApiCompatSuccessOrFailure(generateSuppressionFile, serviceProvider.SuppressibleLog);

            if (generateSuppressionFile)
            {
                SuppressionFileHelper.GenerateSuppressionFile(serviceProvider.SuppressionEngine,
                    serviceProvider.SuppressibleLog,
                    preserveUnnecessarySuppressions,
                    suppressionFiles,
                    suppressionOutputFile);
            }
            else if (!permitUnnecessarySuppressions)
            {
                SuppressionFileHelper.ValidateUnnecessarySuppressions(serviceProvider.SuppressionEngine, serviceProvider.SuppressibleLog);
            }
        }

        private static string[]? GetAssemblyReferences(string[][]? assemblyReferences, int counter)
        {
            if (assemblyReferences == null || assemblyReferences.Length == 0)
                return null;

            if (assemblyReferences.Length > counter)
            {
                return assemblyReferences[counter];
            }

            // If explicit assembly references weren't provided for an assembly, return the ones provided first
            // so that consumers can provide one shareable set of references for all left/right inputs.
            return assemblyReferences[0];
        }

        private static IReadOnlyList<MetadataInformation> GetMetadataInformation(string path,
            IEnumerable<string>? assemblyReferences,
            RegexStringTransformer? regexStringTransformer)
        {
            List<MetadataInformation> metadataInformation = new();
            foreach (string assembly in GetFilesFromPath(path))
            {
                metadataInformation.Add(new MetadataInformation(
                    assemblyName: Path.GetFileNameWithoutExtension(assembly),
                    assemblyId: regexStringTransformer?.Transform(assembly) ?? assembly,
                    fullPath: assembly,
                    references: assemblyReferences));
            }

            return metadataInformation;
        }

        private static IEnumerable<string> GetFilesFromPath(string path)
        {
            // Check if the given path is a directory
            if (Directory.Exists(path))
            {
                return Directory.EnumerateFiles(path, "*.dll");
            }

            // If the path isn't a directory, see if it's a glob expression.
            string filename = Path.GetFileName(path);
#if NETCOREAPP
            if (filename.Contains('*'))
#else
            if (filename.Contains("*"))
#endif
            {
                string? directoryName = Path.GetDirectoryName(path);
                if (directoryName != null)
                {
                    try
                    {
                        return Directory.EnumerateFiles(directoryName, filename);
                    }
                    catch (ArgumentException)
                    {
                    }
                }
            }

            return new string[] { path };
        }
    }
}
