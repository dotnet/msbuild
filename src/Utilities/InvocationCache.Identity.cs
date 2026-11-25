// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Build.Framework;

#pragma warning disable MSBUILD001

namespace Microsoft.Build.Utilities;

public sealed partial class InvocationCache
{
    internal static bool TryComputeKey(TaskLoggingHelper log, string toolName, InvocationDescription invocation,
        string workingDirectory, out string key)
    {
        key = string.Empty;
        try
        {
            key = ComputeKey(invocation, workingDirectory);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or (ArgumentException and not ArgumentNullException) or NotSupportedException or SecurityException)
        {
            log.LogMessage(MessageImportance.Low, $"{toolName} cache bypassed: input identity could not be computed: {ex.Message}");
            return false;
        }
    }

    internal static string ComputeKey(InvocationDescription invocation)
        => ComputeKey(invocation, Environment.CurrentDirectory);

    internal static string ComputeKey(InvocationDescription invocation, string workingDirectory)
    {
        using var sha256 = SHA256.Create();
        using var data = new MemoryStream();
        using var writer = new BinaryWriter(data, Encoding.UTF8, leaveOpen: true);
        writer.Write("invocation-cache-key-v1");
        writer.Write(Path.GetFullPath(workingDirectory));
        writer.Write(RuntimeInformation.OSDescription);
        writer.Write(RuntimeInformation.ProcessArchitecture.ToString());
        writer.Write(CultureInfo.CurrentCulture.Name);
        writer.Write(CultureInfo.CurrentUICulture.Name);
        writer.Write(invocation.Context.Length);
        foreach (string value in invocation.Context)
        {
            writer.Write(value);
        }

        writer.Write(invocation.Outputs.DirectoryPath is not null);
        if (invocation.Outputs.DirectoryPath is not null)
        {
            writer.Write(invocation.Outputs.DirectoryPath);
        }

        writer.Write(invocation.Outputs.Files.Length);
        foreach (string file in invocation.Outputs.Files)
        {
            writer.Write(file);
        }

        writer.Write(invocation.Inputs.Files.Length);
        foreach (string input in invocation.Inputs.Files)
        {
            writer.Write(input);
            try
            {
                using FileStream file = File.OpenRead(input);
                writer.Write(true);
                writer.Write(sha256.ComputeHash(file));
            }
            catch (FileNotFoundException)
            {
                writer.Write(false);
            }
            catch (DirectoryNotFoundException)
            {
                writer.Write(false);
            }
        }

        writer.Flush();
        data.Position = 0;
#if NET
        return Convert.ToHexStringLower(sha256.ComputeHash(data));
#else
        return BitConverter.ToString(sha256.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
#endif
    }
}
