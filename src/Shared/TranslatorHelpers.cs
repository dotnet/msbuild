// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Configuration.Assemblies;
using System.Globalization;
using System.Reflection;
using AssemblyHashAlgorithm = System.Configuration.Assemblies.AssemblyHashAlgorithm;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// This class provides helper methods to adapt from <see cref="NodePacketValueFactory{T}"/> to
    /// <see cref="ObjectTranslator{T}"/>.
    /// </summary>
    internal static class TranslatorHelpers
    {
        /// <summary>
        /// Translates an object implementing <see cref="ITranslatable"/> which does not expose a
        /// public parameterless constructor.
        /// </summary>
        /// <typeparam name="T">The reference type.</typeparam>
        /// <param name="translator">The translator</param>
        /// <param name="instance">The value to be translated.</param>
        /// <param name="valueFactory">The factory method used to instantiate values of type T.</param>
        public static void Translate<T>(
            this ITranslator translator,
            ref T instance,
            NodePacketValueFactory<T> valueFactory) where T : ITranslatable
        {
            if (!translator.TranslateNullable(instance))
            {
                return;
            }
            if (translator.Mode == TranslationDirection.ReadFromStream)
            {
                instance = valueFactory(translator);
            }
            else
            {
                instance.Translate(translator);
            }
        }

        static ObjectTranslator<T> AdaptFactory<T>(NodePacketValueFactory<T> valueFactory) where T : ITranslatable
        {
            void TranslateUsingValueFactory(ITranslator translator, ref T objectToTranslate)
            {
                translator.Translate(ref objectToTranslate, valueFactory);
            }

            return TranslateUsingValueFactory;
        }

        public static void Translate<T>(
            this ITranslator translator,
            ref List<T> list,
            NodePacketValueFactory<T> valueFactory) where T : class, ITranslatable
        {
            translator.Translate(ref list, AdaptFactory(valueFactory));
        }

        public static void Translate<T, L>(
            this ITranslator translator,
            ref IList<T> list,
            NodePacketValueFactory<T> valueFactory,
            NodePacketCollectionCreator<L> collectionFactory) where L : IList<T> where T : ITranslatable
        {
            translator.Translate(ref list, AdaptFactory(valueFactory), collectionFactory);
        }

        public static void TranslateArray<T>(
            this ITranslator translator,
            ref T[] array,
            NodePacketValueFactory<T> valueFactory) where T : class, ITranslatable
        {
            translator.TranslateArray(ref array, AdaptFactory(valueFactory));
        }

        public static void TranslateDictionary<T>(
            this ITranslator translator,
            ref Dictionary<string, T> dictionary,
            IEqualityComparer<string> comparer,
            NodePacketValueFactory<T> valueFactory) where T : class, ITranslatable
        {
            translator.TranslateDictionary(ref dictionary, comparer, AdaptFactory(valueFactory));
        }

        public static void TranslateDictionary<D, T>(
            this ITranslator translator,
            ref D dictionary,
            NodePacketValueFactory<T> valueFactory)
            where D : IDictionary<string, T>, new()
            where T : class, ITranslatable
        {
            translator.TranslateDictionary(ref dictionary, AdaptFactory(valueFactory));
        }

        public static void TranslateDictionary<D, T>(
            this ITranslator translator,
            ref D dictionary,
            NodePacketValueFactory<T> valueFactory,
            NodePacketCollectionCreator<D> collectionCreator)
            where D : IDictionary<string, T>
            where T : class, ITranslatable
        {
            translator.TranslateDictionary(ref dictionary, AdaptFactory(valueFactory), collectionCreator);
        }

        public static void TranslateHashSet<T>(
            this ITranslator translator,
            ref HashSet<T> hashSet,
            NodePacketValueFactory<T> valueFactory,
            NodePacketCollectionCreator<HashSet<T>> collectionFactory) where T : class, ITranslatable
        {
            if (!translator.TranslateNullable(hashSet))
                return;

            int count = default;
            if (translator.Mode == TranslationDirection.WriteToStream)
            {
                count = hashSet.Count;
            }
            translator.Translate(ref count);

            if (translator.Mode == TranslationDirection.ReadFromStream)
            {
                hashSet = collectionFactory(count);
                for (int i = 0; i < count; i++)
                {
                    T value = default;
                    translator.Translate(ref value, valueFactory);
                    hashSet.Add(value);
                }
            }

            if (translator.Mode == TranslationDirection.WriteToStream)
            {
                foreach (T item in hashSet)
                {
                    T value = item;
                    translator.Translate(ref value, valueFactory);
                }
            }
        }

        public static void Translate(this ITranslator translator, ref CultureInfo cultureInfo)
        {
            if (!translator.TranslateNullable(cultureInfo))
                return;

            int lcid = default;

            if (translator.Mode == TranslationDirection.WriteToStream)
            {
                lcid = cultureInfo.LCID;
            }

            translator.Translate(ref lcid);

            if (translator.Mode == TranslationDirection.ReadFromStream)
            {
                cultureInfo = new CultureInfo(lcid);
            }
        }

        public static void Translate(this ITranslator translator, ref Version version)
        {
            if (!translator.TranslateNullable(version))
                return;

            int major = 0;
            int minor = 0;
            int build = 0;
            int revision = 0;

            if (translator.Mode == TranslationDirection.WriteToStream)
            {
                major = version.Major;
                minor = version.Minor;
                build = version.Build;
                revision = version.Revision;
            }

            translator.Translate(ref major);
            translator.Translate(ref minor);
            translator.Translate(ref build);
            translator.Translate(ref revision);

            if (translator.Mode == TranslationDirection.ReadFromStream)
            {
                if (build < 0)
                {
                    version = new Version(major, minor);
                }
                else if (revision < 0)
                {
                    version = new Version(major, minor, build);
                }
                else
                {
                    version = new Version(major, minor, build, revision);
                }
            }
        }

        public static void Translate(this ITranslator translator, ref AssemblyName assemblyName)
        {
            if (!translator.TranslateNullable(assemblyName))
                return;

            string name = null;
            Version version = null;
            AssemblyNameFlags flags = default;
            ProcessorArchitecture processorArchitecture = default;
            CultureInfo cultureInfo = null;
            AssemblyHashAlgorithm hashAlgorithm = default;
            AssemblyVersionCompatibility versionCompatibility = default;
            string codeBase = null;

            byte[] publicKey = null;
            byte[] publicKeyToken = null;

            if (translator.Mode == TranslationDirection.WriteToStream)
            {
                name = assemblyName.Name;
                version = assemblyName.Version;
                flags = assemblyName.Flags;
                processorArchitecture = assemblyName.ProcessorArchitecture;
                cultureInfo = assemblyName.CultureInfo;
                hashAlgorithm = assemblyName.HashAlgorithm;
                versionCompatibility = assemblyName.VersionCompatibility;
                codeBase = assemblyName.CodeBase;

                publicKey = assemblyName.GetPublicKey(); // TODO: no need to serialize, public key is not used anywhere in context of RAR, only public key token
                publicKeyToken = assemblyName.GetPublicKeyToken();
            }

            translator.Translate(ref name);
            translator.Translate(ref version);
            translator.TranslateEnum(ref flags, (int)flags);
            translator.TranslateEnum(ref processorArchitecture, (int)processorArchitecture);
            translator.Translate(ref cultureInfo);
            translator.TranslateEnum(ref hashAlgorithm, (int)hashAlgorithm);
            translator.TranslateEnum(ref versionCompatibility, (int)versionCompatibility);
            translator.Translate(ref codeBase);

            translator.Translate(ref publicKey);
            translator.Translate(ref publicKeyToken);

            if (translator.Mode == TranslationDirection.ReadFromStream)
            {
                assemblyName = new AssemblyName
                {
                    Name = name,
                    Version = version,
                    Flags = flags,
                    ProcessorArchitecture = processorArchitecture,
                    CultureInfo = cultureInfo,
                    HashAlgorithm = hashAlgorithm,
                    VersionCompatibility = versionCompatibility,
                    CodeBase = codeBase,
                    // AssemblyName.KeyPair is not used anywhere, additionally StrongNameKeyPair is not supported in .net core 5-
                    // and throws platform not supported exception when serialized or deserialized
                    KeyPair = null,
                };

                assemblyName.SetPublicKey(publicKey);
                assemblyName.SetPublicKeyToken(publicKeyToken);
            }
        }
    }
}
