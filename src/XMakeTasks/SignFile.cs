// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#region Using directives

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Build.Tasks.Deployment.ManifestUtilities;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using System.ComponentModel;


#endregion

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// This task signs the passed in file using the Authenticode cert
    /// provided and optionally uses a timestamp if a URL is provided.
    /// It can sign ClickOnce manifests as well as exe's.
    /// </summary>
    public sealed class SignFile : Task
    {
        public SignFile()
            : base(AssemblyResources.PrimaryResources, "MSBuild.")
        {
        }

        private string _certificateThumbprint;
        private ITaskItem _sigingTarget;
        private string _targetFrameVersion;
        private string _timestampUrl;

        [Required()]
        public string CertificateThumbprint
        {
            get { return _certificateThumbprint; }
            set { _certificateThumbprint = value; }
        }

        [Required()]
        public ITaskItem SigningTarget
        {
            get { return _sigingTarget; }
            set { _sigingTarget = value; }
        }

        public String TargetFrameworkVersion
        {
            get { return _targetFrameVersion; }
            set { _targetFrameVersion = value; }
        }

        public string TimestampUrl
        {
            get { return _timestampUrl; }
            set { _timestampUrl = value; }
        }

        public override bool Execute()
        {
            try
            {
                SecurityUtilities.SignFile(CertificateThumbprint,
                TimestampUrl == null ? null : new Uri(TimestampUrl),
                SigningTarget.ItemSpec, TargetFrameworkVersion);
                return true;
            }
            catch (ArgumentException ex) when (ex.ParamName.Equals("certThumbprint"))
            {
                Log.LogErrorWithCodeFromResources("SignFile.CertNotInStore");
                return false;
            }
            catch (FileNotFoundException ex)
            {
                Log.LogErrorWithCodeFromResources("SignFile.TargetFileNotFound", ex.FileName);
                return false;
            }
            catch (ApplicationException ex)
            {
                Log.LogErrorWithCodeFromResources("SignFile.SignToolError", ex.Message.Trim());
                return false;
            }
            catch (WarningException ex)
            {
                Log.LogWarningWithCodeFromResources("SignFile.SignToolWarning", ex.Message.Trim());
                return true;
            }
            catch (CryptographicException ex)
            {
                Log.LogErrorWithCodeFromResources("SignFile.SignToolError", ex.Message.Trim());
                return false;
            }
            catch (Win32Exception ex)
            {
                Log.LogErrorWithCodeFromResources("SignFile.SignToolError", ex.Message.Trim());
                return false;
            }
            catch (UriFormatException ex)
            {
                Log.LogErrorWithCodeFromResources("SignFile.SignToolError", ex.Message.Trim());
                return false;
            }
        }
    }
}
