// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.IO;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Framework
{
    public sealed class AssemblyLoadBuildEventArgs : BuildMessageEventArgs
    {
        private const string DefaultAppDomainDescriptor = "[Default]";

        public AssemblyLoadBuildEventArgs()
        { }

        public AssemblyLoadBuildEventArgs(
            AssemblyLoadingContext loadingContext,
            string? loadingInitiator,
            string? assemblyName,
            string? assemblyPath,
            Guid mvid,
            string? customAppDomainDescriptor,
            MessageImportance importance = MessageImportance.Low)
            : base(null, null, null, importance, DateTime.UtcNow, null)
        {
            LoadingContext = loadingContext;
            LoadingInitiator = loadingInitiator;
            AssemblyName = assemblyName;
            AssemblyPath = assemblyPath;
            MVID = mvid;
            AppDomainDescriptor = customAppDomainDescriptor;
        }

        public AssemblyLoadingContext LoadingContext { get; private set; }
        public string? LoadingInitiator { get; private set; }
        public string? AssemblyName { get; private set; }
        public string? AssemblyPath { get; private set; }
        public Guid MVID { get; private set; }
        // Null string indicates that load occurred on Default AppDomain (for both Core and Framework).
        public string? AppDomainDescriptor { get; private set; }

        internal override void WriteToStream(BinaryWriter writer)
        {
            base.WriteToStream(writer);

            writer.Write7BitEncodedInt((int)LoadingContext);
            writer.WriteTimestamp(RawTimestamp);
            writer.WriteOptionalBuildEventContext(BuildEventContext);
            writer.WriteGuid(MVID);
            writer.WriteOptionalString(LoadingInitiator);
            writer.WriteOptionalString(AssemblyName);
            writer.WriteOptionalString(AssemblyPath);
            writer.WriteOptionalString(AppDomainDescriptor);
        }

        internal override void CreateFromStream(BinaryReader reader, int version)
        {
            base.CreateFromStream(reader, version);

            LoadingContext = (AssemblyLoadingContext)reader.Read7BitEncodedInt();
            RawTimestamp = reader.ReadTimestamp();
            BuildEventContext = reader.ReadOptionalBuildEventContext();
            MVID = reader.ReadGuid();
            LoadingInitiator = reader.ReadOptionalString();
            AssemblyName = reader.ReadOptionalString();
            AssemblyPath = reader.ReadOptionalString();
            AppDomainDescriptor = reader.ReadOptionalString();
        }

        public override string Message
        {
            get
            {
                if (RawMessage == null)
                {
                    string? loadingInitiator = LoadingInitiator == null ? null : $" ({LoadingInitiator})";
                    RawMessage = FormatResourceStringIgnoreCodeAndKeyword("TaskAssemblyLoaded", LoadingContext.ToString(), loadingInitiator, AssemblyName, AssemblyPath, MVID.ToString(), AppDomainDescriptor ?? DefaultAppDomainDescriptor);
                }

                return RawMessage;
            }
        }
    }
}
