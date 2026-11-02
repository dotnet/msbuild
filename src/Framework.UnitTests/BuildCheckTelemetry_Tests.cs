// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework.Telemetry;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Framework.UnitTests;

public class BuildCheckTelemetry_Tests
{
    [Fact]
    public void CustomCheckLoadingFailureReportsOnlyTheHashOfTheExceptionMessage()
    {
        // Redacting the paths in the message leaves everything else in it, such as the names of customer types.
        string checkPath = Path.Combine(Path.GetTempPath(), "someone", "MyCheck.dll");
        InvalidOperationException exception = new($"Could not load {checkPath} for Contoso.Billing.Check");
        string sanitizedMessage = CrashTelemetry.TruncateMessage(exception.Message)!;

        (string eventName, IDictionary<string, string> properties) =
            new BuildCheckTelemetry().ProcessCustomCheckLoadingFailure("MyCheck", exception);

        eventName.ShouldBe("buildcheck/acquisitionfailure");
        properties["ExceptionMessage"].ShouldBe(TelemetryDataUtils.GetHashed(sanitizedMessage));
        properties["ExceptionMessage"].ShouldNotBe(sanitizedMessage);
        properties["ExceptionMessage"].ShouldNotContain("Contoso");
        properties["ExceptionType"].ShouldBe("System.InvalidOperationException");
    }

    [Fact]
    public void CustomCheckLoadingFailureReportsNoMessageWhenTheExceptionHasNone()
    {
        InvalidOperationException exception = new(string.Empty);

        (_, IDictionary<string, string> properties) = new BuildCheckTelemetry().ProcessCustomCheckLoadingFailure("MyCheck", exception);

        properties.ShouldNotContainKey("ExceptionMessage");
    }
}
