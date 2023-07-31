// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Cli
{
    internal static class TelemetryConstants
    {
        // event names
        internal const string InstallEvent = "template/new-install";
        internal const string CreateEvent = "template/new-create-template";

        // install event args
        internal const string ToInstallCount = "CountOfThingsToInstall";

        // create event args
        internal const string Language = "language";

        internal const string ArgError = "argument-error";
        internal const string Framework = "framework";
        internal const string TemplateName = "template-name";
        internal const string TemplateShortName = "template-short-name";
        internal const string PackageName = "package-name";
        internal const string PackageVersion = "package-version";
        internal const string IsTemplateThirdParty = "is-template-3rd-party";
        internal const string Auth = "auth";
        internal const string CreationResult = "create-success";
    }
}
