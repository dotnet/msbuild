// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.IO;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Framework
{
    public class AssemblyLoadBuildEventArgs : BuildMessageEventArgs
    {
        public AssemblyLoadBuildEventArgs()
        { }

        public AssemblyLoadBuildEventArgs(
            AssemblyLoadingContext loadingContext,
            string assemblyName,
            string assemblyPath,
            Guid mvid,
            int appDomainId,
            string appDomainFriendlyName,
            MessageImportance importance = MessageImportance.Low)
            : base(null, null, null, importance, DateTime.UtcNow, assemblyName, assemblyPath, mvid)
        {
            LoadingContext = loadingContext;
            AssemblyName = assemblyName;
            AssemblyPath = assemblyPath;
            MVID = mvid;
            AppDomainId = appDomainId;
            AppDomainFriendlyName = appDomainFriendlyName;
        }

        public AssemblyLoadingContext LoadingContext { get; private set; }
        public string AssemblyName { get; private set; }
        public string AssemblyPath { get; private set; }
        public Guid MVID { get; private set; }
        public int AppDomainId { get; private set; }
        public string AppDomainFriendlyName { get; private set; }

        internal override void WriteToStream(BinaryWriter writer)
        {
            writer.Write7BitEncodedInt((int)LoadingContext);
            writer.WriteTimestamp(RawTimestamp);
            writer.WriteOptionalBuildEventContext(BuildEventContext);
            writer.WriteGuid(MVID);
            writer.WriteOptionalString(AssemblyName);
            writer.WriteOptionalString(AssemblyPath);
            writer.Write7BitEncodedInt(AppDomainId);
            writer.WriteOptionalString(AppDomainFriendlyName);
        }

        internal override void CreateFromStream(BinaryReader reader, int version)
        {
            LoadingContext = (AssemblyLoadingContext)reader.Read7BitEncodedInt();
            RawTimestamp = reader.ReadTimestamp();
            BuildEventContext = reader.ReadOptionalBuildEventContext();
            MVID = reader.ReadGuid();
            AssemblyName = reader.ReadOptionalString();
            AssemblyPath = reader.ReadOptionalString();
            AppDomainId = reader.Read7BitEncodedInt();
            AppDomainFriendlyName = reader.ReadOptionalString();
        }

        public override string Message
        {
            get
            {
                if (RawMessage == null)
                {
                    RawMessage = FormatResourceStringIgnoreCodeAndKeyword("TaskAssemblyLoaded", LoadingContext.ToString(), AssemblyName, AssemblyPath, MVID.ToString(), AppDomainId.ToString(), AppDomainFriendlyName);
                }

                return RawMessage;
            }
        }
    }
}
