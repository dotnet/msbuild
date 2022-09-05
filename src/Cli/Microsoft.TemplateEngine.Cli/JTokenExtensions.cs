// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Cli
{
    internal static class JTokenExtensions
    {
        internal static bool TryParse(string arg, out JToken? token)
        {
            try
            {
                token = JToken.Parse(arg);

                return true;
            }
            catch
            {
                token = null;

                return false;
            }
        }
    }
}
