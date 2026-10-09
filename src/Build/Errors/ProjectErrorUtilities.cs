// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Internal;

/// <summary>
///  Provides methods for reporting project-file errors as <see cref="InvalidProjectFileException"/>.
/// </summary>
/// <remarks>
///  Generic verification overloads avoid boxing format arguments and allocating argument arrays when
///  their condition succeeds. Diagnostic resources are resolved only when an exception is needed.
/// </remarks>
internal static class ProjectErrorUtilities
{
    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> without formatting arguments or a subcategory.
    /// </summary>
    /// <param name="location">The location associated with the project-file error.</param>
    /// <param name="resourceName">The name of the resource containing the error message.</param>
    /// <exception cref="InvalidProjectFileException">Always thrown.</exception>
    [DoesNotReturn]
    public static void ThrowInvalidProject(IElementLocation location, string resourceName)
        => ThrowInvalidProjectCore(DiagnosticSubcategory.None, location, innerException: null, resourceName);

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> with one format argument and no subcategory.
    /// </summary>
    /// <param name="location">The location associated with the project-file error.</param>
    /// <param name="resourceName">The name of the resource containing the error message.</param>
    /// <param name="arg0">The first argument for formatting the resource string.</param>
    /// <exception cref="InvalidProjectFileException">Always thrown.</exception>
    [DoesNotReturn]
    public static void ThrowInvalidProject(IElementLocation location, string resourceName, object? arg0)
        => ThrowInvalidProjectCore(DiagnosticSubcategory.None, location, innerException: null, resourceName, arg0);

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> with two format arguments and no subcategory.
    /// </summary>
    /// <param name="location">The location associated with the project-file error.</param>
    /// <param name="resourceName">The name of the resource containing the error message.</param>
    /// <param name="arg0">The first argument for formatting the resource string.</param>
    /// <param name="arg1">The second argument for formatting the resource string.</param>
    /// <exception cref="InvalidProjectFileException">Always thrown.</exception>
    [DoesNotReturn]
    public static void ThrowInvalidProject(IElementLocation location, string resourceName, object? arg0, object? arg1)
        => ThrowInvalidProjectCore(DiagnosticSubcategory.None, location, innerException: null, resourceName, arg0, arg1);

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> with three format arguments and no subcategory.
    /// </summary>
    /// <param name="location">The location associated with the project-file error.</param>
    /// <param name="resourceName">The name of the resource containing the error message.</param>
    /// <param name="arg0">The first argument for formatting the resource string.</param>
    /// <param name="arg1">The second argument for formatting the resource string.</param>
    /// <param name="arg2">The third argument for formatting the resource string.</param>
    /// <exception cref="InvalidProjectFileException">Always thrown.</exception>
    [DoesNotReturn]
    public static void ThrowInvalidProject(IElementLocation location, string resourceName, object? arg0, object? arg1, object? arg2)
        => ThrowInvalidProjectCore(DiagnosticSubcategory.None, location, innerException: null, resourceName, arg0, arg1, arg2);

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> with formatting arguments and no subcategory.
    /// </summary>
    /// <param name="location">The location associated with the project-file error.</param>
    /// <param name="resourceName">The name of the resource containing the error message.</param>
    /// <param name="args">The arguments for formatting the resource string.</param>
    /// <exception cref="InvalidProjectFileException">Always thrown.</exception>
    [DoesNotReturn]
    public static void ThrowInvalidProject(IElementLocation location, string resourceName, params object?[] args)
        => ThrowInvalidProjectCore(DiagnosticSubcategory.None, location, innerException: null, resourceName, args);

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> with an inner exception, no formatting arguments,
    ///  and no subcategory.
    /// </summary>
    /// <param name="location">The location associated with the project-file error.</param>
    /// <param name="innerException">The inner exception, or <see langword="null"/>.</param>
    /// <param name="resourceName">The name of the resource containing the error message.</param>
    /// <exception cref="InvalidProjectFileException">Always thrown.</exception>
    [DoesNotReturn]
    public static void ThrowInvalidProject(IElementLocation location, Exception? innerException, string resourceName)
        => ThrowInvalidProjectCore(DiagnosticSubcategory.None, location, innerException, resourceName);

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> with an inner exception, one format argument,
    ///  and no subcategory.
    /// </summary>
    /// <param name="location">The location associated with the project-file error.</param>
    /// <param name="innerException">The inner exception, or <see langword="null"/>.</param>
    /// <param name="resourceName">The name of the resource containing the error message.</param>
    /// <param name="arg0">The first argument for formatting the resource string.</param>
    /// <exception cref="InvalidProjectFileException">Always thrown.</exception>
    [DoesNotReturn]
    public static void ThrowInvalidProject(IElementLocation location, Exception? innerException, string resourceName, object? arg0)
        => ThrowInvalidProjectCore(DiagnosticSubcategory.None, location, innerException, resourceName, arg0);

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> with an inner exception, two format arguments,
    ///  and no subcategory.
    /// </summary>
    /// <param name="location">The location associated with the project-file error.</param>
    /// <param name="innerException">The inner exception, or <see langword="null"/>.</param>
    /// <param name="resourceName">The name of the resource containing the error message.</param>
    /// <param name="arg0">The first argument for formatting the resource string.</param>
    /// <param name="arg1">The second argument for formatting the resource string.</param>
    /// <exception cref="InvalidProjectFileException">Always thrown.</exception>
    [DoesNotReturn]
    public static void ThrowInvalidProject(
        IElementLocation location,
        Exception? innerException,
        string resourceName,
        object? arg0,
        object? arg1)
        => ThrowInvalidProjectCore(DiagnosticSubcategory.None, location, innerException, resourceName, arg0, arg1);

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> with an inner exception, three format arguments,
    ///  and no subcategory.
    /// </summary>
    /// <param name="location">The location associated with the project-file error.</param>
    /// <param name="innerException">The inner exception, or <see langword="null"/>.</param>
    /// <param name="resourceName">The name of the resource containing the error message.</param>
    /// <param name="arg0">The first argument for formatting the resource string.</param>
    /// <param name="arg1">The second argument for formatting the resource string.</param>
    /// <param name="arg2">The third argument for formatting the resource string.</param>
    /// <exception cref="InvalidProjectFileException">Always thrown.</exception>
    [DoesNotReturn]
    public static void ThrowInvalidProject(
        IElementLocation location,
        Exception? innerException,
        string resourceName,
        object? arg0,
        object? arg1,
        object? arg2)
        => ThrowInvalidProjectCore(DiagnosticSubcategory.None, location, innerException, resourceName, arg0, arg1, arg2);

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> with an inner exception and no subcategory.
    /// </summary>
    /// <param name="location">The location associated with the project-file error.</param>
    /// <param name="innerException">The inner exception, or <see langword="null"/>.</param>
    /// <param name="resourceName">The name of the resource containing the error message.</param>
    /// <param name="args">The arguments for formatting the resource string.</param>
    /// <exception cref="InvalidProjectFileException">Always thrown.</exception>
    [DoesNotReturn]
    public static void ThrowInvalidProject(
        IElementLocation location,
        Exception? innerException,
        string resourceName,
        params object?[] args)
        => ThrowInvalidProjectCore(DiagnosticSubcategory.None, location, innerException, resourceName, args);

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> with a subcategory and no formatting arguments.
    /// </summary>
    /// <param name="subcategory">The diagnostic subcategory, or <see cref="DiagnosticSubcategory.None"/> for no subcategory.</param>
    /// <param name="location">The location associated with the project-file error.</param>
    /// <param name="resourceName">The name of the resource containing the error message.</param>
    /// <exception cref="InvalidProjectFileException">Always thrown.</exception>
    [DoesNotReturn]
    public static void ThrowInvalidProject(DiagnosticSubcategory subcategory, IElementLocation location, string resourceName)
        => ThrowInvalidProjectCore(subcategory, location, innerException: null, resourceName);

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> with a subcategory and one format argument.
    /// </summary>
    /// <param name="subcategory">The diagnostic subcategory, or <see cref="DiagnosticSubcategory.None"/> for no subcategory.</param>
    /// <param name="location">The location associated with the project-file error.</param>
    /// <param name="resourceName">The name of the resource containing the error message.</param>
    /// <param name="arg0">The first argument for formatting the resource string.</param>
    /// <exception cref="InvalidProjectFileException">Always thrown.</exception>
    [DoesNotReturn]
    public static void ThrowInvalidProject(DiagnosticSubcategory subcategory, IElementLocation location, string resourceName, object? arg0)
        => ThrowInvalidProjectCore(subcategory, location, innerException: null, resourceName, arg0);

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> with a subcategory and two format arguments.
    /// </summary>
    /// <param name="subcategory">The diagnostic subcategory, or <see cref="DiagnosticSubcategory.None"/> for no subcategory.</param>
    /// <param name="location">The location associated with the project-file error.</param>
    /// <param name="resourceName">The name of the resource containing the error message.</param>
    /// <param name="arg0">The first argument for formatting the resource string.</param>
    /// <param name="arg1">The second argument for formatting the resource string.</param>
    /// <exception cref="InvalidProjectFileException">Always thrown.</exception>
    [DoesNotReturn]
    public static void ThrowInvalidProject(
        DiagnosticSubcategory subcategory,
        IElementLocation location,
        string resourceName,
        object? arg0,
        object? arg1)
        => ThrowInvalidProjectCore(subcategory, location, innerException: null, resourceName, arg0, arg1);

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> with a subcategory and three format arguments.
    /// </summary>
    /// <param name="subcategory">The diagnostic subcategory, or <see cref="DiagnosticSubcategory.None"/> for no subcategory.</param>
    /// <param name="location">The location associated with the project-file error.</param>
    /// <param name="resourceName">The name of the resource containing the error message.</param>
    /// <param name="arg0">The first argument for formatting the resource string.</param>
    /// <param name="arg1">The second argument for formatting the resource string.</param>
    /// <param name="arg2">The third argument for formatting the resource string.</param>
    /// <exception cref="InvalidProjectFileException">Always thrown.</exception>
    [DoesNotReturn]
    public static void ThrowInvalidProject(
        DiagnosticSubcategory subcategory,
        IElementLocation location,
        string resourceName,
        object? arg0,
        object? arg1,
        object? arg2)
        => ThrowInvalidProjectCore(subcategory, location, innerException: null, resourceName, arg0, arg1, arg2);

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> with a subcategory and formatting arguments.
    /// </summary>
    /// <param name="subcategory">The diagnostic subcategory, or <see cref="DiagnosticSubcategory.None"/> for no subcategory.</param>
    /// <param name="location">The location associated with the project-file error.</param>
    /// <param name="resourceName">The name of the resource containing the error message.</param>
    /// <param name="args">The arguments for formatting the resource string.</param>
    /// <exception cref="InvalidProjectFileException">Always thrown.</exception>
    [DoesNotReturn]
    public static void ThrowInvalidProject(
        DiagnosticSubcategory subcategory,
        IElementLocation location,
        string resourceName,
        params object?[] args)
        => ThrowInvalidProjectCore(subcategory, location, innerException: null, resourceName, args);

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> with a subcategory, an inner exception,
    ///  and no formatting arguments.
    /// </summary>
    /// <param name="subcategory">The diagnostic subcategory, or <see cref="DiagnosticSubcategory.None"/> for no subcategory.</param>
    /// <param name="location">The location associated with the project-file error.</param>
    /// <param name="innerException">The inner exception, or <see langword="null"/>.</param>
    /// <param name="resourceName">The name of the resource containing the error message.</param>
    /// <exception cref="InvalidProjectFileException">Always thrown.</exception>
    [DoesNotReturn]
    public static void ThrowInvalidProject(
        DiagnosticSubcategory subcategory,
        IElementLocation location,
        Exception? innerException,
        string resourceName)
        => ThrowInvalidProjectCore(subcategory, location, innerException, resourceName);

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> with a subcategory, an inner exception,
    ///  and one format argument.
    /// </summary>
    /// <param name="subcategory">The diagnostic subcategory, or <see cref="DiagnosticSubcategory.None"/> for no subcategory.</param>
    /// <param name="location">The location associated with the project-file error.</param>
    /// <param name="innerException">The inner exception, or <see langword="null"/>.</param>
    /// <param name="resourceName">The name of the resource containing the error message.</param>
    /// <param name="arg0">The first argument for formatting the resource string.</param>
    /// <exception cref="InvalidProjectFileException">Always thrown.</exception>
    [DoesNotReturn]
    public static void ThrowInvalidProject(
        DiagnosticSubcategory subcategory,
        IElementLocation location,
        Exception? innerException,
        string resourceName,
        object? arg0)
        => ThrowInvalidProjectCore(subcategory, location, innerException, resourceName, arg0);

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> with a subcategory, an inner exception,
    ///  and two format arguments.
    /// </summary>
    /// <param name="subcategory">The diagnostic subcategory, or <see cref="DiagnosticSubcategory.None"/> for no subcategory.</param>
    /// <param name="location">The location associated with the project-file error.</param>
    /// <param name="innerException">The inner exception, or <see langword="null"/>.</param>
    /// <param name="resourceName">The name of the resource containing the error message.</param>
    /// <param name="arg0">The first argument for formatting the resource string.</param>
    /// <param name="arg1">The second argument for formatting the resource string.</param>
    /// <exception cref="InvalidProjectFileException">Always thrown.</exception>
    [DoesNotReturn]
    public static void ThrowInvalidProject(
        DiagnosticSubcategory subcategory,
        IElementLocation location,
        Exception? innerException,
        string resourceName,
        object? arg0,
        object? arg1)
        => ThrowInvalidProjectCore(subcategory, location, innerException, resourceName, arg0, arg1);

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> with a subcategory, an inner exception,
    ///  and three format arguments.
    /// </summary>
    /// <param name="subcategory">The diagnostic subcategory, or <see cref="DiagnosticSubcategory.None"/> for no subcategory.</param>
    /// <param name="location">The location associated with the project-file error.</param>
    /// <param name="innerException">The inner exception, or <see langword="null"/>.</param>
    /// <param name="resourceName">The name of the resource containing the error message.</param>
    /// <param name="arg0">The first argument for formatting the resource string.</param>
    /// <param name="arg1">The second argument for formatting the resource string.</param>
    /// <param name="arg2">The third argument for formatting the resource string.</param>
    /// <exception cref="InvalidProjectFileException">Always thrown.</exception>
    [DoesNotReturn]
    public static void ThrowInvalidProject(
        DiagnosticSubcategory subcategory,
        IElementLocation location,
        Exception? innerException,
        string resourceName,
        object? arg0,
        object? arg1,
        object? arg2)
        => ThrowInvalidProjectCore(subcategory, location, innerException, resourceName, arg0, arg1, arg2);

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> with a subcategory, an inner exception,
    ///  and formatting arguments.
    /// </summary>
    /// <param name="subcategory">The diagnostic subcategory, or <see cref="DiagnosticSubcategory.None"/> for no subcategory.</param>
    /// <param name="location">The location associated with the project-file error.</param>
    /// <param name="innerException">The inner exception, or <see langword="null"/>.</param>
    /// <param name="resourceName">The name of the resource containing the error message.</param>
    /// <param name="args">The arguments for formatting the resource string.</param>
    /// <exception cref="InvalidProjectFileException">Always thrown.</exception>
    [DoesNotReturn]
    public static void ThrowInvalidProject(
        DiagnosticSubcategory subcategory,
        IElementLocation location,
        Exception? innerException,
        string resourceName,
        params object?[] args)
        => ThrowInvalidProjectCore(subcategory, location, innerException, resourceName, args);

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> without a subcategory when
    ///  <paramref name="condition"/> is <see langword="false"/>.
    /// </summary>
    /// <param name="condition">The condition that must be <see langword="true"/>.</param>
    /// <param name="location">The location associated with the project-file error.</param>
    /// <param name="resourceName">The name of the resource containing the error message.</param>
    /// <exception cref="InvalidProjectFileException"><paramref name="condition"/> is <see langword="false"/>.</exception>
    public static void VerifyThrowInvalidProject([DoesNotReturnIf(false)] bool condition, IElementLocation location, string resourceName)
        => VerifyThrowInvalidProject(condition, DiagnosticSubcategory.None, location, resourceName);

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> with one format argument and no subcategory when
    ///  <paramref name="condition"/> is <see langword="false"/>.
    /// </summary>
    /// <typeparam name="T1">The type of the first format argument.</typeparam>
    /// <param name="condition">The condition that must be <see langword="true"/>.</param>
    /// <param name="location">The location associated with the project-file error.</param>
    /// <param name="resourceName">The name of the resource containing the error message.</param>
    /// <param name="arg0">The first argument for formatting the resource string.</param>
    /// <exception cref="InvalidProjectFileException"><paramref name="condition"/> is <see langword="false"/>.</exception>
    public static void VerifyThrowInvalidProject<T1>(
        [DoesNotReturnIf(false)] bool condition,
        IElementLocation location,
        string resourceName,
        T1 arg0)
        => VerifyThrowInvalidProject(condition, DiagnosticSubcategory.None, location, resourceName, arg0);

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> with two format arguments and no subcategory when
    ///  <paramref name="condition"/> is <see langword="false"/>.
    /// </summary>
    /// <typeparam name="T1">The type of the first format argument.</typeparam>
    /// <typeparam name="T2">The type of the second format argument.</typeparam>
    /// <param name="condition">The condition that must be <see langword="true"/>.</param>
    /// <param name="location">The location associated with the project-file error.</param>
    /// <param name="resourceName">The name of the resource containing the error message.</param>
    /// <param name="arg0">The first argument for formatting the resource string.</param>
    /// <param name="arg1">The second argument for formatting the resource string.</param>
    /// <exception cref="InvalidProjectFileException"><paramref name="condition"/> is <see langword="false"/>.</exception>
    public static void VerifyThrowInvalidProject<T1, T2>(
        [DoesNotReturnIf(false)] bool condition,
        IElementLocation location,
        string resourceName,
        T1 arg0,
        T2 arg1)
        => VerifyThrowInvalidProject(condition, DiagnosticSubcategory.None, location, resourceName, arg0, arg1);

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> with three format arguments and no subcategory when
    ///  <paramref name="condition"/> is <see langword="false"/>.
    /// </summary>
    /// <typeparam name="T1">The type of the first format argument.</typeparam>
    /// <typeparam name="T2">The type of the second format argument.</typeparam>
    /// <typeparam name="T3">The type of the third format argument.</typeparam>
    /// <param name="condition">The condition that must be <see langword="true"/>.</param>
    /// <param name="location">The location associated with the project-file error.</param>
    /// <param name="resourceName">The name of the resource containing the error message.</param>
    /// <param name="arg0">The first argument for formatting the resource string.</param>
    /// <param name="arg1">The second argument for formatting the resource string.</param>
    /// <param name="arg2">The third argument for formatting the resource string.</param>
    /// <exception cref="InvalidProjectFileException"><paramref name="condition"/> is <see langword="false"/>.</exception>
    public static void VerifyThrowInvalidProject<T1, T2, T3>(
        [DoesNotReturnIf(false)] bool condition,
        IElementLocation location,
        string resourceName,
        T1 arg0,
        T2 arg1,
        T3 arg2)
        => VerifyThrowInvalidProject(condition, DiagnosticSubcategory.None, location, resourceName, arg0, arg1, arg2);

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> with four format arguments and no subcategory when
    ///  <paramref name="condition"/> is <see langword="false"/>.
    /// </summary>
    /// <typeparam name="T1">The type of the first format argument.</typeparam>
    /// <typeparam name="T2">The type of the second format argument.</typeparam>
    /// <typeparam name="T3">The type of the third format argument.</typeparam>
    /// <typeparam name="T4">The type of the fourth format argument.</typeparam>
    /// <param name="condition">The condition that must be <see langword="true"/>.</param>
    /// <param name="location">The location associated with the project-file error.</param>
    /// <param name="resourceName">The name of the resource containing the error message.</param>
    /// <param name="arg0">The first argument for formatting the resource string.</param>
    /// <param name="arg1">The second argument for formatting the resource string.</param>
    /// <param name="arg2">The third argument for formatting the resource string.</param>
    /// <param name="arg3">The fourth argument for formatting the resource string.</param>
    /// <exception cref="InvalidProjectFileException"><paramref name="condition"/> is <see langword="false"/>.</exception>
    public static void VerifyThrowInvalidProject<T1, T2, T3, T4>(
        [DoesNotReturnIf(false)] bool condition,
        IElementLocation location,
        string resourceName,
        T1 arg0,
        T2 arg1,
        T3 arg2,
        T4 arg3)
        => VerifyThrowInvalidProject(condition, DiagnosticSubcategory.None, location, resourceName, arg0, arg1, arg2, arg3);

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> with formatting arguments and no subcategory when
    ///  <paramref name="condition"/> is <see langword="false"/>.
    /// </summary>
    /// <param name="condition">The condition that must be <see langword="true"/>.</param>
    /// <param name="location">The location associated with the project-file error.</param>
    /// <param name="resourceName">The name of the resource containing the error message.</param>
    /// <param name="args">The arguments for formatting the resource string.</param>
    /// <exception cref="InvalidProjectFileException"><paramref name="condition"/> is <see langword="false"/>.</exception>
    public static void VerifyThrowInvalidProject(
        [DoesNotReturnIf(false)] bool condition,
        IElementLocation location,
        string resourceName,
        params object?[] args)
    {
        if (!condition)
        {
            ThrowInvalidProjectCore(DiagnosticSubcategory.None, location, innerException: null, resourceName, args);
        }
    }

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> with an inner exception and no subcategory when
    ///  <paramref name="condition"/> is <see langword="false"/>.
    /// </summary>
    /// <param name="condition">The condition that must be <see langword="true"/>.</param>
    /// <param name="location">The location associated with the project-file error.</param>
    /// <param name="innerException">The inner exception, or <see langword="null"/>.</param>
    /// <param name="resourceName">The name of the resource containing the error message.</param>
    /// <exception cref="InvalidProjectFileException"><paramref name="condition"/> is <see langword="false"/>.</exception>
    public static void VerifyThrowInvalidProject(
        [DoesNotReturnIf(false)] bool condition,
        IElementLocation location,
        Exception? innerException,
        string resourceName)
        => VerifyThrowInvalidProject(condition, DiagnosticSubcategory.None, location, innerException, resourceName);

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> with an inner exception, one format argument,
    ///  and no subcategory when <paramref name="condition"/> is <see langword="false"/>.
    /// </summary>
    /// <typeparam name="T1">The type of the first format argument.</typeparam>
    /// <param name="condition">The condition that must be <see langword="true"/>.</param>
    /// <param name="location">The location associated with the project-file error.</param>
    /// <param name="innerException">The inner exception, or <see langword="null"/>.</param>
    /// <param name="resourceName">The name of the resource containing the error message.</param>
    /// <param name="arg0">The first argument for formatting the resource string.</param>
    /// <exception cref="InvalidProjectFileException"><paramref name="condition"/> is <see langword="false"/>.</exception>
    public static void VerifyThrowInvalidProject<T1>(
        [DoesNotReturnIf(false)] bool condition,
        IElementLocation location,
        Exception? innerException,
        string resourceName,
        T1 arg0)
        => VerifyThrowInvalidProject(condition, DiagnosticSubcategory.None, location, innerException, resourceName, arg0);

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> with an inner exception, two format arguments,
    ///  and no subcategory when <paramref name="condition"/> is <see langword="false"/>.
    /// </summary>
    /// <typeparam name="T1">The type of the first format argument.</typeparam>
    /// <typeparam name="T2">The type of the second format argument.</typeparam>
    /// <param name="condition">The condition that must be <see langword="true"/>.</param>
    /// <param name="location">The location associated with the project-file error.</param>
    /// <param name="innerException">The inner exception, or <see langword="null"/>.</param>
    /// <param name="resourceName">The name of the resource containing the error message.</param>
    /// <param name="arg0">The first argument for formatting the resource string.</param>
    /// <param name="arg1">The second argument for formatting the resource string.</param>
    /// <exception cref="InvalidProjectFileException"><paramref name="condition"/> is <see langword="false"/>.</exception>
    public static void VerifyThrowInvalidProject<T1, T2>(
        [DoesNotReturnIf(false)] bool condition,
        IElementLocation location,
        Exception? innerException,
        string resourceName,
        T1 arg0,
        T2 arg1)
        => VerifyThrowInvalidProject(condition, DiagnosticSubcategory.None, location, innerException, resourceName, arg0, arg1);

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> with an inner exception, three format arguments,
    ///  and no subcategory when <paramref name="condition"/> is <see langword="false"/>.
    /// </summary>
    /// <typeparam name="T1">The type of the first format argument.</typeparam>
    /// <typeparam name="T2">The type of the second format argument.</typeparam>
    /// <typeparam name="T3">The type of the third format argument.</typeparam>
    /// <param name="condition">The condition that must be <see langword="true"/>.</param>
    /// <param name="location">The location associated with the project-file error.</param>
    /// <param name="innerException">The inner exception, or <see langword="null"/>.</param>
    /// <param name="resourceName">The name of the resource containing the error message.</param>
    /// <param name="arg0">The first argument for formatting the resource string.</param>
    /// <param name="arg1">The second argument for formatting the resource string.</param>
    /// <param name="arg2">The third argument for formatting the resource string.</param>
    /// <exception cref="InvalidProjectFileException"><paramref name="condition"/> is <see langword="false"/>.</exception>
    public static void VerifyThrowInvalidProject<T1, T2, T3>(
        [DoesNotReturnIf(false)] bool condition,
        IElementLocation location,
        Exception? innerException,
        string resourceName,
        T1 arg0,
        T2 arg1,
        T3 arg2)
        => VerifyThrowInvalidProject(
            condition, DiagnosticSubcategory.None, location, innerException, resourceName, arg0, arg1, arg2);

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> with an inner exception, four format arguments,
    ///  and no subcategory when <paramref name="condition"/> is <see langword="false"/>.
    /// </summary>
    /// <typeparam name="T1">The type of the first format argument.</typeparam>
    /// <typeparam name="T2">The type of the second format argument.</typeparam>
    /// <typeparam name="T3">The type of the third format argument.</typeparam>
    /// <typeparam name="T4">The type of the fourth format argument.</typeparam>
    /// <param name="condition">The condition that must be <see langword="true"/>.</param>
    /// <param name="location">The location associated with the project-file error.</param>
    /// <param name="innerException">The inner exception, or <see langword="null"/>.</param>
    /// <param name="resourceName">The name of the resource containing the error message.</param>
    /// <param name="arg0">The first argument for formatting the resource string.</param>
    /// <param name="arg1">The second argument for formatting the resource string.</param>
    /// <param name="arg2">The third argument for formatting the resource string.</param>
    /// <param name="arg3">The fourth argument for formatting the resource string.</param>
    /// <exception cref="InvalidProjectFileException"><paramref name="condition"/> is <see langword="false"/>.</exception>
    public static void VerifyThrowInvalidProject<T1, T2, T3, T4>(
        [DoesNotReturnIf(false)] bool condition,
        IElementLocation location,
        Exception? innerException,
        string resourceName,
        T1 arg0,
        T2 arg1,
        T3 arg2,
        T4 arg3)
        => VerifyThrowInvalidProject(
            condition, DiagnosticSubcategory.None, location, innerException, resourceName, arg0, arg1, arg2, arg3);

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> with an inner exception, formatting arguments,
    ///  and no subcategory when <paramref name="condition"/> is <see langword="false"/>.
    /// </summary>
    /// <param name="condition">The condition that must be <see langword="true"/>.</param>
    /// <param name="location">The location associated with the project-file error.</param>
    /// <param name="innerException">The inner exception, or <see langword="null"/>.</param>
    /// <param name="resourceName">The name of the resource containing the error message.</param>
    /// <param name="args">The arguments for formatting the resource string.</param>
    /// <exception cref="InvalidProjectFileException"><paramref name="condition"/> is <see langword="false"/>.</exception>
    public static void VerifyThrowInvalidProject(
        [DoesNotReturnIf(false)] bool condition,
        IElementLocation location,
        Exception? innerException,
        string resourceName,
        params object?[] args)
    {
        if (!condition)
        {
            ThrowInvalidProjectCore(DiagnosticSubcategory.None, location, innerException, resourceName, args);
        }
    }

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> with a subcategory when
    ///  <paramref name="condition"/> is <see langword="false"/>.
    /// </summary>
    /// <param name="condition">The condition that must be <see langword="true"/>.</param>
    /// <param name="subcategory">The diagnostic subcategory, or <see cref="DiagnosticSubcategory.None"/> for no subcategory.</param>
    /// <param name="location">The location associated with the project-file error.</param>
    /// <param name="resourceName">The name of the resource containing the error message.</param>
    /// <exception cref="InvalidProjectFileException"><paramref name="condition"/> is <see langword="false"/>.</exception>
    public static void VerifyThrowInvalidProject(
        [DoesNotReturnIf(false)] bool condition,
        DiagnosticSubcategory subcategory,
        IElementLocation location,
        string resourceName)
    {
        if (!condition)
        {
            ThrowInvalidProjectCore(subcategory, location, innerException: null, resourceName);
        }
    }

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> with a subcategory and one format argument when
    ///  <paramref name="condition"/> is <see langword="false"/>.
    /// </summary>
    /// <typeparam name="T1">The type of the first format argument.</typeparam>
    /// <param name="condition">The condition that must be <see langword="true"/>.</param>
    /// <param name="subcategory">The diagnostic subcategory, or <see cref="DiagnosticSubcategory.None"/> for no subcategory.</param>
    /// <param name="location">The location associated with the project-file error.</param>
    /// <param name="resourceName">The name of the resource containing the error message.</param>
    /// <param name="arg0">The first argument for formatting the resource string.</param>
    /// <exception cref="InvalidProjectFileException"><paramref name="condition"/> is <see langword="false"/>.</exception>
    public static void VerifyThrowInvalidProject<T1>(
        [DoesNotReturnIf(false)] bool condition,
        DiagnosticSubcategory subcategory,
        IElementLocation location,
        string resourceName,
        T1 arg0)
    {
        if (!condition)
        {
            ThrowInvalidProjectCore(subcategory, location, innerException: null, resourceName, arg0);
        }
    }

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> with a subcategory and two format arguments when
    ///  <paramref name="condition"/> is <see langword="false"/>.
    /// </summary>
    /// <typeparam name="T1">The type of the first format argument.</typeparam>
    /// <typeparam name="T2">The type of the second format argument.</typeparam>
    /// <param name="condition">The condition that must be <see langword="true"/>.</param>
    /// <param name="subcategory">The diagnostic subcategory, or <see cref="DiagnosticSubcategory.None"/> for no subcategory.</param>
    /// <param name="location">The location associated with the project-file error.</param>
    /// <param name="resourceName">The name of the resource containing the error message.</param>
    /// <param name="arg0">The first argument for formatting the resource string.</param>
    /// <param name="arg1">The second argument for formatting the resource string.</param>
    /// <exception cref="InvalidProjectFileException"><paramref name="condition"/> is <see langword="false"/>.</exception>
    public static void VerifyThrowInvalidProject<T1, T2>(
        [DoesNotReturnIf(false)] bool condition,
        DiagnosticSubcategory subcategory,
        IElementLocation location,
        string resourceName,
        T1 arg0,
        T2 arg1)
    {
        if (!condition)
        {
            ThrowInvalidProjectCore(subcategory, location, innerException: null, resourceName, arg0, arg1);
        }
    }

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> with a subcategory and three format arguments when
    ///  <paramref name="condition"/> is <see langword="false"/>.
    /// </summary>
    /// <typeparam name="T1">The type of the first format argument.</typeparam>
    /// <typeparam name="T2">The type of the second format argument.</typeparam>
    /// <typeparam name="T3">The type of the third format argument.</typeparam>
    /// <param name="condition">The condition that must be <see langword="true"/>.</param>
    /// <param name="subcategory">The diagnostic subcategory, or <see cref="DiagnosticSubcategory.None"/> for no subcategory.</param>
    /// <param name="location">The location associated with the project-file error.</param>
    /// <param name="resourceName">The name of the resource containing the error message.</param>
    /// <param name="arg0">The first argument for formatting the resource string.</param>
    /// <param name="arg1">The second argument for formatting the resource string.</param>
    /// <param name="arg2">The third argument for formatting the resource string.</param>
    /// <exception cref="InvalidProjectFileException"><paramref name="condition"/> is <see langword="false"/>.</exception>
    public static void VerifyThrowInvalidProject<T1, T2, T3>(
        [DoesNotReturnIf(false)] bool condition,
        DiagnosticSubcategory subcategory,
        IElementLocation location,
        string resourceName,
        T1 arg0,
        T2 arg1,
        T3 arg2)
    {
        if (!condition)
        {
            ThrowInvalidProjectCore(subcategory, location, innerException: null, resourceName, arg0, arg1, arg2);
        }
    }

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> with a subcategory and four format arguments when
    ///  <paramref name="condition"/> is <see langword="false"/>.
    /// </summary>
    /// <typeparam name="T1">The type of the first format argument.</typeparam>
    /// <typeparam name="T2">The type of the second format argument.</typeparam>
    /// <typeparam name="T3">The type of the third format argument.</typeparam>
    /// <typeparam name="T4">The type of the fourth format argument.</typeparam>
    /// <param name="condition">The condition that must be <see langword="true"/>.</param>
    /// <param name="subcategory">The diagnostic subcategory, or <see cref="DiagnosticSubcategory.None"/> for no subcategory.</param>
    /// <param name="location">The location associated with the project-file error.</param>
    /// <param name="resourceName">The name of the resource containing the error message.</param>
    /// <param name="arg0">The first argument for formatting the resource string.</param>
    /// <param name="arg1">The second argument for formatting the resource string.</param>
    /// <param name="arg2">The third argument for formatting the resource string.</param>
    /// <param name="arg3">The fourth argument for formatting the resource string.</param>
    /// <exception cref="InvalidProjectFileException"><paramref name="condition"/> is <see langword="false"/>.</exception>
    public static void VerifyThrowInvalidProject<T1, T2, T3, T4>(
        [DoesNotReturnIf(false)] bool condition,
        DiagnosticSubcategory subcategory,
        IElementLocation location,
        string resourceName,
        T1 arg0,
        T2 arg1,
        T3 arg2,
        T4 arg3)
    {
        if (!condition)
        {
            ThrowInvalidProjectCore(subcategory, location, innerException: null, resourceName, arg0, arg1, arg2, arg3);
        }
    }

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> with a subcategory and formatting arguments when
    ///  <paramref name="condition"/> is <see langword="false"/>.
    /// </summary>
    /// <param name="condition">The condition that must be <see langword="true"/>.</param>
    /// <param name="subcategory">The diagnostic subcategory, or <see cref="DiagnosticSubcategory.None"/> for no subcategory.</param>
    /// <param name="location">The location associated with the project-file error.</param>
    /// <param name="resourceName">The name of the resource containing the error message.</param>
    /// <param name="args">The arguments for formatting the resource string.</param>
    /// <exception cref="InvalidProjectFileException"><paramref name="condition"/> is <see langword="false"/>.</exception>
    public static void VerifyThrowInvalidProject(
        [DoesNotReturnIf(false)] bool condition,
        DiagnosticSubcategory subcategory,
        IElementLocation location,
        string resourceName,
        params object?[] args)
    {
        if (!condition)
        {
            ThrowInvalidProjectCore(subcategory, location, innerException: null, resourceName, args);
        }
    }

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> with a subcategory and an inner exception when
    ///  <paramref name="condition"/> is <see langword="false"/>.
    /// </summary>
    /// <param name="condition">The condition that must be <see langword="true"/>.</param>
    /// <param name="subcategory">The diagnostic subcategory, or <see cref="DiagnosticSubcategory.None"/> for no subcategory.</param>
    /// <param name="location">The location associated with the project-file error.</param>
    /// <param name="innerException">The inner exception, or <see langword="null"/>.</param>
    /// <param name="resourceName">The name of the resource containing the error message.</param>
    /// <exception cref="InvalidProjectFileException"><paramref name="condition"/> is <see langword="false"/>.</exception>
    public static void VerifyThrowInvalidProject(
        [DoesNotReturnIf(false)] bool condition,
        DiagnosticSubcategory subcategory,
        IElementLocation location,
        Exception? innerException,
        string resourceName)
    {
        if (!condition)
        {
            ThrowInvalidProjectCore(subcategory, location, innerException, resourceName);
        }
    }

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> with a subcategory, an inner exception,
    ///  and one format argument when <paramref name="condition"/> is <see langword="false"/>.
    /// </summary>
    /// <typeparam name="T1">The type of the first format argument.</typeparam>
    /// <param name="condition">The condition that must be <see langword="true"/>.</param>
    /// <param name="subcategory">The diagnostic subcategory, or <see cref="DiagnosticSubcategory.None"/> for no subcategory.</param>
    /// <param name="location">The location associated with the project-file error.</param>
    /// <param name="innerException">The inner exception, or <see langword="null"/>.</param>
    /// <param name="resourceName">The name of the resource containing the error message.</param>
    /// <param name="arg0">The first argument for formatting the resource string.</param>
    /// <exception cref="InvalidProjectFileException"><paramref name="condition"/> is <see langword="false"/>.</exception>
    public static void VerifyThrowInvalidProject<T1>(
        [DoesNotReturnIf(false)] bool condition,
        DiagnosticSubcategory subcategory,
        IElementLocation location,
        Exception? innerException,
        string resourceName,
        T1 arg0)
    {
        if (!condition)
        {
            ThrowInvalidProjectCore(subcategory, location, innerException, resourceName, arg0);
        }
    }

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> with a subcategory, an inner exception,
    ///  and two format arguments when <paramref name="condition"/> is <see langword="false"/>.
    /// </summary>
    /// <typeparam name="T1">The type of the first format argument.</typeparam>
    /// <typeparam name="T2">The type of the second format argument.</typeparam>
    /// <param name="condition">The condition that must be <see langword="true"/>.</param>
    /// <param name="subcategory">The diagnostic subcategory, or <see cref="DiagnosticSubcategory.None"/> for no subcategory.</param>
    /// <param name="location">The location associated with the project-file error.</param>
    /// <param name="innerException">The inner exception, or <see langword="null"/>.</param>
    /// <param name="resourceName">The name of the resource containing the error message.</param>
    /// <param name="arg0">The first argument for formatting the resource string.</param>
    /// <param name="arg1">The second argument for formatting the resource string.</param>
    /// <exception cref="InvalidProjectFileException"><paramref name="condition"/> is <see langword="false"/>.</exception>
    public static void VerifyThrowInvalidProject<T1, T2>(
        [DoesNotReturnIf(false)] bool condition,
        DiagnosticSubcategory subcategory,
        IElementLocation location,
        Exception? innerException,
        string resourceName,
        T1 arg0,
        T2 arg1)
    {
        if (!condition)
        {
            ThrowInvalidProjectCore(subcategory, location, innerException, resourceName, arg0, arg1);
        }
    }

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> with a subcategory, an inner exception,
    ///  and three format arguments when <paramref name="condition"/> is <see langword="false"/>.
    /// </summary>
    /// <typeparam name="T1">The type of the first format argument.</typeparam>
    /// <typeparam name="T2">The type of the second format argument.</typeparam>
    /// <typeparam name="T3">The type of the third format argument.</typeparam>
    /// <param name="condition">The condition that must be <see langword="true"/>.</param>
    /// <param name="subcategory">The diagnostic subcategory, or <see cref="DiagnosticSubcategory.None"/> for no subcategory.</param>
    /// <param name="location">The location associated with the project-file error.</param>
    /// <param name="innerException">The inner exception, or <see langword="null"/>.</param>
    /// <param name="resourceName">The name of the resource containing the error message.</param>
    /// <param name="arg0">The first argument for formatting the resource string.</param>
    /// <param name="arg1">The second argument for formatting the resource string.</param>
    /// <param name="arg2">The third argument for formatting the resource string.</param>
    /// <exception cref="InvalidProjectFileException"><paramref name="condition"/> is <see langword="false"/>.</exception>
    public static void VerifyThrowInvalidProject<T1, T2, T3>(
        [DoesNotReturnIf(false)] bool condition,
        DiagnosticSubcategory subcategory,
        IElementLocation location,
        Exception? innerException,
        string resourceName,
        T1 arg0,
        T2 arg1,
        T3 arg2)
    {
        if (!condition)
        {
            ThrowInvalidProjectCore(subcategory, location, innerException, resourceName, arg0, arg1, arg2);
        }
    }

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> with a subcategory, an inner exception,
    ///  and four format arguments when <paramref name="condition"/> is <see langword="false"/>.
    /// </summary>
    /// <typeparam name="T1">The type of the first format argument.</typeparam>
    /// <typeparam name="T2">The type of the second format argument.</typeparam>
    /// <typeparam name="T3">The type of the third format argument.</typeparam>
    /// <typeparam name="T4">The type of the fourth format argument.</typeparam>
    /// <param name="condition">The condition that must be <see langword="true"/>.</param>
    /// <param name="subcategory">The diagnostic subcategory, or <see cref="DiagnosticSubcategory.None"/> for no subcategory.</param>
    /// <param name="location">The location associated with the project-file error.</param>
    /// <param name="innerException">The inner exception, or <see langword="null"/>.</param>
    /// <param name="resourceName">The name of the resource containing the error message.</param>
    /// <param name="arg0">The first argument for formatting the resource string.</param>
    /// <param name="arg1">The second argument for formatting the resource string.</param>
    /// <param name="arg2">The third argument for formatting the resource string.</param>
    /// <param name="arg3">The fourth argument for formatting the resource string.</param>
    /// <exception cref="InvalidProjectFileException"><paramref name="condition"/> is <see langword="false"/>.</exception>
    public static void VerifyThrowInvalidProject<T1, T2, T3, T4>(
        [DoesNotReturnIf(false)] bool condition,
        DiagnosticSubcategory subcategory,
        IElementLocation location,
        Exception? innerException,
        string resourceName,
        T1 arg0,
        T2 arg1,
        T3 arg2,
        T4 arg3)
    {
        if (!condition)
        {
            ThrowInvalidProjectCore(subcategory, location, innerException, resourceName, arg0, arg1, arg2, arg3);
        }
    }

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> with a subcategory, an inner exception,
    ///  and formatting arguments when <paramref name="condition"/> is <see langword="false"/>.
    /// </summary>
    /// <param name="condition">The condition that must be <see langword="true"/>.</param>
    /// <param name="subcategory">The diagnostic subcategory, or <see cref="DiagnosticSubcategory.None"/> for no subcategory.</param>
    /// <param name="location">The location associated with the project-file error.</param>
    /// <param name="innerException">The inner exception, or <see langword="null"/>.</param>
    /// <param name="resourceName">The name of the resource containing the error message.</param>
    /// <param name="args">The arguments for formatting the resource string.</param>
    /// <exception cref="InvalidProjectFileException"><paramref name="condition"/> is <see langword="false"/>.</exception>
    public static void VerifyThrowInvalidProject(
        [DoesNotReturnIf(false)] bool condition,
        DiagnosticSubcategory subcategory,
        IElementLocation location,
        Exception? innerException,
        string resourceName,
        params object?[] args)
    {
        if (!condition)
        {
            ThrowInvalidProjectCore(subcategory, location, innerException, resourceName, args);
        }
    }

