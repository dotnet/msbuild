// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BackEnd
{
    internal sealed class BuildTransferredException : Exception
    {
        private readonly string _originalTypeName;

        public BuildTransferredException(
            string message,
            Exception? inner,
            string originalTypeName,
            string? deserializedStackTrace)
            : base(message, inner)
        {
            _originalTypeName = originalTypeName;
            StackTrace = deserializedStackTrace;
        }

        public override string? StackTrace { get; }

        public override string ToString() => $"{_originalTypeName}->{base.ToString()}";

        internal static Exception ReadExceptionFromTranslator(ITranslator translator)
        {
            BinaryReader reader = translator.Reader;
            Exception? innerException = null;
            if (reader.ReadBoolean())
            {
                innerException = ReadExceptionFromTranslator(translator);
            }

            string message = reader.ReadString();
            string typeName = reader.ReadString();
            string? deserializedStackTrace = reader.ReadOptionalString();
            BuildTransferredException exception = new(message, innerException, typeName, deserializedStackTrace)
            {
                Source = reader.ReadOptionalString(),
                HelpLink = reader.ReadOptionalString(),
                HResult = reader.ReadOptionalInt32(),
            };

            return exception;
        }

        internal static void WriteExceptionToTranslator(ITranslator translator, Exception exception)
        {
            BinaryWriter writer = translator.Writer;
            writer.Write(exception.InnerException != null);
            if (exception.InnerException != null)
            {
                WriteExceptionToTranslator(translator, exception.InnerException);
            }
            writer.Write(exception.Message);
            writer.Write(exception.GetType().FullName ?? exception.GetType().ToString());
            writer.WriteOptionalString(exception.StackTrace);
            writer.WriteOptionalString(exception.Source);
            writer.WriteOptionalString(exception.HelpLink);
            // HResult is completely protected up till net4.5
#if NET || NET45_OR_GREATER
            writer.Write((byte)1);
            writer.Write(exception.HResult);
#else
            writer.Write((byte)0);
#endif

            Debug.Assert((exception.Data?.Count ?? 0) == 0,
                "Exception Data is not supported in BuildTransferredException");
        }
    }
}
