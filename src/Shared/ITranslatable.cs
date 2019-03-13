// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Delegate for users that want to translate an arbitrary structure that cannot implement <see cref="ITranslatable"/> (e.g. translating a complex collection)
    /// </summary>
    /// <param name="translator">the translator</param>
    /// <param name="obj">the object to translate</param>
    internal delegate void Translator<T>(ref T obj, ITranslator translator);

    /// <summary>
    /// An interface representing an object which may be serialized by the node packet serializer.
    /// </summary>
    internal interface ITranslatable
    {
        /// <summary>
        /// Reads or writes the packet to the serializer.
        /// </summary>
        void Translate(ITranslator translator);
    }
}
