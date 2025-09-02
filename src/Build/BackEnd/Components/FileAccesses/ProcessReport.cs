// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.BackEnd;
using Microsoft.Build.Experimental.FileAccess;

namespace Microsoft.Build.FileAccesses
{
    internal sealed class ProcessReport : INodePacket
    {
        private ProcessData _processData;

        internal ProcessReport(ProcessData processData) => _processData = processData;

        private ProcessReport(ITranslator translator) => Translate(translator);

        /// <inheritdoc/>
        public NodePacketType Type => NodePacketType.ProcessReport;

        internal ProcessData ProcessData => _processData;

        internal static INodePacket FactoryForDeserialization(ITranslator translator) => new ProcessReport(translator);

        /// <inheritdoc/>
        public void Translate(ITranslator translator) => translator.Translate(ref _processData);
    }
}
