// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

#nullable disable

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
