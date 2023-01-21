// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks.Deployment.ManifestUtilities;
using Microsoft.Build.Utilities;

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// This task signs the passed in file using the Authenticode cert
    /// provided and optionally uses a timestamp if a URL is provided.
    /// It can sign ClickOnce manifests as well as exe's.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public sealed class SignFile : Task
    {
        public SignFile()
            : base(AssemblyResources.PrimaryResources, "MSBuild.")
        {
        }

        [Required]
        public string CertificateThumbprint { get; set; }

        [Required]
        public ITaskItem SigningTarget { get; set; }

        public string TargetFrameworkIdentifier { get; set; } = Constants.DotNetFrameworkIdentifier;

        public String TargetFrameworkVersion { get; set; }

        public string TimestampUrl { get; set; }
        public bool DisallowMansignTimestampFallback { get; set; } = false;

        public override bool Execute()
        {
            if (!NativeMethodsShared.IsWindows)
            {
                Log.LogErrorWithCodeFromResources("General.TaskRequiresWindows", nameof(SignFile));
                return false;
            }
            try
            {
                SecurityUtilities.SignFile(
                    CertificateThumbprint,
                    TimestampUrl == null ? null : new Uri(TimestampUrl),
                    SigningTarget.ItemSpec,
                    TargetFrameworkVersion,
                    TargetFrameworkIdentifier,
                    DisallowMansignTimestampFallback);
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
