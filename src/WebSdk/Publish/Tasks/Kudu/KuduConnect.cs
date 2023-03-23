using Microsoft.Build.Utilities;
using System;
using System.Text;

namespace Microsoft.NET.Sdk.Publish.Tasks.Kudu
{
    public abstract class KuduConnect
    {
        private KuduConnectionInfo _connectionInfo;
        private object _syncObject = new object();
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
                    string authInfo = String.Format("{0}:{1}", _connectionInfo.UserName, _connectionInfo.Password);
                    return Convert.ToBase64String(Encoding.UTF8.GetBytes(authInfo));
                }
            }
        } 
    }
}
