// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Utilities;

namespace Microsoft.NET.Sdk.Publish.Tasks.Kudu
{
    public abstract class KuduConnect
    {
        private KuduConnectionInfo _connectionInfo;
        private object _syncObject = new();
        internal KuduConnect(KuduConnectionInfo connectionInfo, TaskLoggingHelper logger)
        {
            _connectionInfo = connectionInfo;
        }

        public abstract string DestinationUrl
        {
            get;
        }

        protected KuduConnectionInfo ConnectionInfo
        {
            get
            {
                return _connectionInfo;
            }
        }

        protected string AuthorizationInfo
        {
            get
            {
                lock (_syncObject)
                {
                    string authInfo = string.Format("{0}:{1}", _connectionInfo.UserName, _connectionInfo.Password);
                    return Convert.ToBase64String(Encoding.UTF8.GetBytes(authInfo));
                }
            }
        }
    }
}
