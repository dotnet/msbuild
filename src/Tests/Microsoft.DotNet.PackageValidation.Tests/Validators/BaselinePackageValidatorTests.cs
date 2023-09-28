// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.ApiCompatibility.Runner;
using Microsoft.DotNet.ApiCompatibility.Tests;
using Microsoft.DotNet.PackageValidation.Tests;
using Moq;

namespace Microsoft.DotNet.PackageValidation.Validators.Tests
{
    public class BaselinePackageValidatorTests
    {
        private (SuppressibleTestLog, BaselinePackageValidator) CreateLoggerAndValidator()
        {
            SuppressibleTestLog log = new();
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
            (SuppressibleTestLog log, BaselinePackageValidator validator) = CreateLoggerAndValidator();

            validator.Validate(new PackageValidatorOption(package,
                enableStrictMode: false,
                enqueueApiCompatWorkItems: false,
                baselinePackage: baselinePackage));

            Assert.NotEmpty(log.errors);
            Assert.Contains(DiagnosticIds.TargetFrameworkDropped + " " + string.Format(Resources.MissingTargetFramework, ".NETStandard,Version=v2.0"), log.errors);
        }
    }
}
