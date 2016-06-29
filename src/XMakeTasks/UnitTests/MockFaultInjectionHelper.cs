// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Build.UnitTests
{
    public class MockFaultInjectionHelper<FailurePointEnum>
        where FailurePointEnum : IComparable
    {
        private FailurePointEnum _failureToInject;
        private Exception _exceptionToThrow;

        public MockFaultInjectionHelper()
        {
        }

        public void InjectFailure(FailurePointEnum failureToInject, Exception exceptionToThrow)
        {
            _failureToInject = failureToInject;
            _exceptionToThrow = exceptionToThrow;
        }

        public void FailurePointThrow(FailurePointEnum failurePointId)
        {
            if (_failureToInject.CompareTo(failurePointId) == 0)
            {
                throw _exceptionToThrow;
            }
        }
    }
}