    /// <summary>
    ///  Formats a project-file error without format arguments and throws an <see cref="InvalidProjectFileException"/>.
    /// </summary>
    /// <param name="subcategory">The diagnostic subcategory, or <see cref="DiagnosticSubcategory.None"/> for no subcategory.</param>
    /// <param name="location">The location associated with the project-file error.</param>
    /// <param name="innerException">The inner exception, or <see langword="null"/>.</param>
    /// <param name="resourceName">The name of the resource containing the error message.</param>
    /// <exception cref="InvalidProjectFileException">Always thrown.</exception>
    [DoesNotReturn]
    private static void ThrowInvalidProjectCore(
        DiagnosticSubcategory subcategory,
        IElementLocation location,
        Exception? innerException,
        string resourceName)
    {
        Assumed.NotNull(location);
        ResourceUtilities.VerifyResourceStringExists(resourceName);

        string message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(
            out string? errorCode, out string? helpKeyword, resourceName);

        ThrowInvalidProjectCore(subcategory, location, innerException, message, errorCode, helpKeyword);
    }

    /// <summary>
    ///  Formats a project-file error with one format argument and throws an <see cref="InvalidProjectFileException"/>.
    /// </summary>
    /// <param name="subcategory">The diagnostic subcategory, or <see cref="DiagnosticSubcategory.None"/> for no subcategory.</param>
    /// <param name="location">The location associated with the project-file error.</param>
    /// <param name="innerException">The inner exception, or <see langword="null"/>.</param>
    /// <param name="resourceName">The name of the resource containing the error message.</param>
    /// <param name="arg0">The first argument for formatting the resource string.</param>
    /// <exception cref="InvalidProjectFileException">Always thrown.</exception>
    [DoesNotReturn]
    private static void ThrowInvalidProjectCore(
        DiagnosticSubcategory subcategory,
        IElementLocation location,
        Exception? innerException,
        string resourceName,
        object? arg0)
    {
        Assumed.NotNull(location);
        ResourceUtilities.VerifyResourceStringExists(resourceName);

