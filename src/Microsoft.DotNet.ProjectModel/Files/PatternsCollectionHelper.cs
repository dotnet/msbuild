// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.ProjectModel.Files
{
    internal static class PatternsCollectionHelper
    {
        private static readonly char[] PatternSeparator = new[] { ';' };

        public static IEnumerable<string> GetPatternsCollection(JObject rawProject,
                                                                string projectDirectory,
                                                                string projectFilePath,
                                                                string propertyName,
                                                                IEnumerable<string> defaultPatterns = null,
                                                                bool literalPath = false)
        {
            defaultPatterns = defaultPatterns ?? Enumerable.Empty<string>();

            try
            {
                JToken propertyNameToken;
                if (!rawProject.TryGetValue(propertyName, out propertyNameToken))
                {
                    return IncludeContext.CreateCollection(
                        projectDirectory, propertyName, defaultPatterns, literalPath);
                }

                if (propertyNameToken.Type == JTokenType.String)
                {
                    return IncludeContext.CreateCollection(
                        projectDirectory, propertyName, new string[] { propertyNameToken.Value<string>() }, literalPath);
                }

                if (propertyNameToken.Type == JTokenType.Array)
                {
                    var valuesInArray = propertyNameToken.Values<string>();
                    return IncludeContext.CreateCollection(
                        projectDirectory, propertyName, valuesInArray.Select(s => s.ToString()), literalPath);
                }
            }
            catch (Exception ex)
            {
                throw FileFormatException.Create(ex, rawProject.Value<JToken>(propertyName), projectFilePath);
            }

            throw FileFormatException.Create("Value must be either string or array.", rawProject.Value<JToken>(propertyName), projectFilePath);
        }
    }
}
