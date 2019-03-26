// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;

namespace Microsoft.Build.UnitTests.BackEnd
{
    internal class MockRemoteHostObject : ITaskHost, ITestRemoteHostObject
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
