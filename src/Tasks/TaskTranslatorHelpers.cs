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

        public static void Translate(this ITranslator translator, ref Dependencies dependencies, Type t)
        {
            translator.TranslateDictionary(ref dependencies.dependencies, (ITranslator translator, ref DependencyFile dependency) => {
                if (t == typeof(ResGenDependencies.ResXFile))
                {
                    ResGenDependencies.ResXFile resx = dependency as ResGenDependencies.ResXFile;
                    resx ??= new();
                    translator.Translate(ref resx.linkedFiles);
                    dependency = resx;
                }
                else if (t == typeof(ResGenDependencies.PortableLibraryFile))
                {
                    ResGenDependencies.PortableLibraryFile lib = dependency as ResGenDependencies.PortableLibraryFile;
                    lib ??= new();
                    translator.Translate(ref lib.assemblySimpleName);
                    translator.Translate(ref lib.outputFiles);
                    translator.Translate(ref lib.neutralResourceLanguage);
                    dependency = lib;
                }

                dependency ??= new();
                translator.Translate(ref dependency.filename);
                translator.Translate(ref dependency.lastModified);
                translator.Translate(ref dependency.exists);
            });
        }

        public static void Translate(this ITranslator translator, ref StateFileBase stateFile, Type t)
        {
            if (t == typeof(ResGenDependencies))
            {
                ResGenDependencies rgd = stateFile as ResGenDependencies;
                rgd ??= new();
                translator.Translate(ref rgd.resXFiles, typeof(ResGenDependencies.ResXFile));
                translator.Translate(ref rgd.portableLibraries, typeof(ResGenDependencies.PortableLibraryFile));
                translator.Translate(ref rgd.baseLinkedFileDirectory);
                stateFile = rgd;
            }
#if NETFRAMEWORK
            else if (t == typeof(ResolveComReferenceCache))
            {
                ResolveComReferenceCache rcrc = stateFile as ResolveComReferenceCache;
                rcrc ??= new(string.Empty, string.Empty);
                translator.Translate(ref rcrc.axImpLocation);
                translator.Translate(ref rcrc.tlbImpLocation);
                translator.TranslateDictionary(ref rcrc.componentTimestamps, StringComparer.Ordinal);
                stateFile = rcrc;
            }
            else if (t == typeof(AssemblyRegistrationCache))
            {
                AssemblyRegistrationCache arc = stateFile as AssemblyRegistrationCache;
                arc ??= new();
                translator.Translate(ref arc._assemblies);
                translator.Translate(ref arc._typeLibraries);
                stateFile = arc;
            }
#endif
        }
    }
}
