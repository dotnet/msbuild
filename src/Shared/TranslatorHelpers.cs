using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.Build.BackEnd
{
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

        public static void Translate<T>(
            this ITranslator translator,
            ref List<T> list,
            NodePacketValueFactory<T> valueFactory) where T : class, ITranslatable
        {
            void objectTranslator(ITranslator t2, ref T objectToTranslate)
            {
                if (!t2.TranslateNullable(objectToTranslate))
                {
                    return;
                }
                if (t2.Mode == TranslationDirection.ReadFromStream)
                {
                    objectToTranslate = valueFactory(t2);
                }
                else
                {
                    objectToTranslate.Translate(t2);
                }
            }

            translator.Translate(ref list, objectTranslator);
        }

        public static void Translate<T, L>(
            this ITranslator translator,
            ref IList<T> list,
            NodePacketValueFactory<T> valueFactory,
            NodePacketCollectionCreator<L> collectionFactory) where L : IList<T> where T : ITranslatable
        {
            void objectTranslator(ITranslator t2, ref T objectToTranslate)
            {
                if (!t2.TranslateNullable(objectToTranslate))
                {
                    return;
                }

                if (t2.Mode == TranslationDirection.ReadFromStream)
                {
                    objectToTranslate = valueFactory(t2);
                }
                else
                {
                    objectToTranslate.Translate(t2);
                }
            }

            translator.Translate(ref list, objectTranslator, collectionFactory);
        }

        public static void TranslateArray<T>(
            this ITranslator translator,
            ref T[] array,
            NodePacketValueFactory<T> valueFactory) where T : class, ITranslatable
        {
            void objectTranslator(ITranslator t2, ref T objectToTranslate)
            {
                if (!t2.TranslateNullable(objectToTranslate))
                {
                    return;
                }

                if (t2.Mode == TranslationDirection.ReadFromStream)
                {
                    objectToTranslate = valueFactory(t2);
                }
                else
                {
                    objectToTranslate.Translate(t2);
                }
            }

            translator.TranslateArray(ref array, objectTranslator);
        }

        public static void TranslateDictionary<T>(
            this ITranslator translator,
            ref Dictionary<string, T> dictionary,
            IEqualityComparer<string> comparer,
            NodePacketValueFactory<T> valueFactory) where T : class, ITranslatable
        {
            void objectTranslator(ITranslator t2, ref T objectToTranslate)
            {
                if (!t2.TranslateNullable(objectToTranslate))
                {
                    return;
                }

                if (t2.Mode == TranslationDirection.ReadFromStream)
                {
                    objectToTranslate = valueFactory(t2);
                }
                else
                {
                    objectToTranslate.Translate(t2);
                }
            }

            translator.TranslateDictionary(ref dictionary, comparer, objectTranslator);
        }

        public static void TranslateDictionary<D, T>(
            this ITranslator translator,
            ref D dictionary,
            NodePacketValueFactory<T> valueFactory)
            where D : IDictionary<string, T>, new()
            where T : class, ITranslatable
        {
            void objectTranslator(ITranslator t2, ref T objectToTranslate)
            {
                if (!t2.TranslateNullable(objectToTranslate))
                {
                    return;
                }

                if (t2.Mode == TranslationDirection.ReadFromStream)
                {
                    objectToTranslate = valueFactory(t2);
                }
                else
                {
                    objectToTranslate.Translate(t2);
                }
            }

            translator.TranslateDictionary(ref dictionary, (ObjectTranslator<T>) objectTranslator);
        }

        public static void TranslateDictionary<D, T>(
            this ITranslator translator,
            ref D dictionary,
            NodePacketValueFactory<T> valueFactory,
            NodePacketCollectionCreator<D> collectionCreator)
            where D : IDictionary<string, T>
            where T : class, ITranslatable
        {
            void objectTranslator(ITranslator t2, ref T objectToTranslate)
            {
                if (!t2.TranslateNullable(objectToTranslate))
                {
                    return;
                }

                if (t2.Mode == TranslationDirection.ReadFromStream)
                {
                    objectToTranslate = valueFactory(t2);
                }
                else
                {
                    objectToTranslate.Translate(t2);
                }
            }

            translator.TranslateDictionary(ref dictionary, (ObjectTranslator<T>) objectTranslator, collectionCreator);
        }


        public static void TranslatableTranslator<T>(ITranslator t, ref T objectToTranslate) where T : class, ITranslatable, new()
        {
            objectToTranslate = null;
            if (t.Mode == TranslationDirection.ReadFromStream)
            {
                objectToTranslate = new T();
            }
            objectToTranslate.Translate(t);
        }

        public static void FactoryAdapter<T>(ITranslator t, ref T objectToTranslate, Func<ITranslator, T> factoryForDeserialization)
            where T : ITranslatable
        {
            if (t.Mode == TranslationDirection.ReadFromStream)
            {
                objectToTranslate = factoryForDeserialization(t);
            }
            else
            {
                objectToTranslate.Translate(t);
            }
        }
    }
}
