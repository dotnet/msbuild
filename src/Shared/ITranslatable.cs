// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.BackEnd
{
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
