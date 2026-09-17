// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Microsoft.Build;

internal static partial class ArgumentExceptionExtensions
{
    extension(ArgumentException)
    {
        /// <summary>
        ///  Throws an exception if <paramref name="argument"/> is <see langword="null"/> or empty.
        /// </summary>
        /// <typeparam name="T">The element type of the collection.</typeparam>
        /// <param name="argument">
        ///  The collection argument to validate as non-null and non-empty.
        /// </param>
        /// <param name="paramName">
        ///  The name of the parameter with which <paramref name="argument"/> corresponds.
        /// </param>
        /// <exception cref="ArgumentNullException">
        ///  <paramref name="argument"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///  <paramref name="argument"/> is empty.
        /// </exception>
        public static void ThrowIfNullOrEmpty<T>([NotNull] IReadOnlyCollection<T>? argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        {
            ArgumentNullException.ThrowIfNull(argument, paramName);

            if (argument.Count == 0)
            {
                throw new ArgumentException(SR.Argument_EmptyCollection, paramName);
            }
        }
    }
}
