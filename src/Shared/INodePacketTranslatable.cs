// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Interface for objects which can be serialized to packets for inter-node communication.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// An interface representing an object which may be serialized by the node packet serializer.
    /// </summary>
    internal interface INodePacketTranslatable
    {
        #region Methods

        /// <summary>
        /// Reads or writes the packet to the serializer.
        /// </summary>
        void Translate(INodePacketTranslator translator);

        #endregion
    }
}
