// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Event args for any build event.</summary>
//-----------------------------------------------------------------------

using System;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Stores strings for parts of a message delaying the formatting until it needs to be shown
    /// </summary>
#if FEATURE_BINARY_SERIALIZATION
    [Serializable]
#endif
    public class LazyFormattedBuildEventArgs : BuildEventArgs
    {
        /// <summary>
        /// Stores the message arguments.
        /// </summary>
        private object[] arguments;

        /// <summary>
        /// Stores the original culture for String.Format.
        /// </summary>
        private CultureInfo originalCulture;

        /// <summary>
        /// Lock object.
        /// </summary>
#if FEATURE_BINARY_SERIALIZATION
        [NonSerialized]
#endif
        private Object locker;

        /// <summary>
        /// This constructor allows all event data to be initialized.
        /// </summary>
        /// <param name="message">text message.</param>
        /// <param name="helpKeyword">help keyword.</param>
        /// <param name="senderName">name of event sender.</param>
        public LazyFormattedBuildEventArgs
        (
            string message,
            string helpKeyword,
            string senderName
        )
            : this(message, helpKeyword, senderName, DateTime.Now, null)
        {
        }

        /// <summary>
        /// This constructor that allows message arguments that are lazily formatted.
        /// </summary>
        /// <param name="message">text message.</param>
        /// <param name="helpKeyword">help keyword.</param>
        /// <param name="senderName">name of event sender.</param>
        /// <param name="eventTimestamp">Timestamp when event was created.</param>
        /// <param name="messageArgs">Message arguments.</param>
        public LazyFormattedBuildEventArgs
        (
            string message,
            string helpKeyword,
            string senderName,
            DateTime eventTimestamp,
            params object[] messageArgs
        )
            : base(message, helpKeyword, senderName, eventTimestamp)
        {
            arguments = messageArgs;
            originalCulture = CultureInfo.CurrentCulture;
            locker = new Object();
        }

        /// <summary>
        /// Default constructor.
        /// </summary>
        protected LazyFormattedBuildEventArgs()
            : base()
        {
            locker = new Object();
        }

        /// <summary>
        /// Gets the formatted message.
        /// </summary>
        public override string Message
        {
            get
            {
                lock (locker)
                {
                    if (arguments != null && arguments.Length > 0)
                    {
                        base.Message = FormatString(originalCulture, base.Message, arguments);
                        arguments = null;
                    }
                }

                return base.Message;
            }
        }

#if FEATURE_BINARY_SERIALIZATION
        /// <summary>
        /// Serializes to a stream through a binary writer.
        /// </summary>
        /// <param name="writer">Binary writer which is attached to the stream the event will be serialized into.</param>
        internal override void WriteToStream(BinaryWriter writer)
        {
            // Locking is needed here as this is invoked on the serialization thread,
            // whereas a local logger (a distributed logger) may concurrently invoke this.Message
            // which will trigger formatting and thus the exception below
            lock (locker)
            {
                bool hasArguments = arguments != null;
                base.WriteToStream(writer);

                if (hasArguments && arguments == null)
                {
                    throw new InvalidOperationException("BuildEventArgs has formatted message while serializing!");
                }

                if (arguments != null)
                {
                    writer.Write(arguments.Length);

                    foreach (object argument in arguments)
                    {
                        // Arguments may be ints, etc, so explicitly convert
                        // Convert.ToString returns String.Empty when it cannot convert, rather than throwing
                        writer.Write(Convert.ToString(argument, CultureInfo.CurrentCulture));
                    }
                }
                else
                {
                    writer.Write((Int32)(-1));
                }

                writer.Write(originalCulture != null ? originalCulture.LCID : 0);
            }
        }

        /// <summary>
        /// Deserializes from a stream through a binary reader.
        /// </summary>
        /// <param name="reader">Binary reader which is attached to the stream the event will be deserialized from.</param>
        /// <param name="version">The version of the runtime the message packet was created from</param>
        internal override void CreateFromStream(BinaryReader reader, Int32 version)
        {
            base.CreateFromStream(reader, version);

            if (version > 20)
            {
                string[] messageArgs = null;
                int numArguments = reader.ReadInt32();

                if (numArguments >= 0)
                {
                    messageArgs = new string[numArguments];

                    for (int numRead = 0; numRead < numArguments; numRead++)
                    {
                        messageArgs[numRead] = reader.ReadString();
                    }
                }

                arguments = messageArgs;

                int originalCultureId = reader.ReadInt32();
                if (originalCultureId != 0)
                {
                    if (originalCultureId == CultureInfo.CurrentCulture.LCID)
                    {
                        originalCulture = CultureInfo.CurrentCulture;
                    }
                    else
                    {
                        originalCulture = new CultureInfo(originalCultureId);
                    }
                }
            }
        }
#endif

        /// <summary>
        /// Formats the given string using the variable arguments passed in.
        /// 
        /// PERF WARNING: calling a method that takes a variable number of arguments is expensive, because memory is allocated for
        /// the array of arguments -- do not call this method repeatedly in performance-critical scenarios
        /// </summary>
        /// <remarks>This method is thread-safe.</remarks>
        /// <param name="culture">The culture info for formatting the message.</param>
        /// <param name="unformatted">The string to format.</param>
        /// <param name="args">Optional arguments for formatting the given string.</param>
        /// <returns>The formatted string.</returns>
        private static string FormatString(CultureInfo culture, string unformatted, params object[] args)
        {
            // Based on the one in Shared/ResourceUtilities.
            string formatted = unformatted;

            // NOTE: String.Format() does not allow a null arguments array
            if ((args != null) && (args.Length > 0))
            {
#if DEBUG

#if VALIDATERESOURCESTRINGS
                // The code below reveals many places in our codebase where
                // we're not using all of the data given to us to format
                // strings -- but there are too many to presently fix.
                // Rather than toss away the code, we should later build it
                // and fix each offending resource (or the code processing
                // the resource).

                // String.Format() will throw a FormatException if args does
                // not have enough elements to match each format parameter.
                // However, it provides no feedback in the case when args contains
                // more elements than necessary to replace each format 
                // parameter.  We'd like to know if we're providing too much
                // data in cases like these, so we'll fail if this code runs.
                                                
                // We create an array with one fewer element
                object[] trimmedArgs = new object[args.Length - 1];
                Array.Copy(args, 0, trimmedArgs, 0, args.Length - 1);

                bool caughtFormatException = false;
                try
                {
                    // This will throw if there aren't enough elements in trimmedArgs...
                    String.Format(CultureInfo.CurrentCulture, unformatted, trimmedArgs);
                }
                catch (FormatException)
                {
                    caughtFormatException = true;
                }

                // If we didn't catch an exception above, then some of the elements
                // of args were unnecessary when formatting unformatted...
                Debug.Assert
                (
                    caughtFormatException,
                    String.Format("The provided format string '{0}' had fewer format parameters than the number of format args, '{1}'.", unformatted, args.Length)
                );
#endif
                // If you accidentally pass some random type in that can't be converted to a string, 
                // FormatResourceString calls ToString() which returns the full name of the type!
                foreach (object param in args)
                {
                    // Check against a list of types that we know have
                    // overridden ToString() usefully. If you want to pass 
                    // another one, add it here.
                    if (param != null && param.ToString() == param.GetType().FullName)
                    {
                        throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, "Invalid type for message formatting argument, was {0}", param.GetType().FullName));
                    }
                }
#endif
                // Format the string, using the variable arguments passed in.
                // NOTE: all String methods are thread-safe
                try
                {
                    formatted = String.Format(culture, unformatted, args);
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
                    //             at System.String.Format(IFormatProvider provider, String format, Object[] args)
                    //             at Microsoft.Build.Framework.LazyFormattedBuildEventArgs.FormatString(CultureInfo culture, String unformatted, Object[] args) in d:\W8T_Refactor\src\vsproject\xmake\Framework\LazyFormattedBuildEventArgs.cs:line 263
                    //          Done executing task "Crash".
                    //
                    // T
                    formatted = String.Format(CultureInfo.CurrentCulture, "\"{0}\"\n{1}", unformatted, ex.ToString());
                }
            }

            return formatted;
        }
#if FEATURE_BINARY_SERIALIZATION
        /// <summary>
        /// Deserialization does not call any constructors, not even
        /// the parameterless constructor. Therefore since we do not serialize
        /// this field, we must populate it here.
        /// </summary>
        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            locker = new Object();
        }
#endif
    }
}
