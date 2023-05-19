// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

namespace Microsoft.Build.BackEnd
{
    internal sealed class BuildTransferredException : Exception
    {
        private readonly string? _typeName;

        public BuildTransferredException(
            string? message,
            Exception? inner,
            string? typeName,
            string deserializedStackTrace)
            : base(message, inner)
        {
            _typeName = typeName;
            StackTrace = deserializedStackTrace;
        }

        public override string? StackTrace { get; }

        public override string ToString() => $"{_typeName ?? "Unknown"}->{base.ToString()}";

        internal static Exception ReadExceptionFromTranslator(ITranslator translator)
        {
            BinaryReader reader = translator.Reader;
            Exception? innerException = null;
            if (reader.ReadBoolean())
            {
                innerException = ReadExceptionFromTranslator(translator);
            }

            string? message = ReadOptionalString(reader);
            string? typeName = ReadOptionalString(reader);
            string deserializedStackTrace = reader.ReadString();
            BuildTransferredException exception = new(message, innerException, typeName, deserializedStackTrace)
            {
                Source = ReadOptionalString(reader),
                HelpLink = ReadOptionalString(reader),
                // HResult = reader.ReadInt32(),
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
            WriteOptionalString(writer, exception.Message);
            WriteOptionalString(writer, exception.GetType().FullName);
            writer.Write(exception.StackTrace ?? string.Empty);
            WriteOptionalString(writer, exception.Source);
            WriteOptionalString(writer, exception.HelpLink);
            // HResult is completely protected up till net4.5
            // writer.Write(System.Runtime.InteropServices.Marshal.GetHRForException(exception));
        }

        private static string? ReadOptionalString(BinaryReader reader)
        {
            return reader.ReadByte() == 0 ? null : reader.ReadString();
        }

        private static void WriteOptionalString(BinaryWriter writer, string? value)
        {
            if (value == null)
            {
                writer.Write((byte)0);
            }
            else
            {
                writer.Write((byte)1);
                writer.Write(value);
            }
        }
    }
}
