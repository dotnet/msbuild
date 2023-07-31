// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Containers.Tasks;

internal sealed class VSHostObject
{
    private const string CredentialItemSpecName = "MsDeployCredential";
    private const string UserMetaDataName = "UserName";
    private const string PasswordMetaDataName = "Password";
    IEnumerable<ITaskItem>? _hostObject;

    public VSHostObject(IEnumerable<ITaskItem>? hostObject)
    {
        _hostObject = hostObject;
    }

    public bool ExtractCredentials(out string username, out string password, Action<string> logMethod)
    {
        bool retVal = false;
        username = password = string.Empty;
        if (_hostObject != null)
        {
            ITaskItem credentialItem = _hostObject.FirstOrDefault<ITaskItem>(p => p.ItemSpec == CredentialItemSpecName);
            if (credentialItem != null)
            {
                retVal = true;
                username = credentialItem.GetMetadata(UserMetaDataName);
                if (!string.IsNullOrEmpty(username))
                {
                    password = credentialItem.GetMetadata(PasswordMetaDataName);
                }
                else
                {
                    logMethod("HostObject credentials not detected. Falling back to Docker credential retrieval.");
                }
            }
        }
        return retVal;
    }
}

