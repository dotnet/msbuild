// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !TASKHOST && !NETSTANDARD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using Microsoft.Build.BackEnd;

#nullable disable

namespace Microsoft.Build.BackEnd
{
    internal interface IJsonTranslator : ITranslatorBase, IDisposable
    {
        /// <summary>
        /// Returns the current serialization mode.
        /// </summary>
        TranslationDirection Mode { get; }

        void TranslateToJson<T>(T model, JsonSerializerOptions jsonSerializerOptions = null);

        T TranslateFromJson<T>(JsonSerializerOptions jsonSerializerOptions = null);

        // Additional methods for specific type handling if needed
        void TranslateCulture(string propertyName, ref CultureInfo culture);

        void TranslateDictionary<TKey, TValue>(
            JsonSerializerOptions jsonSerializerOptions,
            string propertyName,
            ref Dictionary<TKey, TValue> dictionary,
            IEqualityComparer<TKey> comparer,
            Func<TValue> valueFactory = null);
    }
}
#endif
