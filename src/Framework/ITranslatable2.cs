// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#if !TASKHOST && !NETSTANDARD

#nullable disable

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// An interface representing an object which may be serialized by the node packet serializer.
    /// </summary>
    internal interface ITranslatable2 : ITranslatable
    {
        /// <summary>
        /// Reads or writes the packet to the json serializer.
        /// </summary>
        void Translate(IJsonTranslator translator);
    }
}
#endif
