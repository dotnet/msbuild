// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Build.Collections;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// This class is responsible for serializing and deserializing anything that is not
    /// officially supported by ITranslator, but that we still want to do
    /// custom translation of.
    /// </summary>
    internal static class TranslatorExtensions
    {
        private static readonly Lazy<ConcurrentDictionary<Type, ConstructorInfo>> parameterlessConstructorCache = new Lazy<ConcurrentDictionary<Type, ConstructorInfo>>(() => new ConcurrentDictionary<Type, ConstructorInfo>());

        /// <summary>
        /// Translates a PropertyDictionary of ProjectPropertyInstances.
        /// </summary>
        /// <param name="translator">The tranlator doing the translating</param>
        /// <param name="value">The dictionary to translate.</param>
        public static void TranslateProjectPropertyInstanceDictionary(this ITranslator translator, ref PropertyDictionary<ProjectPropertyInstance> value)
        {
            if (!translator.TranslateNullable(value))
            {
                return;
            }

            if (translator.Mode == TranslationDirection.ReadFromStream)
            {
                int count = 0;
                translator.Translate(ref count);

                value = new PropertyDictionary<ProjectPropertyInstance>(count);
                for (int i = 0; i < count; i++)
                {
                    ProjectPropertyInstance instance = null;
                    translator.Translate(ref instance, ProjectPropertyInstance.FactoryForDeserialization);
                    value[instance.Name] = instance;
                }
            }
            else // TranslationDirection.WriteToStream
            {
                int count = value.Count;
                translator.Translate(ref count);

                foreach (ProjectPropertyInstance instance in value)
                {
                    ProjectPropertyInstance instanceForSerialization = instance;
                    translator.Translate(ref instanceForSerialization, ProjectPropertyInstance.FactoryForDeserialization);
                }
            }
        }

        /// <summary>
        /// Deserialize a type or a subtype by its full name. The type must implement ITranslateable
        /// </summary>
        /// <typeparam name="T">Top level type. Serialized types can be of this type, or subtypes</typeparam>
        /// <returns></returns>
        public static T FactoryForDeserializingTypeWithName<T>(this ITranslator translator)
        {
            string typeName = null;
            translator.Translate(ref typeName);

            var type = Type.GetType(typeName);
            ErrorUtilities.VerifyThrow(type != null, "type cannot be null");
            ErrorUtilities.VerifyThrow(
                typeof(T).IsAssignableFrom(type),
                $"{typeName} must be a {typeof(T).FullName}");
            ErrorUtilities.VerifyThrow(
                typeof(ITranslatable).IsAssignableFrom(type),
                $"{typeName} must be a {nameof(ITranslatable)}");

            var parameterlessConstructor = parameterlessConstructorCache.Value.GetOrAdd(
                type,
                t =>
                {
                    ConstructorInfo constructor = null;
                    constructor = type.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                    ErrorUtilities.VerifyThrow(
                        constructor != null,
                        "{0} must have a private parameterless constructor", typeName);
                    return constructor;
                });

            var targetInstanceChild = (ITranslatable)parameterlessConstructor.Invoke(Array.Empty<object>());

            targetInstanceChild.Translate(translator);

            return (T)targetInstanceChild;
        }

        public static void TranslateOptionalBuildEventContext(this ITranslator translator, ref BuildEventContext buildEventContext)
        {
            if (translator.Mode == TranslationDirection.ReadFromStream)
            {
                buildEventContext = translator.Reader.ReadOptionalBuildEventContext();
            }
            else
            {
                translator.Writer.WriteOptionalBuildEventContext(buildEventContext);
            }
        }
    }
}
