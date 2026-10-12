// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework.Telemetry;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Framework.UnitTests;

public class TelemetryDataUtils_Tests
{
    [Theory]
    [InlineData("Microsoft.Build.Logging.ConsoleLogger")]
    [InlineData("Microsoft.Build.BackEnd.Logging.CentralForwardingLogger")]
    [InlineData("Microsoft.VisualStudio.Build.SomeLogger")]
    [InlineData("Microsoft.Build.Logging.OuterLogger+InnerLogger")]
    public void GetHashedUnlessMicrosoftType_ReportsTheNameOfAMicrosoftTypeAsIs(string typeName)
    {
        TelemetryDataUtils.GetHashedUnlessMicrosoftType(typeName).ShouldBe(typeName);
    }

    [Theory]
    [InlineData("Contoso.Billing.CustomerLogger")]
    [InlineData("CustomerLogger")]
    [InlineData("MicrosoftFoo.Logger")]
    [InlineData("Contoso.Microsoft.Logger")]
    [InlineData("microsoft.build.logging.ConsoleLogger")]
    [InlineData("MICROSOFT.Build.Logging.ConsoleLogger")]
    [InlineData("Microsoft")]
    [InlineData("")]
    public void GetHashedUnlessMicrosoftType_HashesTheNameOfAnyOtherType(string typeName)
    {
        string reported = TelemetryDataUtils.GetHashedUnlessMicrosoftType(typeName);

        reported.ShouldBe(TelemetryDataUtils.GetHashed(typeName));
        reported.ShouldNotBe(typeName);
    }

    [Fact]
    public void GetHashedUnlessMicrosoftType_HashesAMissingTypeNameLikeAnEmptyOne()
    {
        TelemetryDataUtils.GetHashedUnlessMicrosoftType(null).ShouldBe(TelemetryDataUtils.GetHashed(string.Empty));
    }

    [Fact]
    public void GetHashedUnlessMicrosoftType_HashesTheNameOfAConstructedGenericTypeBecauseItListsTheTypeArguments()
    {
        string typeName = typeof(GenericLogger<CustomerSpecificType>).FullName!;
        typeName.ShouldStartWith("Microsoft.");
        typeName.ShouldContain(nameof(CustomerSpecificType));

        string reported = TelemetryDataUtils.GetHashedUnlessMicrosoftType(typeName);

        reported.ShouldBe(TelemetryDataUtils.GetHashed(typeName));
        reported.ShouldNotContain(nameof(CustomerSpecificType));
    }

    private sealed class GenericLogger<T>
    {
    }

    private sealed class CustomerSpecificType
    {
    }
}
