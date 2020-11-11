// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Build.UnitTests
{
    internal class ExpectedExceptionAttribute : Attribute
    {
        public ExpectedExceptionAttribute(Type expectedException)
        {
            throw new NotImplementedException();
        }
    }
}