        string message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(
            out string? errorCode, out string? helpKeyword, resourceName, arg0);

        ThrowInvalidProjectCore(subcategory, location, innerException, message, errorCode, helpKeyword);
    }

    /// <summary>
    ///  Formats a project-file error with two format arguments and throws an <see cref="InvalidProjectFileException"/>.
    /// </summary>
    /// <param name="subcategory">The diagnostic subcategory, or <see cref="DiagnosticSubcategory.None"/> for no subcategory.</param>
    /// <param name="location">The location associated with the project-file error.</param>
    /// <param name="innerException">The inner exception, or <see langword="null"/>.</param>
    /// <param name="resourceName">The name of the resource containing the error message.</param>
    /// <param name="arg0">The first argument for formatting the resource string.</param>
    /// <param name="arg1">The second argument for formatting the resource string.</param>
    /// <exception cref="InvalidProjectFileException">Always thrown.</exception>
    [DoesNotReturn]
    private static void ThrowInvalidProjectCore(
        DiagnosticSubcategory subcategory,
        IElementLocation location,
        Exception? innerException,
        string resourceName,
        object? arg0,
        object? arg1)
    {
        Assumed.NotNull(location);
        ResourceUtilities.VerifyResourceStringExists(resourceName);

        string message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(
            out string? errorCode, out string? helpKeyword, resourceName, arg0, arg1);

