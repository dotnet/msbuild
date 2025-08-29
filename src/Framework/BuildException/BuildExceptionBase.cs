// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Framework.BuildException;

public abstract class BuildExceptionBase : Exception
{
    private string? _remoteTypeName;
    private string? _remoteStackTrace;

    private protected BuildExceptionBase()
        : base()
    { }

    private protected BuildExceptionBase(string message)
        : base(message)
    { }

    private protected BuildExceptionBase(
        string message,
        Exception? inner)
        : base(message, inner)
    { }

    // This is needed to allow opting back in to BinaryFormatter serialization
#if NET8_0_OR_GREATER
    [Obsolete(DiagnosticId = "SYSLIB0051")]
#endif
    private protected BuildExceptionBase(SerializationInfo info, StreamingContext context)
        : base(info, context)
    { }

    public override string? StackTrace => string.IsNullOrEmpty(_remoteStackTrace) ? base.StackTrace : _remoteStackTrace;

    public override string ToString() => string.IsNullOrEmpty(_remoteTypeName) ? base.ToString() : $"{_remoteTypeName}->{base.ToString()}";

    /// <summary>
    /// Override this method to recover subtype-specific state from the remote exception.
    /// </summary>
    protected virtual void InitializeCustomState(IDictionary<string, string?>? customKeyedSerializedData)
    { }

    /// <summary>
    /// Override this method to provide subtype-specific state to be serialized.
    /// </summary>
    /// <returns></returns>
    protected virtual IDictionary<string, string?>? FlushCustomState()
    {
        return null;
    }

    private void InitializeFromRemoteState(BuildExceptionRemoteState remoteState)
    {
        _remoteTypeName = remoteState.RemoteTypeName;
        _remoteStackTrace = remoteState.RemoteStackTrace;
        base.Source = remoteState.Source;
        base.HelpLink = remoteState.HelpLink;
        base.HResult = remoteState.HResult;
        if (remoteState.Source != null)
        {
            InitializeCustomState(remoteState.CustomKeyedSerializedData);
        }
    }

    internal static void WriteExceptionToTranslator(ITranslator translator, Exception exception)
    {
        BinaryWriter writer = translator.Writer;
        writer.Write(exception.InnerException != null);
        if (exception.InnerException != null)
        {
            WriteExceptionToTranslator(translator, exception.InnerException);
        }

        string serializationType = BuildExceptionSerializationHelper.GetExceptionSerializationKey(exception.GetType());
        writer.Write(serializationType);
        writer.Write(exception.Message);
        writer.WriteOptionalString(exception.StackTrace);
        writer.WriteOptionalString(exception.Source);
        writer.WriteOptionalString(exception.HelpLink);
        // HResult is completely protected up till net4.5
#if NET || NET45_OR_GREATER
        int? hresult = exception.HResult;
#else
            int? hresult = null;
#endif
        writer.WriteOptionalInt32(hresult);

        IDictionary<string, string?>? customKeyedSerializedData = (exception as BuildExceptionBase)?.FlushCustomState();
        if (customKeyedSerializedData == null)
        {
            writer.Write((byte)0);
        }
        else
        {
            writer.Write((byte)1);
            writer.Write(customKeyedSerializedData.Count);
            foreach (var pair in customKeyedSerializedData)
            {
                writer.Write(pair.Key);
                writer.WriteOptionalString(pair.Value);
            }
        }

        Debug.Assert((exception.Data?.Count ?? 0) == 0,
            "Exception Data is not supported in BuildTransferredException");
    }

    internal static Exception ReadExceptionFromTranslator(ITranslator translator)
    {
        BinaryReader reader = translator.Reader;
        Exception? innerException = null;
        if (reader.ReadBoolean())
        {
            innerException = ReadExceptionFromTranslator(translator);
        }

        string serializationType = reader.ReadString();
        string message = reader.ReadString();
        string? deserializedStackTrace = reader.ReadOptionalString();
        string? source = reader.ReadOptionalString();
        string? helpLink = reader.ReadOptionalString();
        int hResult = reader.ReadOptionalInt32();

        IDictionary<string, string?>? customKeyedSerializedData = null;
        if (reader.ReadByte() == 1)
        {
            int count = reader.ReadInt32();
            customKeyedSerializedData = new Dictionary<string, string?>(count, StringComparer.CurrentCulture);

            for (int i = 0; i < count; i++)
            {
                customKeyedSerializedData[reader.ReadString()] = reader.ReadOptionalString();
            }
        }

        BuildExceptionBase exception = BuildExceptionSerializationHelper.CreateExceptionFactory(serializationType)(message, innerException);

        exception.InitializeFromRemoteState(
            new BuildExceptionRemoteState(
                serializationType,
                deserializedStackTrace,
                source,
                helpLink,
                hResult,
                customKeyedSerializedData));

        return exception;
    }
}
