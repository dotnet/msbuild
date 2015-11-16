// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.DotNet.Cli.Compiler.Common
{
    internal class OptionTemplate
    {
        public string Template { get; }
        public string ShortName { get; }
        public string LongName { get; }

        private static char[] s_separator = { '|' };
        public OptionTemplate(string template)
        {
            Template = template;

            foreach (var part in Template.Split(s_separator, 2, StringSplitOptions.RemoveEmptyEntries))
            {
                if (part.Length == 1)
                {
                    ShortName = part;
                }
                else
                {
                    LongName = part;
                }
            }

            if (string.IsNullOrEmpty(LongName) && string.IsNullOrEmpty(ShortName))
            {
                throw new ArgumentException($"Invalid template pattern '{template}'", nameof(template));
            }
        }

        public string ToLongArg(object value)
        {
            return $"--{LongName}:{value}";
        }
    }
}