        ThrowInvalidProjectCore(subcategory, location, innerException, message, errorCode, helpKeyword);
    }

    /// <summary>
    ///  Formats a project-file error with three format arguments and throws an <see cref="InvalidProjectFileException"/>.
    /// </summary>
    /// <param name="subcategory">The diagnostic subcategory, or <see cref="DiagnosticSubcategory.None"/> for no subcategory.</param>
    /// <param name="location">The location associated with the project-file error.</param>
    /// <param name="innerException">The inner exception, or <see langword="null"/>.</param>
    /// <param name="resourceName">The name of the resource containing the error message.</param>
    /// <param name="arg0">The first argument for formatting the resource string.</param>
    /// <param name="arg1">The second argument for formatting the resource string.</param>
    /// <param name="arg2">The third argument for formatting the resource string.</param>
    /// <exception cref="InvalidProjectFileException">Always thrown.</exception>
    [DoesNotReturn]
    private static void ThrowInvalidProjectCore(
        DiagnosticSubcategory subcategory,
        IElementLocation location,
        Exception? innerException,
        string resourceName,
        object? arg0,
        object? arg1,
        object? arg2)
    {
        Assumed.NotNull(location);
        ResourceUtilities.VerifyResourceStringExists(resourceName);

        string message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(
            out string? errorCode, out string? helpKeyword, resourceName, arg0, arg1, arg2);

