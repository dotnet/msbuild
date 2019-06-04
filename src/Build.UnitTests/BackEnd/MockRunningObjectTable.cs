// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Build.Execution;

namespace Microsoft.Build.UnitTests.BackEnd
{
    internal class MockRunningObjectTable : IRunningObjectTableWrapper
    {
        private readonly Dictionary<string, object> _dictionary = new Dictionary<string, object>();

        public void Dispose()
        {
        }

        public object GetObject(string itemName)
        {
            if (_dictionary.TryGetValue(itemName, out var obj))
            {
                return obj;
            }

            throw new COMException(
                "Operation unavailable(Exception from HRESULT: 0x800401E3(MK_E_UNAVAILABLE))");
        }

        public IDisposable Register(string itemName, object obj)
        {
            _dictionary.Add(itemName, obj);
            return new MockRegisterHandle();
        }

        private class MockRegisterHandle : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
