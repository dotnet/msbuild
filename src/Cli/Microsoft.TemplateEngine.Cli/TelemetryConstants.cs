// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

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
        internal const string IsTemplateThirdParty = "is-template-3rd-party";
        internal const string Auth = "auth";
        internal const string CreationResult = "create-success";
    }
}
