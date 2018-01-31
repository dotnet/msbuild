// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Microsoft.Build.Logging.StructuredLogger
{
    internal static class ItemGroupParser
    {
        /// <summary>
        /// Parses a log output string to a list of Items (e.g. ItemGroup with metadata or property string).
        /// </summary>
        /// <param name="message">The message output from the logger.</param>
        /// <param name="prefix">The prefix parsed out (e.g. 'Output Item(s): '.).</param>
        /// <param name="name">Out: The name of the list.</param>
        /// <returns>List of items within the list and all metadata.</returns>
        public static IList<Item> ParseItemList(string message, string prefix, out string name)
        {
            name = null;

            var items = new List<Item>();
            var lines = message.Split('\n');

            if (lines.Length == 1)
            {
                var line = lines[0];
                line = line.Substring(prefix.Length);
                var nameValue = ParseNameValue(line);
                name = nameValue.Key;
                items.Add(new Item(nameValue.Value));
                return items;
            }

            if (lines[0].Length > prefix.Length)
            {
                // we have a weird case of multi-line value
                var nameValue = ParseNameValue(lines[0].Substring(prefix.Length));
                name = nameValue.Key;

                items.Add(new Item(nameValue.Value.Replace("\r", "")));
                for (int i = 1; i < lines.Length; i++)
                {
                    items.Add(new Item(lines[i].Replace("\r", "")));
                }

                return items;
            }

            Item currentItem = null;
            foreach (var line in lines)
            {
                switch (GetNumberOfLeadingSpaces(line))
                {
                    case 4:
                        if (line.EndsWith("=", StringComparison.Ordinal))
                        {
                            name = line.Substring(4, line.Length - 5);
                        }
                        break;
                    case 8:
                        currentItem = new Item(line.Substring(8));
                        items.Add(currentItem);
                        break;
                    case 16:
                        if (currentItem != null)
                        {
                            var nameValue = ParseNameValue(line.Substring(16));
                            currentItem.AddMetadata(nameValue.Key, nameValue.Value);
                        }
                        break;
                }
            }

            return items;
        }

        /// <summary>
        /// Parse a string for a name value pair (name=value).
        /// </summary>
        /// <param name="nameEqualsValue">The (name = value) string to parse.</param>
        /// <returns>KeyValuePair name and value</returns>
        private static KeyValuePair<string, string> ParseNameValue(string nameEqualsValue)
        {
            var equals = nameEqualsValue.IndexOf('=');
            var name = nameEqualsValue.Substring(0, equals);
            var value = nameEqualsValue.Substring(equals + 1);
            return new KeyValuePair<string, string>(name, value);
        }

        /// <summary>
        /// Gets the number of leading spaces from a string
        /// </summary>
        /// <param name="line">The string.</param>
        /// <returns>Number of spaces in the beginning of the string.</returns>
        private static int GetNumberOfLeadingSpaces(string line)
        {
            int result = 0;
            while (result < line.Length && line[result] == ' ')
            {
                result++;
            }

            return result;
        }
    }
}
