// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.DotNet.ApiCompatibility;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using Microsoft.DotNet.ApiCompatibility.Logging;
using Microsoft.DotNet.ApiCompatibility.Rules;
using Microsoft.DotNet.ApiCompatibility.Runner;

namespace Microsoft.DotNet.ApiCompat
{
    internal sealed class ApiCompatServiceProvider
    {
        private readonly Lazy<ISuppressionEngine> _suppressionEngine;
        private readonly Lazy<ICompatibilityLogger> _compatibilityLogger;
        private readonly Lazy<IApiCompatRunner> _apiCompatRunner;

        internal ApiCompatServiceProvider(Func<ISuppressionEngine, ICompatibilityLogger> logFactory,
            Func<ISuppressionEngine> suppressionEngineFactory,
            RuleFactory ruleFactory)
        {
            _suppressionEngine = new Lazy<ISuppressionEngine>(suppressionEngineFactory);
            _compatibilityLogger = new Lazy<ICompatibilityLogger>(() => logFactory(SuppressionEngine));
            _apiCompatRunner = new Lazy<IApiCompatRunner>(() =>
                new ApiCompatRunner(CompatibilityLogger, SuppressionEngine, new ApiComparerFactory(ruleFactory), new AssemblySymbolLoaderFactory(), new MetadataStreamProvider()));
        }

        public ISuppressionEngine SuppressionEngine => _suppressionEngine.Value;
        public ICompatibilityLogger CompatibilityLogger => _compatibilityLogger.Value;
        public IApiCompatRunner ApiCompatRunner => _apiCompatRunner.Value;
    }
}
