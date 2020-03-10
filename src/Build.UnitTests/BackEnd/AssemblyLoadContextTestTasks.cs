// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Collections;
using System.Collections.Generic;

namespace AssemblyLoadContextTest
{
    public class RegisterObject : Task
    {
        internal const string CacheKey = "RegressionForMSBuild#5080";

        public override bool Execute()
        {
            BuildEngine4.RegisterTaskObject(
                  CacheKey,
                  new RegisterObject(),
                  RegisteredTaskObjectLifetime.Build,
                  allowEarlyCollection: false);

            return true;
        }
    }

    public class RetrieveObject : Task
    {
        public override bool Execute()
        {
            var entry = (RegisterObject)BuildEngine4.GetRegisteredTaskObject(RegisterObject.CacheKey, RegisteredTaskObjectLifetime.Build);

            return true;
        }
    }
}
