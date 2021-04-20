// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Cli
{
    internal static class TelemetryConstants
    {
        // event name suffixes
        internal static readonly string InstallEventSuffix = "-install";
        internal static readonly string HelpEventSuffix = "-help";
        internal static readonly string CreateEventSuffix = "-create-template";
        internal static readonly string CalledWithNoArgsEventSuffix = "-called-with-no-args";

        // install event args
        internal static readonly string ToInstallCount = "CountOfThingsToInstall";

        // create event args
        internal static readonly string Language = "language";
        internal static readonly string ArgError = "argument-error";
        internal static readonly string Framework = "framework";
        internal static readonly string TemplateName = "template-name";
        internal static readonly string IsTemplateThirdParty = "is-template-3rd-party";
        internal static readonly string Auth = "auth";
        internal static readonly string CreationResult = "create-success";
    }
}
