// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

#nullable disable

namespace Microsoft.Build.BackEnd.SdkResolution
{
    /// <summary>
    /// A factory used when creating a <see cref="OutOfProcNodeSdkResolverService"/> which can pass parameters to its constructor.  Our
    /// dependency inject cannot pass parameters to constructors so this factory is used as a middle man.
    /// </summary>
    internal sealed class OutOfProcNodeSdkResolverServiceFactory
    {
        /// <summary>
        /// Stores the SendPacket delegate to use.
        /// </summary>
        private readonly Action<INodePacket> _sendPacket;

        public OutOfProcNodeSdkResolverServiceFactory(Action<INodePacket> sendPacket)
        {
            _sendPacket = sendPacket;
        }

        public IBuildComponent CreateInstance(BuildComponentType type)
        {
            // Create the instance of OutOfProcNodeSdkResolverService and pass parameters to the constructor.
            return new OutOfProcNodeSdkResolverService(_sendPacket);
        }
    }
}
