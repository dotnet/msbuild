// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Linq;


namespace Microsoft.Build.Framework
{

    public class EnvironmentWriter
    {
        public static Action<string> OutputWriter;

        public static void WriteEnvironmentVariables(
            string message,
            IDictionary<string, string> environment = null,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0)
        {
            if (OutputWriter == null)
            {
                return;
            }

            lock (OutputWriter)
            {
                environment = environment ?? GetEnvironmentVariables();

                OutputWriter($"======={Path.GetFileNameWithoutExtension(sourceFilePath)}:{memberName}:{sourceLineNumber}=======");
                OutputWriter(message);
                OutputWriter($"{environment.Keys.Count} variables");

                foreach (var key in environment.Keys.OrderBy(_ => _))
                {
                    var value = environment[key];

                    value = value == null
                        ? $"[null]"
                        : value.Equals(string.Empty)
                            ? "[empty]"
                            : value;

                    OutputWriter($"\t{key}=[{value}]");
                }

                OutputWriter("========================================================");
            }
        }

        public static Dictionary<string, string> GetEnvironmentVariables()
        {
            var environment = Environment.GetEnvironmentVariables();

            var dictionary = new Dictionary<string, string>(environment.Keys.Count);

            foreach (DictionaryEntry var in environment)
            {
                dictionary[(string) var.Key] = (string) var.Value;
            }

            return dictionary;
        }

        public static void Write(string message)
        {
            if (OutputWriter == null)
            {
                return;
            }

            lock (OutputWriter)
            {
                OutputWriter(message);
            }
        }
    }
}
