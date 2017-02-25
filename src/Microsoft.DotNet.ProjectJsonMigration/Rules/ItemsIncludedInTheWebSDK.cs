// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.ProjectJsonMigration.Rules
{
    internal class ItemsIncludedInTheWebSDK
    {
        public static bool HasContent(string content)
        {
            return content.Equals("wwwroot") ||
                content.Contains("web.config") ||
                content.Equals("**/*.cshtml") ||
                content.Equals(@"**\*.cshtml") ||
                content.Contains(".json");
        }
    }
}