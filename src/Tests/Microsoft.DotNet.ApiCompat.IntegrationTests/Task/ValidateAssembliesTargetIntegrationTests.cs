// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.ApiCompat.IntegrationTests;
using Microsoft.DotNet.ApiCompatibility;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using Microsoft.DotNet.ApiCompatibility.Logging;
using Microsoft.DotNet.ApiCompatibility.Runner;
using Microsoft.DotNet.PackageValidation;
using Microsoft.DotNet.PackageValidation.Tests;
using Microsoft.DotNet.PackageValidation.Validators;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.ApiCompat.Task.IntegrationTests
{
    public class ValidateAssembliesTargetIntegrationTests : SdkTest
    {
        public ValidateAssembliesTargetIntegrationTests(ITestOutputHelper log) : base(log)
        {
        }
    }
}
