// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.BackEnd.SdkResolution;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Framework;
using SdkResolverContext = Microsoft.Build.Framework.SdkResolverContext;
using SdkResult = Microsoft.Build.BackEnd.SdkResolution.SdkResult;
using SdkResultFactory = Microsoft.Build.Framework.SdkResultFactory;

namespace Microsoft.Build.Unittest
{
    internal static class SdkUtilities
    {
        public static ProjectOptions CreateProjectOptionsWithResolver(SdkResolver resolver)
        {
            var context = EvaluationContext.Create(EvaluationContext.SharingPolicy.Isolated);
            var sdkService = (SdkResolverService)context.SdkResolverService;
            sdkService.InitializeForTests(null, new List<SdkResolver>() { resolver });

            return new ProjectOptions
            {
                EvaluationContext = context
            };
        }

        internal class ConfigurableMockSdkResolver : SdkResolver
        {
            private readonly Dictionary<string, SdkResult> _resultMap;
            private readonly Func<SdkReference, SdkResolverContext, SdkResultFactory, Framework.SdkResult> _resolveFunc;

            public ConcurrentDictionary<string, int> ResolvedCalls { get; } = new ConcurrentDictionary<string, int>();

            public ConfigurableMockSdkResolver(SdkResult result)
            {
                _resultMap = new Dictionary<string, SdkResult> { [result.SdkReference.Name] = result };
            }

            public ConfigurableMockSdkResolver(Dictionary<string, SdkResult> resultMap)
            {
                _resultMap = resultMap;
            }

            public ConfigurableMockSdkResolver(Func<SdkReference, SdkResolverContext, SdkResultFactory, Framework.SdkResult> resolveFunc)
            {
                _resolveFunc = resolveFunc;
            }

            public override string Name => nameof(ConfigurableMockSdkResolver);

            public override int Priority => int.MaxValue;

            public override Framework.SdkResult Resolve(SdkReference sdkReference, SdkResolverContext resolverContext, SdkResultFactory factory)
            {
                if (_resolveFunc != null)
                {
                    return _resolveFunc(sdkReference, resolverContext, factory);
                }

                ResolvedCalls.AddOrUpdate(sdkReference.Name, k => 1, (k, c) => c + 1);

                return _resultMap.TryGetValue(sdkReference.Name, out var result)
                    ? CloneSdkResult(result)
                    : null;
            }

            private SdkResult CloneSdkResult(SdkResult sdkResult)
            {
                if (!sdkResult.Success)
                {
                    return new SdkResult(sdkResult.SdkReference, sdkResult.Warnings, sdkResult.Errors);
                }

                IEnumerable<string> sdkResultPaths;
                if (sdkResult.Path == null)
                {
                    sdkResultPaths = Enumerable.Empty<string>();
                }
                else
                {
                    List<string> pathList = new List<string>();
                    pathList.Add(sdkResult.Path);
                    if (sdkResult.AdditionalPaths != null)
                    {
                        pathList.AddRange(sdkResult.AdditionalPaths);
                    }
                    sdkResultPaths = pathList;
                }

                Dictionary<string, SdkResultItem> sdkResultItems;

                if (sdkResult.ItemsToAdd == null)
                {
                    sdkResultItems = null;
                }
                else
                {
                    sdkResultItems = new Dictionary<string, SdkResultItem>(StringComparer.OrdinalIgnoreCase);
                    foreach (var item in sdkResult.ItemsToAdd)
                    {
                        Dictionary<string, string> newMetadata = null;
                        if (item.Value.Metadata != null)
                        {
                            newMetadata = new Dictionary<string, string>(item.Value.Metadata, StringComparer.OrdinalIgnoreCase);
                        }
                        sdkResultItems.Add(item.Key, new SdkResultItem(item.Value.ItemSpec, newMetadata));
                    }
                }

                return new SdkResult(sdkResult.SdkReference,
                                     sdkResultPaths,
                                     version: sdkResult.Version,
                                     sdkResult.PropertiesToAdd == null ? null : new Dictionary<string, string>(sdkResult.PropertiesToAdd, StringComparer.OrdinalIgnoreCase),
                                     sdkResultItems,
                                     sdkResult.Warnings);
            }
        }

        internal class FileBasedMockSdkResolver : SdkResolver
        {
            private readonly Dictionary<string, string> _mapping;

            public FileBasedMockSdkResolver(Dictionary<string, string> mapping)
            {
                _mapping = mapping;
            }
            public override string Name => "FileBasedMockSdkResolver";
            public override int Priority => int.MinValue;

            public override Framework.SdkResult Resolve(SdkReference sdkReference, SdkResolverContext resolverContext, SdkResultFactory factory)
            {
                resolverContext.Logger.LogMessage($"{nameof(resolverContext.ProjectFilePath)} = {resolverContext.ProjectFilePath}", MessageImportance.High);
                resolverContext.Logger.LogMessage($"{nameof(resolverContext.SolutionFilePath)} = {resolverContext.SolutionFilePath}", MessageImportance.High);
                resolverContext.Logger.LogMessage($"{nameof(resolverContext.MSBuildVersion)} = {resolverContext.MSBuildVersion}", MessageImportance.High);

                return _mapping.ContainsKey(sdkReference.Name)
                    ? factory.IndicateSuccess(_mapping[sdkReference.Name], null)
                    : factory.IndicateFailure(new[] { $"Not in {nameof(_mapping)}" });
            }
        }
    }
}
