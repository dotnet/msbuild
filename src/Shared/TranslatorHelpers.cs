// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
        /// <param name="factory">The factory method used to instantiate values of type T.</param>
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
            void Translate(ITranslator translator, ref T objectToTranslate)
            {
                TranslatorHelpers.Translate<T>(translator, ref objectToTranslate, valueFactory);
            }

            return Translate;
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
    }
}
