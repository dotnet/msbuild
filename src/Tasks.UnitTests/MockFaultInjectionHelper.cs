// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

#nullable disable

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
