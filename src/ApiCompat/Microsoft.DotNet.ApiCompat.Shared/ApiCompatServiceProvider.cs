// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.DotNet.ApiCompatibility;
using Microsoft.DotNet.ApiCompatibility.Comparing;
using Microsoft.DotNet.ApiCompatibility.Logging;
using Microsoft.DotNet.ApiCompatibility.Rules;
using Microsoft.DotNet.ApiCompatibility.Runner;
using Microsoft.DotNet.ApiSymbolExtensions;
using Microsoft.DotNet.ApiSymbolExtensions.Filtering;

namespace Microsoft.DotNet.ApiCompat
{
    internal sealed class ApiCompatServiceProvider
    {
        private readonly Lazy<ISuppressionEngine> _suppressionEngine;
        private readonly Lazy<ISuppressableLog> _compatibilityLogger;
        private readonly Lazy<IApiCompatRunner> _apiCompatRunner;

        internal ApiCompatServiceProvider(Func<ISuppressionEngine, ISuppressableLog> logFactory,
            Func<ISuppressionEngine> suppressionEngineFactory,
            Func<ISuppressableLog, IRuleFactory> ruleFactory,
            bool respectInternals,
            string[]? excludeAttributesFiles)
        {
            _suppressionEngine = new Lazy<ISuppressionEngine>(suppressionEngineFactory);
            _compatibilityLogger = new Lazy<ISuppressableLog>(() => logFactory(SuppressionEngine));
            _apiCompatRunner = new Lazy<IApiCompatRunner>(() =>
            {
                CompositeSymbolFilter compositeSymbolFilter = new CompositeSymbolFilter()
                    .Add(new AccessibilitySymbolFilter(respectInternals));

                if (excludeAttributesFiles != null)
                {
                    compositeSymbolFilter.Add(new DocIdSymbolFilter(excludeAttributesFiles));
                }

                SymbolEqualityComparer symbolEqualityComparer = new();
                ApiComparerSettings apiComparerSettings = new(compositeSymbolFilter,
                    symbolEqualityComparer,
                    new AttributeDataEqualityComparer(symbolEqualityComparer,
                        new TypedConstantEqualityComparer(symbolEqualityComparer)),
                    respectInternals);

                return new ApiCompatRunner(SuppressableLog,
                    SuppressionEngine,
                    new ApiComparerFactory(ruleFactory(SuppressableLog), apiComparerSettings),
                    new AssemblySymbolLoaderFactory(respectInternals));
            });
        }

        public ISuppressionEngine SuppressionEngine => _suppressionEngine.Value;
        public ISuppressableLog SuppressableLog => _compatibilityLogger.Value;
        public IApiCompatRunner ApiCompatRunner => _apiCompatRunner.Value;
    }
}
