// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
