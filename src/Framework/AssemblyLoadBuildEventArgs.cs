// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//

#nullable disable

using System;
using System.IO;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Framework
{
    // [Serializable] TODO: this is likely not needed - custom serialization is happening
    public class AssemblyLoadBuildEventArgs : BuildMessageEventArgs // or LazyFormattedBuildEventArgs?
    {
        public AssemblyLoadBuildEventArgs()
        { }

        public AssemblyLoadBuildEventArgs(
            string assemblyName,
            string assemblyPath,
            Guid mvid,
            MessageImportance importance = MessageImportance.Low)
            : base(null, null, null, importance, DateTime.UtcNow, assemblyName, assemblyPath, mvid)
        {
            AssemblyName = assemblyName;
            AssemblyPath = assemblyPath;
            MVID = mvid;
        }

        public string AssemblyName { get; private set; }
        public string AssemblyPath { get; private set; }
        public Guid MVID { get; private set; }

        internal override void WriteToStream(BinaryWriter writer)
        {
            writer.WriteTimestamp(RawTimestamp);
            writer.WriteOptionalBuildEventContext(BuildEventContext);
            writer.WriteGuid(MVID);
            writer.WriteOptionalString(AssemblyName);
            writer.WriteOptionalString(AssemblyPath);
        }

        internal override void CreateFromStream(BinaryReader reader, int version)
        {
            RawTimestamp = reader.ReadTimestamp();
            BuildEventContext = reader.ReadOptionalBuildEventContext();
            MVID = reader.ReadGuid();
            AssemblyName = reader.ReadOptionalString();
            AssemblyPath = reader.ReadOptionalString();
        }

        public override string Message
        {
            get
            {
                if (RawMessage == null)
                {
                    RawMessage = FormatResourceStringIgnoreCodeAndKeyword("TaskAssemblyLoaded", AssemblyName, AssemblyPath, MVID.ToString());
                }

                return RawMessage;
            }
        }
    }
}
