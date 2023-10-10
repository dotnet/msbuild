// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.Build.Framework;

namespace Microsoft.NET.Sdk.Publish.Tasks.MsDeploy
{
    ///-----------------------------------------------------------------------------
    /// <summary>
    /// Covnert server name to foramt like "https://<server>:8172/msdeploy.axd"
    /// Code ported (in a combining manner) from:
    ///     IISOOB\projects\Wdeploy\released\VS_RI4\code\MSDeploy\WDeploy.cs
    ///     IISOOB\projects\ui\wm\Deployment\Data\Profiles\PublishProfile.cs
    /// So VS's behavior conforms to webmatrix and wdeploy as much as possible
    /// </summary>
    ///-----------------------------------------------------------------------------
    public sealed class NormalizeServiceUrl : Task
    {
        private string _serviceUrl = string.Empty;
        private string _resultUrl = string.Empty;
        private string _siteName = string.Empty;
        private bool _useWMSVC = false;
        private bool _useRemoteAgent = false;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1056:UriPropertiesShouldNotBeStrings", Justification = "This is interface with the Msbuild method, all argument is basically pass by string")]
        [Required]
        public string ServiceUrl
        {
            get { return _serviceUrl; }
            set { _serviceUrl = value; }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "WMSVC", Justification = "Special term that used by MSDeploy team for remote service")]
        [Required]
        public bool UseWMSVC
        {
            get { return _useWMSVC; }
            set { _useWMSVC = value; }
        }

        [Required]
        public bool UseRemoteAgent
        {
            get { return _useRemoteAgent; }
            set { _useRemoteAgent = value; }
        }

        [Required]
        public string SiteName
        {
            get { return _siteName; }
            set { _siteName = value; }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1056:UriPropertiesShouldNotBeStrings", Justification = "This is interface with the Msbuild method, all argument is basically pass by string")]
        [Output]
        public string ResultUrl
        {
            get { return _resultUrl; }
        }

        public override bool Execute()
        {
            string tempUrl = _serviceUrl;
            if (!string.IsNullOrEmpty(tempUrl))
            {
                tempUrl = tempUrl.Trim();
                if (_useWMSVC)
                {
                    _resultUrl = ConstructServiceUrlForDeployThruWMSVC(tempUrl);
                }
                else
                {
                    if (_useRemoteAgent)
                    {
                        _resultUrl = ConstructServiceUrlForDeployThruAgentService(tempUrl);//through remote agent
                    }
                    else
                    {
                        if (string.Compare(tempUrl, "localhost", StringComparison.OrdinalIgnoreCase) == 0 ||
                            string.Compare(tempUrl, "http://localhost", StringComparison.OrdinalIgnoreCase) == 0)
                        {
                            _resultUrl = string.Empty;//through in proc and don't need server name at all.
                        }
                        else
                        {
                            if (tempUrl.StartsWith("localhost:", StringComparison.OrdinalIgnoreCase))
                            {
                                _resultUrl = string.Concat("http://", tempUrl);
                            }
                            else if (tempUrl.StartsWith("http://localhost:", StringComparison.OrdinalIgnoreCase))
                            {
                                _resultUrl = tempUrl;
                            }
                        }
                    }
                }
                return true;
            }
            else
                return false;
        }

        //ported from IISOOB\projects\ui\wm\Deployment\Data\Profiles\PublishProfile.cs
        private string ConstructServiceUrlForDeployThruWMSVC(string serviceUrl)
        {
            const string https = "https://";
            const string http = "http://";
            const string msddepaxd = "msdeploy.axd";

            System.UriBuilder serviceUriBuilder = null;

            // We want to try adding https:// if there is no schema. However abc:123 is parsed as a schema=abc and path=123
            // so the goal is to isolate this case and add the https:// but allow for http if the user chooses to
            // since we do not allow for any schema other than http or https, it's safe to assume we can add it if none exist
            try
            {
                if (!(serviceUrl.StartsWith(http, StringComparison.OrdinalIgnoreCase) || serviceUrl.StartsWith(https, StringComparison.OrdinalIgnoreCase)))
                {
                    serviceUrl = string.Concat(https, serviceUrl.TrimStart());
                }

                serviceUriBuilder = new UriBuilder(serviceUrl);
            }
            catch (NullReferenceException)
            {
                return string.Empty;
            }
            catch (ArgumentNullException)
            {
                return string.Empty;
            }
            catch (UriFormatException)
            {
                return serviceUrl;
            }

            // if the user did not explicitly defined a port
            if (serviceUrl.IndexOf(":" + serviceUriBuilder.Port.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase) == -1)
            {
                serviceUriBuilder.Port = 8172;
            }

            // user did not explicitly set a path
            if (string.IsNullOrEmpty(serviceUriBuilder.Path) || serviceUriBuilder.Path.Equals("/", StringComparison.OrdinalIgnoreCase))
            {
                serviceUriBuilder.Path = msddepaxd;
            }

            // user did not explicityly set the scheme
            if (serviceUrl.IndexOf(serviceUriBuilder.Scheme, StringComparison.OrdinalIgnoreCase) == -1)
            {
                serviceUriBuilder.Scheme = https;
            }

            if (string.IsNullOrEmpty(serviceUriBuilder.Query))
            {
                string[] fragments = SiteName.Trim().Split(new char[] { '/', '\\' });
                serviceUriBuilder.Query = "site=" + fragments[0];
            }

            return serviceUriBuilder.Uri.AbsoluteUri;
        }

        private string ConstructServiceUrlForDeployThruAgentService(string serviceUrl)
        {
            System.Text.StringBuilder url = new("http://");
            int iSpot = 0;
            // It needs to start with http:// 
            // It needs to then have the computer name
            // It should then be "/MSDEPLOYAGENTSERVICE" 
            if (serviceUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                iSpot = "http://".Length;
            }
            url.Append(serviceUrl.Substring(iSpot));

            int msdepSpot = serviceUrl.IndexOf("/MSDEPLOYAGENTSERVICE", StringComparison.OrdinalIgnoreCase);
            if (msdepSpot < 0)
            {
                url.Append("/MSDEPLOYAGENTSERVICE");
            }

            return url.ToString();
        }
    }
}
