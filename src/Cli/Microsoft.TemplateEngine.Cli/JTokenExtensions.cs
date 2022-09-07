// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

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
