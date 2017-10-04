// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.TemplateEngine.Cli
{
    internal static class TelemetryConstants
    {
        // event name suffixes
        public static readonly string InstallEventSuffix = "-install";
        public static readonly string HelpEventSuffix = "-help";
        public static readonly string CreateEventSuffix = "-create-template";
        public static readonly string CalledWithNoArgsEventSuffix = "-called-with-no-args";

        // install event args
        public static readonly string ToInstallCount = "CountOfThingsToInstall";

        // create event args
        public static readonly string Language = "language";
        public static readonly string ArgError = "argument-error";
        public static readonly string Framework = "framework";
        public static readonly string TemplateName = "template-name";
        public static readonly string IsTemplateThirdParty = "is-template-3rd-party";
        public static readonly string Auth = "auth";
        public static readonly string CreationResult = "create-success";
    }
}
