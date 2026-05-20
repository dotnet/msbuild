// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Coordinator;
using Microsoft.Build.Internal;

namespace Microsoft.Build.BackEnd;

internal sealed partial class CoordinatorClient
{
    private sealed class DefaultOutput : ICoordinatorOutput
    {
        public static readonly DefaultOutput Instance = new();

        private DefaultOutput()
        {
        }

        public bool IsEnabled => Traits.Instance.DebugNodeCommunication;

        public void WriteLine(string message)
        {
            if (IsEnabled)
            {
                CommunicationsUtilities.Trace(message);
            }
        }

        public void WriteLine([InterpolatedStringHandlerArgument("")] ref ICoordinatorOutput.WriteLineInterpolatedStringHandler handler)
        {
            if (IsEnabled)
            {
                CommunicationsUtilities.Trace(handler.GetFormattedText());
            }
        }
    }
}
