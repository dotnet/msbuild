// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using Microsoft.Build.Framework.Utilities;

namespace Microsoft.Build.Framework;

/// <summary>
///  Stores an unformatted message and deferred args, formatting the message only when it is requested.
/// </summary>
[Serializable]
public class LazyFormattedBuildEventArgs : BuildEventArgs
{
    /// <summary>
    ///  Stores deferred args, a formatted message, or <see langword="null"/>.
    /// </summary>
    /// <remarks>
    ///  This field name is part of the formatter-serialization contract and must not be changed.
    /// </remarks>
    private volatile object? argumentsOrFormattedMessage;

    /// <summary>
    ///  Gets the formatted message after deferred formatting has occurred.
    /// </summary>
    private protected override string? FormattedMessage
        => argumentsOrFormattedMessage is string formattedMessage ? formattedMessage : base.FormattedMessage;

    /// <summary>
    ///  Initializes an event without deferred args.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="helpKeyword">The help keyword.</param>
    /// <param name="senderName">The event sender name.</param>
    public LazyFormattedBuildEventArgs(
        [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message,
        string? helpKeyword,
        string? senderName)
        : this(message, helpKeyword, senderName, DateTime.Now, messageArgs: null)
    {
    }

    /// <summary>
    ///  Initializes an event with args that are formatted lazily.
    /// </summary>
    /// <param name="message">The composite format string.</param>
    /// <param name="helpKeyword">The help keyword.</param>
    /// <param name="senderName">The event sender name.</param>
    /// <param name="eventTimestamp">The time when the event was created.</param>
    /// <param name="messageArgs">The args to format into <paramref name="message"/>.</param>
    public LazyFormattedBuildEventArgs(
        [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message,
        string? helpKeyword,
        string? senderName,
        DateTime eventTimestamp,
        params object[]? messageArgs)
        : base(message, helpKeyword, senderName, eventTimestamp)
    {
        argumentsOrFormattedMessage = messageArgs;
    }

    /// <summary>
    ///  Initializes an empty event for derived types and deserialization.
    /// </summary>
    protected LazyFormattedBuildEventArgs()
        : base()
    {
    }

    /// <summary>
    ///  Gets the number of deferred args.
    /// </summary>
    /// <value>
    ///  The number of deferred args, or zero when the message has no deferred args or has already been formatted.
    /// </value>
    internal int ArgCount => argumentsOrFormattedMessage switch
    {
        object?[] args => args.Length,
        IMessageArgs args => args.Count,
        _ => 0,
    };

    /// <summary>
    ///  Gets a deferred arg by index.
    /// </summary>
    /// <param name="index">The zero-based arg index.</param>
    /// <returns>
    ///  The deferred arg.
    /// </returns>
    internal object? this[int index] => argumentsOrFormattedMessage switch
    {
        object?[] args => args[index],
        IMessageArgs args => args[index],
        _ => Assumed.Unreachable<object?>(),
    };

    /// <summary>
    ///  Sets the deferred args.
    /// </summary>
    /// <param name="args">The deferred args to store.</param>
    internal void SetArgs(IMessageArgs args)
    {
        argumentsOrFormattedMessage = args;
    }

    /// <inheritdoc/>
    public override string? Message
    {
        get
        {
            object? argsOrMessage = argumentsOrFormattedMessage;
            if (argsOrMessage is string formattedMessage)
            {
                return formattedMessage;
            }

            string? message = base.Message;

            if (message is not null)
            {
                if (argsOrMessage is object[] { Length: > 0 } args)
                {
                    string result = FormatString(message, args);
                    argumentsOrFormattedMessage = result;
                    return result;
                }

                if (argsOrMessage is IMessageArgs messageArgs)
                {
                    string result = FormatString(message, messageArgs);
                    argumentsOrFormattedMessage = result;
                    return result;
                }
            }

            return message;
        }
    }

    /// <summary>
    ///  Formats the message without replacing the stored deferred args.
    /// </summary>
    /// <returns>
    ///  The formatted message, or <see langword="null"/> when no message is available.
    /// </returns>
    /// <remarks>
    ///  Use this for render-only scenarios that must preserve deferred args for later serialization.
    /// </remarks>
    internal string? FormatMessageWithoutMutation()
    {
        object? argsOrMessage = argumentsOrFormattedMessage;
        if (argsOrMessage is string formattedMessage)
        {
            return formattedMessage;
        }

        string? message = base.Message;

        if (message is not null)
        {
            if (argsOrMessage is object[] { Length: > 0 } args)
            {
                return FormatString(message, args);
            }

            if (argsOrMessage is IMessageArgs messageArgs)
            {
                return FormatString(message, messageArgs);
            }
        }

        return message;
    }

    /// <inheritdoc/>
    internal override void WriteToStream(BinaryWriter writer)
    {
        object? argsOrMessage = argumentsOrFormattedMessage;
        if (argsOrMessage is object[] args && args.Length > 0)
        {
            WriteToStreamWithExplicitMessage(writer, base.Message);
            writer.Write(args.Length);

            foreach (object arg in args)
            {
                WriteArg(writer, arg);
            }
        }
        else if (argsOrMessage is IMessageArgs messageArgs && messageArgs.Count > 0)
        {
            WriteToStreamWithExplicitMessage(writer, base.Message);
            writer.Write(messageArgs.Count);

            for (int i = 0; i < messageArgs.Count; i++)
            {
                WriteArg(writer, messageArgs[i]);
            }
        }
        else
        {
            WriteToStreamWithExplicitMessage(writer, (argsOrMessage is string formattedMessage) ? formattedMessage : base.Message);
            writer.Write(-1);
        }
    }

    /// <inheritdoc/>
    internal override void CreateFromStream(BinaryReader reader, int version)
    {
        base.CreateFromStream(reader, version);

        if (version > 20)
        {
            string[]? messageArgs = null;
            int numArguments = reader.ReadInt32();

            if (numArguments >= 0)
            {
                messageArgs = new string[numArguments];

                for (int numRead = 0; numRead < numArguments; numRead++)
                {
                    messageArgs[numRead] = reader.ReadString();
                }
            }

            argumentsOrFormattedMessage = messageArgs;
        }
    }

    /// <summary>
    ///  Materializes provider-backed args into the legacy array representation before formatter serialization.
    /// </summary>
    /// <param name="context">The formatter streaming context.</param>
    [OnSerializing]
    private void OnSerializing(StreamingContext context)
    {
        if (argumentsOrFormattedMessage is IMessageArgs messageArgs)
        {
            object?[] args = new object?[messageArgs.Count];
            for (int i = 0; i < args.Length; i++)
            {
                args[i] = messageArgs[i];
            }

            argumentsOrFormattedMessage = args;
        }
    }

    /// <summary>
    ///  Writes one arg using the current culture.
    /// </summary>
    /// <param name="writer">The writer receiving the converted arg.</param>
    /// <param name="arg">The arg to convert and write.</param>
    private static void WriteArg(BinaryWriter writer, object? arg)
    {
        // Arguments may be ints, etc, so explicitly convert.
        // Convert.ToString returns String.Empty when it cannot convert, rather than throwing.
        // It returns null if the input is null.
        string argValue;
        try
        {
            argValue = Convert.ToString(arg, CultureInfo.CurrentCulture) ?? "";
        }
        catch (Exception e)
        {
            // Gracefully handle a custom ToString implementation that throws.
            argValue = $"Argument conversion to string failed{Environment.NewLine}{e}";
        }

        writer.Write(argValue);
    }

    /// <summary>
    ///  Formats a composite format string using a legacy arg array.
    /// </summary>
    /// <param name="format">The composite format string.</param>
    /// <param name="args">The args to format.</param>
    /// <returns>
    ///  The formatted string, or a defensive error message when <paramref name="format"/> is invalid.
    /// </returns>
    /// <remarks>
    ///  The caller-provided arg array is retained by the event until the message is formatted.
    /// </remarks>
    private static string FormatString([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format, params object?[] args)
    {
        // Based on the one in Shared/ResourceUtilities.
        string result = format;

        // NOTE: String.Format() does not allow a null arguments array
        if (args?.Length > 0)
        {
#if DEBUG
            // If you accidentally pass some random type in that can't be converted to a string,
            // FormatResourceString calls ToString() which returns the full name of the type!
            foreach (object? arg in args)
            {
                // Check against a list of types that we know have
                // overridden ToString() usefully. If you want to pass
                // another one, add it here.
                if (arg != null && arg.ToString() == arg.GetType().FullName)
                {
                    throw new InvalidOperationException($"Invalid type for message formatting argument, was {arg.GetType().FullName}");
                }
            }
#endif

            // Format the string, using the variable arguments passed in.
            // NOTE: all String methods are thread-safe
            try
            {
                result = string.Format(format, args);
            }
            catch (FormatException ex)
            {
                // User task may have logged something with too many format parameters
                // We don't have resources in this assembly, and we generally log stack for task failures so they can be fixed by the owner
                // However, we don't want to crash the logger and stop the build.
                // Error will look like this (it's OK to not localize subcategory). It's not too bad, although there's no file.
                //
                //       Task "Crash"
                //          (16,14):  error : "This message logged from a task {1} has too few formatting parameters."
                //             at System.Text.StringBuilder.AppendFormat(IFormatProvider provider, String format, Object[] args)
                //             at System.String.Format(String format, Object[] args)
                //             at Microsoft.Build.Framework.LazyFormattedBuildEventArgs.FormatString(String unformatted, Object[] args) in d:\W8T_Refactor\src\vsproject\xmake\Framework\LazyFormattedBuildEventArgs.cs:line 263
                //          Done executing task "Crash".

                result = FormatExceptionMessage(format, ex);
            }
        }

        return result;
    }

    /// <summary>
    ///  Formats a composite format string using provider-backed args.
    /// </summary>
    /// <param name="format">The composite format string.</param>
    /// <param name="args">The args to format.</param>
    /// <returns>
    ///  The formatted string, or a defensive error message when <paramref name="format"/> is invalid.
    /// </returns>
    private static string FormatString([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format, IMessageArgs args)
    {
        try
        {
            return args.Format(format);
        }
        catch (FormatException ex)
        {
            return FormatExceptionMessage(format, ex);
        }
    }

    /// <summary>
    ///  Creates the defensive message used when a composite format string is invalid.
    /// </summary>
    /// <param name="format">The invalid composite format string.</param>
    /// <param name="exception">The formatting exception.</param>
    /// <returns>
    ///  A message containing the invalid format string and exception details.
    /// </returns>
    private static string FormatExceptionMessage(string format, FormatException exception)
        => $"\"{format}\"\n{exception}";
}