        ThrowInvalidProjectCore(subcategory, location, innerException, message, errorCode, helpKeyword);
    }

    /// <summary>
    ///  Formats a project-file error with an argument array and throws an <see cref="InvalidProjectFileException"/>.
    /// </summary>
    /// <param name="subcategory">The diagnostic subcategory, or <see cref="DiagnosticSubcategory.None"/> for no subcategory.</param>
    /// <param name="location">The location associated with the project-file error.</param>
    /// <param name="innerException">The inner exception, or <see langword="null"/>.</param>
    /// <param name="resourceName">The name of the resource containing the error message.</param>
    /// <param name="args">The arguments for formatting the resource string.</param>
    /// <exception cref="InvalidProjectFileException">Always thrown.</exception>
    [DoesNotReturn]
    private static void ThrowInvalidProjectCore(
        DiagnosticSubcategory subcategory,
        IElementLocation location,
        Exception? innerException,
        string resourceName,
        params object?[] args)
    {
        Assumed.NotNull(location);
        ResourceUtilities.VerifyResourceStringExists(resourceName);

        string message = ResourceUtilities.FormatResourceStringStripCodeAndKeyword(
            out string? errorCode, out string? helpKeyword, resourceName, args);

        ThrowInvalidProjectCore(subcategory, location, innerException, message, errorCode, helpKeyword);
    }

    /// <summary>
    ///  Throws an <see cref="InvalidProjectFileException"/> from formatted diagnostic information.
    /// </summary>
    /// <param name="subcategory">The diagnostic subcategory, or <see cref="DiagnosticSubcategory.None"/> for no subcategory.</param>
    /// <param name="location">The location associated with the project-file error.</param>
    /// <param name="innerException">The inner exception, or <see langword="null"/>.</param>
    /// <param name="message">The formatted error message.</param>
    /// <param name="errorCode">The error code, or <see langword="null"/>.</param>
    /// <param name="helpKeyword">The help keyword, or <see langword="null"/>.</param>
    /// <exception cref="InvalidProjectFileException">Always thrown.</exception>
    [DoesNotReturn]
    private static void ThrowInvalidProjectCore(
        DiagnosticSubcategory subcategory,
        IElementLocation location,
        Exception? innerException,
        string message,
        string? errorCode,
        string? helpKeyword)
        => throw new InvalidProjectFileException(
            location.File,
            location.Line,
            location.Column,
            endLineNumber: 0,
            endColumnNumber: 0,
            message,
            subcategory.GetDisplayString(),
            errorCode,
            helpKeyword,
            innerException);
}
