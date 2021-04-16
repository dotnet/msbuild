// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using Microsoft.Build.BackEnd;

namespace Microsoft.Build.Tasks
{
    internal static class TaskTranslatorHelpers
    {
        public static void Translate(this ITranslator translator, ref FrameworkName frameworkName)
        {
            if (!translator.TranslateNullable(frameworkName))
                return;

            string identifier = null;
            Version version = null;
            string profile = null;

            if (translator.Mode == TranslationDirection.WriteToStream)
            {
                identifier = frameworkName.Identifier;
                version = frameworkName.Version;
                profile = frameworkName.Profile;
            }

            translator.Translate(ref identifier);
            translator.Translate(ref version);
            translator.Translate(ref profile);

            if (translator.Mode == TranslationDirection.ReadFromStream)
            {
                frameworkName = new FrameworkName(identifier, version, profile);
            }
        }

        public static void TranslateDictionary(this ITranslator translator, ref Dictionary<string, DateTime> dict, StringComparer comparer)
        {
            int count = 0;
            if (translator.Mode == TranslationDirection.ReadFromStream)
            {
                dict = new Dictionary<string, DateTime>(comparer);
                translator.Translate(ref count);
                string key = string.Empty;
                DateTime val = DateTime.Now;
                for (int i = 0; i < count; i++)
                {
                    translator.Translate(ref key);
                    translator.Translate(ref val);
                    dict.Add(key, val);
                }
            }
            else
            {
                count = dict.Count;
                translator.Translate(ref count);
                foreach (KeyValuePair<string, DateTime> kvp in dict)
                {
                    string key = kvp.Key;
                    DateTime val = kvp.Value;
                    translator.Translate(ref key);
                    translator.Translate(ref val);
                }
            }
        }
    }
}
