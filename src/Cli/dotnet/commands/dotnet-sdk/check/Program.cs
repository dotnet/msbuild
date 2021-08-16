// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.NativeWrapper;
using System;
using System.CommandLine.Parsing;
using EnvironmentProvider = Microsoft.DotNet.NativeWrapper.EnvironmentProvider;
using Parser = Microsoft.DotNet.Cli.Parser;

namespace Microsoft.DotNet.Tools.Sdk.Check
{
    public class SdkCheckCommand : CommandBase
    {
        private readonly INETBundleProvider _netBundleProvider;
        private readonly IReporter _reporter;
        private readonly IProductCollectionProvider _productCollectionProvider;

        public SdkCheckCommand(
            ParseResult parseResult,
            INETBundleProvider bundleProvider = null,
            IProductCollectionProvider productCollectionProvider = null,
            IReporter reporter = null) : base(parseResult)
        {
            _reporter = reporter ?? Reporter.Output;
            _netBundleProvider = bundleProvider == null ? new NETBundlesNativeWrapper() : bundleProvider;
            _productCollectionProvider = productCollectionProvider == null ? new ProductCollectionProvider() : productCollectionProvider;
        }

        public override int Execute()
        {
            try
            {
                var dotnetPath = EnvironmentProvider.GetDotnetExeDirectory();
                var productCollection = _productCollectionProvider.GetProductCollection();
                var environmentInfo = _netBundleProvider.GetDotnetEnvironmentInfo(dotnetPath);
                var sdkFormatter = new SdkOutputWriter(environmentInfo.SdkInfo, productCollection, _productCollectionProvider, _reporter);
                var runtimeFormatter = new RuntimeOutputWriter(environmentInfo.RuntimeInfo, productCollection, _productCollectionProvider, _reporter);

                sdkFormatter.PrintSdkInfo();
                _reporter.WriteLine();
                runtimeFormatter.PrintRuntimeInfo();
                _reporter.WriteLine();
                _reporter.WriteLine(LocalizableStrings.CommandFooter);
                _reporter.WriteLine();
            }
            catch (HostFxrResolutionException hostfxrResolutionException)
            {
                switch (hostfxrResolutionException)
                {
                    case HostFxrRuntimePropertyNotSetException:
                        throw new GracefulException(new[] { LocalizableStrings.RuntimePropertyNotFound }, new string[] { }, isUserError: false);

                    case HostFxrNotFoundException hostFxrNotFoundException:
                        throw new GracefulException(new[] { LocalizableStrings.HostFxrCouldNotBeLoaded }, new string[] { hostFxrNotFoundException.Message }, isUserError: false);
                }
            }

            return 0;
        }

        public static int Run(string[] args)
        {
            var parseResult = Parser.Instance.ParseFrom("dotnet sdk check", args);

            return new SdkCheckCommand(parseResult).Execute();
        }
    }
}
