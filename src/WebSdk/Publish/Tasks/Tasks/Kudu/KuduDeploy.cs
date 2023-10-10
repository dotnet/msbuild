// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

///--------------------------------------------------------------------------------------------
/// KuduDeploy.cs
///
/// Support for WAWS deployment using Kudu API.
///
/// Copyright(c) 2006 Microsoft Corporation
///--------------------------------------------------------------------------------------------

using Microsoft.NET.Sdk.Publish.Tasks.Properties;
using Framework = Microsoft.Build.Framework;
using Utilities = Microsoft.Build.Utilities;

namespace Microsoft.NET.Sdk.Publish.Tasks.Kudu
{
    public sealed class KuduDeploy : Utilities.Task
    {
        public static readonly int TimeoutMilliseconds = 180000;
        [Framework.Required]
        public string PublishIntermediateOutputPath
        {
            get;
            set;
        }

        [Framework.Required]
        public string PublishUrl
        {
            get;
            set;
        }

        [Framework.Required]
        public string UserName
        {
            get;
            set;
        }

        [Framework.Required]
        public string Password
        {
            get;
            set;
        }

        [Framework.Required]
        public string PublishSiteName
        {
            get;
            set;
        }

        public bool DeployIndividualFiles
        {
            get;
            set;
        }

        internal KuduConnectionInfo GetConnectionInfo()
        {
            KuduConnectionInfo connectionInfo = new()
            {
                DestinationUrl = PublishUrl,
                UserName = UserName,
                Password = Password,
                SiteName = PublishSiteName
            };

            return connectionInfo;
        }

        public override bool Execute()
        {
            if (string.IsNullOrEmpty(PublishIntermediateOutputPath))
            {
                Log.LogError(Resources.KUDUDEPLOY_DeployOutputPathEmpty);
                return false;
            }

            KuduConnectionInfo connectionInfo = GetConnectionInfo();
            if (string.IsNullOrEmpty(connectionInfo.UserName) || string.IsNullOrEmpty(connectionInfo.Password) || string.IsNullOrEmpty(connectionInfo.DestinationUrl))
            {
                Log.LogError(Resources.KUDUDEPLOY_ConnectionInfoMissing);
                return false;
            }

            bool publishStatus = DeployFiles(connectionInfo);

            if (publishStatus)
            {
                Log.LogMessage(Framework.MessageImportance.High, Resources.KUDUDEPLOY_PublishSucceeded);
            }
            else
            {
                Log.LogError(Resources.KUDUDEPLOY_PublishFailed);
            }

            return publishStatus;
        }

        internal bool DeployFiles(KuduConnectionInfo connectionInfo)
        {
            KuduVfsDeploy fileDeploy = new(connectionInfo, Log);

            bool success;
            if (!DeployIndividualFiles)
            {
                success = DeployZipFile(connectionInfo);
                return success;
            }

            // Deploy the files.
            System.Threading.Tasks.Task deployTask = fileDeploy.DeployAsync(PublishIntermediateOutputPath);
            try
            {
                success = deployTask.Wait(TimeoutMilliseconds);
                if (!success)
                {
                    Log.LogError(string.Format(Resources.KUDUDEPLOY_AzurePublishErrorReason, Resources.KUDUDEPLOY_OperationTimeout));
                }
            }
            catch (AggregateException ae)
            {
                Log.LogError(string.Format(Resources.KUDUDEPLOY_AzurePublishErrorReason, ae.Flatten().Message));
                success = false;
            }

            return success;
        }


        #region Zip File Publish
        internal bool DeployZipFile(KuduConnectionInfo connectionInfo)
        {
            bool success;
            KuduZipDeploy zipDeploy = new(connectionInfo, Log);

            string zipFileFullPath = CreateZipFile(PublishIntermediateOutputPath);
            System.Threading.Tasks.Task<bool> zipTask = zipDeploy.DeployAsync(zipFileFullPath);
            try
            {
                success = zipTask.Wait(TimeoutMilliseconds);
                if (!success)
                {
                    Log.LogError(string.Format(Resources.KUDUDEPLOY_AzurePublishErrorReason, Resources.KUDUDEPLOY_OperationTimeout));
                }
            }
            catch (AggregateException ae)
            {
                Log.LogError(string.Format(Resources.KUDUDEPLOY_AzurePublishErrorReason, ae.Flatten().Message));
                success = false;
            }

            // Clean up the resources.
            DeleteTempZipFile(zipFileFullPath);

            return success && zipTask.Result;
        }

        internal string CreateZipFile(string sourcePath)
        {
            // Zip the files from PublishOutput path.
            string zipFileFullPath = Path.Combine(Path.GetTempPath(), string.Format("Publish{0}.zip", new Random().Next(int.MaxValue)));
            Log.LogMessage(Framework.MessageImportance.High, string.Format(Resources.KUDUDEPLOY_CopyingToTempLocation, zipFileFullPath));

            try
            {
                System.IO.Compression.ZipFile.CreateFromDirectory(sourcePath, zipFileFullPath);
            }
            catch (Exception e)
            {
                Log.LogError(string.Format(Resources.KUDUDEPLOY_AzurePublishErrorReason, e.Message));
                // If we are unable to zip the file, then we fail.
                return null;
            }

            Log.LogMessage(Framework.MessageImportance.High, Resources.KUDUDEPLOY_CopyingToTempLocationCompleted);

            return zipFileFullPath;
        }

        internal System.Threading.Tasks.Task DeleteTempZipFile(string tempFilePath)
        {
            return System.Threading.Tasks.Task.Factory.StartNew(
               () =>
               {
                   if (File.Exists(tempFilePath))
                   {
                       try
                       {
                           File.Delete(tempFilePath);
                       }
                       catch
                       {
                           // We don't need to do any thing if we are unable to delete the temp file.
                       }
                   }
               });
        }

        #endregion
    }
}
