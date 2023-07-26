// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.NativeWrapper;
using System.CommandLine;
using System.Text.Json;
using EnvironmentProvider = Microsoft.DotNet.NativeWrapper.EnvironmentProvider;

namespace Microsoft.DotNet.Tools.Sdk.Check
{
    public class SdkCheckCommand : CommandBase
    {
        private readonly INETBundleProvider _netBundleProvider;
        private readonly IReporter _reporter;
        private readonly IProductCollectionProvider _productCollectionProvider;
        private readonly string _dotnetPath;
        private readonly SdkCheckConfig _sdkCheckConfig;

        public SdkCheckCommand(
            ParseResult parseResult,
            INETBundleProvider bundleProvider = null,
            IProductCollectionProvider productCollectionProvider = null,
            IReporter reporter = null,
            string dotnetRoot = null, 
            string dotnetVersion = null) : base(parseResult)
        {
            _dotnetPath = dotnetRoot ?? EnvironmentProvider.GetDotnetExeDirectory();
            var configFilePath = Path.Combine(_dotnetPath, "sdk", dotnetVersion ?? Product.Version, "sdk-check-config.json");
            _sdkCheckConfig = File.Exists(configFilePath) ? JsonSerializer.Deserialize<SdkCheckConfig>(File.ReadAllText(configFilePath)) : null;
            _reporter = reporter ?? Reporter.Output;
            _netBundleProvider = bundleProvider == null ? new NETBundlesNativeWrapper() : bundleProvider;
            _productCollectionProvider = productCollectionProvider == null ? new ProductCollectionProvider() : productCollectionProvider;
        }

        public override int Execute()
        {
            if (_sdkCheckConfig != null && !string.IsNullOrEmpty(_sdkCheckConfig.CommandOutputReplacementString))
            {
                _reporter.WriteLine();
                _reporter.WriteLine(_sdkCheckConfig.CommandOutputReplacementString);
                _reporter.WriteLine();
            }
            else
            {
                try
                {
                    var productCollection = _productCollectionProvider.GetProductCollection(
                        _sdkCheckConfig?.ReleasesUri == null ? null : new Uri(_sdkCheckConfig.ReleasesUri),
                        _sdkCheckConfig?.ReleasesFilePath == null ? null : _sdkCheckConfig.ReleasesFilePath);
                    var environmentInfo = _netBundleProvider.GetDotnetEnvironmentInfo(_dotnetPath);
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
            }

            return 0;
        }

        public static int Run(ParseResult parseResult)
        {
            return new SdkCheckCommand(parseResult).Execute();
        }
    }

    internal class SdkCheckConfig
    {
        public string ReleasesUri { get; set; }
        public string ReleasesFilePath { get; set; }
        public string CommandOutputReplacementString { get; set; }
    }
}
