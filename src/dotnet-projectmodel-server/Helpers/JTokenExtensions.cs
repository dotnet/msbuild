// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.ProjectModel.Server.Helpers
{
    public static class JTokenExtensions
    {
        public static string GetValue(this JToken token, string name)
        {
            return GetValue<string>(token, name);
        }

        public static TVal GetValue<TVal>(this JToken token, string name)
        {
            var value = token?[name];
            if (value != null)
            {
                return value.Value<TVal>();
            }

            return default(TVal);
        }
    }
}
