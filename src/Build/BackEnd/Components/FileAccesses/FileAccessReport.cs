// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.BackEnd;
using Microsoft.Build.Experimental.FileAccess;

namespace Microsoft.Build.FileAccesses
{
    internal sealed class FileAccessReport : INodePacket
    {
        private FileAccessData _fileAccessData;

        internal FileAccessReport(FileAccessData fileAccessData) => _fileAccessData = fileAccessData;

        private FileAccessReport(ITranslator translator) => Translate(translator);

        /// <inheritdoc/>
        public NodePacketType Type => NodePacketType.FileAccessReport;

        /// <inheritdoc/>
        public void Translate(ITranslator translator) => translator.Translate(ref _fileAccessData);

        internal FileAccessData FileAccessData => _fileAccessData;

        internal static INodePacket FactoryForDeserialization(ITranslator translator) => new FileAccessReport(translator);
    }
}
