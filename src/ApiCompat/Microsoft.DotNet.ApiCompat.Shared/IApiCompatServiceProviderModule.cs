// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Jab;
using Microsoft.DotNet.ApiCompatibility;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using Microsoft.DotNet.ApiCompatibility.Logging;
using Microsoft.DotNet.ApiCompatibility.Rules;
using Microsoft.DotNet.ApiCompatibility.Runner;

namespace Microsoft.DotNet.ApiCompat
{
    [ServiceProviderModule]
    [Singleton(typeof(ISuppressionEngine), Factory = nameof(SuppressionEngineFactory))]
    [Singleton(typeof(ICompatibilityLogger), Factory = nameof(LogFactory))]
    [Singleton(typeof(IRuleFactory), Instance = nameof(RuleFactory))]
    [Singleton(typeof(IApiComparerFactory), typeof(ApiComparerFactory))]
    [Singleton(typeof(IAssemblySymbolLoaderFactory), typeof(AssemblySymbolLoaderFactory))]
    [Singleton(typeof(IMetadataStreamProvider), typeof(MetadataStreamProvider))]
    [Singleton(typeof(IApiCompatRunner), typeof(ApiCompatRunner))]
    internal interface IApiCompatServiceProviderModule
    {
        Func<ISuppressionEngine> SuppressionEngineFactory { get; }

        Func<ICompatibilityLogger> LogFactory { get; }

        RuleFactory RuleFactory { get; }
    }
}
