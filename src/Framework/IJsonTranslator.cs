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

        void Translate<T>(ref T model, JsonSerializerOptions jsonSerializerOptions = null);
    }
}
#endif
