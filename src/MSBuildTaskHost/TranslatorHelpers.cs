// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Configuration.Assemblies;
using System.Globalization;
using System.Reflection;
using AssemblyHashAlgorithm = System.Configuration.Assemblies.AssemblyHashAlgorithm;

#nullable disable

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

        private static ObjectTranslatorWithValueFactory<T> AdaptFactory<T>(NodePacketValueFactory<T> valueFactory) where T : ITranslatable
        {
            static void TranslateUsingValueFactory(ITranslator translator, NodePacketValueFactory<T> valueFactory, ref T objectToTranslate)
            {
                translator.Translate(ref objectToTranslate, valueFactory);
            }

            return TranslateUsingValueFactory;
        }

        public static void TranslateDictionary<T>(
            this ITranslator translator,
            ref Dictionary<string, T> dictionary,
            IEqualityComparer<string> comparer,
            NodePacketValueFactory<T> valueFactory) where T : class, ITranslatable
        {
            translator.TranslateDictionary(ref dictionary, comparer, AdaptFactory(valueFactory), valueFactory);
        }
    }
}
