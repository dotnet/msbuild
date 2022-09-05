// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.ApiCompatibility.Runner;
using Microsoft.DotNet.PackageValidation.Tests;
using Moq;
using Xunit;

namespace Microsoft.DotNet.PackageValidation.Validators.Tests
{
    public class BaselinePackageValidatorTests
    {
        private (TestLogger, BaselinePackageValidator) CreateLoggerAndValidator()
        {
            TestLogger log = new();
            BaselinePackageValidator validator = new(log,
                Mock.Of<IApiCompatRunner>());

            return (log, validator);
        }

        [Fact]
        public void TfmDroppedInLatestVersion()
        {
            string[] previousFilePaths = new[]
            {
                @"ref/netcoreapp3.1/TestPackage.dll",
                @"ref/netstandard2.0/TestPackage.dll"
            };
            Package baselinePackage = new(string.Empty, "TestPackage", "1.0.0", previousFilePaths, null, null);

            string[] currentFilePaths = new[]
            {
                @"ref/netcoreapp3.1/TestPackage.dll"
            };
            Package package = new(string.Empty, "TestPackage", "2.0.0", currentFilePaths, null, null);
            (TestLogger log, BaselinePackageValidator validator) = CreateLoggerAndValidator();

            validator.Validate(new PackageValidatorOption(package,
                enableStrictMode: false,
                enqueueApiCompatWorkItems: false,
                baselinePackage: baselinePackage));

            Assert.NotEmpty(log.errors);
            Assert.Contains(DiagnosticIds.TargetFrameworkDropped + " " + string.Format(Resources.MissingTargetFramework, ".NETStandard,Version=v2.0"), log.errors);
        }
    }
}
