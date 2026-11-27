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
    internal static bool TryComputeKey(TaskLoggingHelper log, string toolName, InvocationDescription resolvedInvocation,
        string resolvedWorkingDirectory, out string key)
    {
        key = string.Empty;
        try
        {
            key = ComputeKeyResolved(resolvedInvocation, resolvedWorkingDirectory);
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
        AbsolutePath baseDirectory = new AbsolutePath(Path.GetFullPath(workingDirectory)).GetCanonicalForm();
        return ComputeKeyResolved(invocation.ResolvePaths(baseDirectory), baseDirectory.Value);
    }

    private static string ComputeKeyResolved(InvocationDescription resolved, string resolvedWorkingDirectory)
    {
        using var sha256 = SHA256.Create();
        using var data = new MemoryStream();
        using var writer = new BinaryWriter(data, Encoding.UTF8, leaveOpen: true);
        writer.Write("invocation-cache-key-v1");
        writer.Write(resolvedWorkingDirectory);
        writer.Write(RuntimeInformation.OSDescription);
        writer.Write(RuntimeInformation.ProcessArchitecture.ToString());
        writer.Write(CultureInfo.CurrentCulture.Name);
        writer.Write(CultureInfo.CurrentUICulture.Name);
        writer.Write(resolved.Context.Length);
        foreach (string value in resolved.Context)
        {
            writer.Write(value);
        }

        writer.Write(resolved.Outputs.DirectoryPath is not null);
        if (resolved.Outputs.DirectoryPath is not null)
        {
            writer.Write(resolved.Outputs.DirectoryPath);
        }

        writer.Write(resolved.Outputs.Files.Length);
        foreach (string file in resolved.Outputs.Files)
        {
            writer.Write(file);
        }

        writer.Write(resolved.Inputs.Files.Length);
        foreach (string input in resolved.Inputs.Files)
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
