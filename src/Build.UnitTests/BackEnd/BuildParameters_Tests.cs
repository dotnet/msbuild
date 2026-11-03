// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.Build.UnitTests.BackEnd;

public class BuildParameters_Tests
{
    [Fact]
    public void PrecomputeIsClonedAndTranslated()
    {
        var parameters = new BuildParameters { Precompute = true };

        Assert.True(parameters.Clone().Precompute);

        ((ITranslatable)parameters).Translate(TranslationHelpers.GetWriteTranslator());
        BuildParameters deserializedParameters = BuildParameters.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

        Assert.True(deserializedParameters.Precompute);
    }
}
