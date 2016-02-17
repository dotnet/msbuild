// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.DotNet.Tools.Compiler
{
    public class PPFilePreprocessor
    {
        public static void Preprocess(Stream input, Stream output, IDictionary<string, string> parameters)
        {
            string text;
            using (var streamReader = new StreamReader(input))
            {
                text = streamReader.ReadToEnd();
            }
            var tokenizer = new PPFileTokenizer(text);
            using (var streamWriter = new StreamWriter(output))
            {
                while (true)
                {
                    var token = tokenizer.Read();
                    if (token == null)
                    {
                        break;
                    }

                    if (token.Category == PPFileTokenizer.TokenCategory.Variable)
                    {
                        var replaced = ReplaceToken(token.Value, parameters);
                        streamWriter.Write(replaced);
                    }
                    else
                    {
                        streamWriter.Write(token.Value);
                    }
                }
            }
        }

        private static string ReplaceToken(string name, IDictionary<string, string> parameters)
        {
            string value;
            if (!parameters.TryGetValue(name, out value))
            {
                throw new InvalidOperationException($"The replacement token '{name}' has no value.");
            }
            return value;
        }
    }
}
