// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

#nullable disable

namespace Microsoft.Build.UnitTests.BackEnd
{
    internal sealed class MockRemoteHostObject : ITaskHost, ITestRemoteHostObject
    {
        private int _state;

        public MockRemoteHostObject(int state)
        {
            _state = state;
        }

        public int GetState()
        {
            return _state;
        }
    }

    internal interface ITestRemoteHostObject
    {
        int GetState();
    }
}
