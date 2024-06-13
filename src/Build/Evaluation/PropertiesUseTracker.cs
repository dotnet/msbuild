// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.BuildCheck.Infrastructure;
using Microsoft.Build.Collections;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

#nullable enable

namespace Microsoft.Build.Evaluation;

/// <summary>
/// This class tracks reads of properties - so that it can detect uninitialized usages
///  and so that it can forward the accessing information to further interested consumers (e.g. BuildCheck).
/// </summary>
internal sealed class PropertiesUseTracker
{
    internal LoggingContext? LoggingContext { get; init; }

    public PropertiesUseTracker(LoggingContext? loggingContext) => LoggingContext = loggingContext;

    /// <summary>
    /// Whether to warn when we set a property for the first time, after it was previously used.
    /// Default is false, unless MSBUILDWARNONUNINITIALIZEDPROPERTY is set.
    /// </summary>
    // This setting may change after the build has started, therefore if the user has not set the property to true on the build parameters we need to check to see if it is set to true on the environment variable.
    private bool _warnForUninitializedProperties = BuildParameters.WarnOnUninitializedProperty || Traits.Instance.EscapeHatches.WarnOnUninitializedProperty;

    /// <summary>
    /// Lazily allocated collection of properties and the element which used them.
    /// </summary>
    private Dictionary<string, IElementLocation>? _properties;

    internal void TrackRead(string propertyName, int startIndex, int endIndex, IElementLocation elementLocation, bool isUninitialized, bool isArtificial)
    {
        if (isArtificial)
        {
            return;
        }

        // LoggingContext can be null e.g. for initial toolset resolving and reading - we'll miss those expansions in our tracking
        LoggingContext?.ProcessPropertyRead(new PropertyReadInfo(propertyName, startIndex, endIndex,
            elementLocation, isUninitialized, GetPropertyReadContext(propertyName, startIndex, endIndex)));

        if (!isUninitialized)
        {
            return;
        }

        // We have evaluated a property to null. We now need to see if we need to add it to the list of properties which are used before they have been initialized
        //
        // We also do not want to add the property to the list if the environment variable is not set, also we do not want to add the property to the list if we are currently
        // evaluating a condition because a common pattern for msbuild projects is to see if the property evaluates to empty and then set a value as this would cause a considerable number of false positives.   <A Condition="'$(A)' == ''">default</A>
        //
        // Another pattern used is where a property concatenates with other values,  <a>$(a);something</a> however we do not want to add the a element to the list because again this would make a number of
        // false positives. Therefore we check to see what element we are currently evaluating and if it is the same as our property we do not add the property to the list.

        // here handle null probably (or otherwise execution)
        if (_warnForUninitializedProperties && CurrentlyEvaluatingPropertyElementName != null)
        {
            // Check to see if the property name does not match the property we are currently evaluating, note the property we are currently evaluating in the element name, this means no $( or )
            if (!MSBuildNameIgnoreCaseComparer.Default.Equals(CurrentlyEvaluatingPropertyElementName, propertyName, startIndex, endIndex - startIndex + 1))
            {
                TryAdd(
                    propertyName: propertyName.Substring(startIndex, endIndex - startIndex + 1),
                    elementLocation);
            }
        }
    }

    private PropertyReadContext GetPropertyReadContext(string propertyName, int startIndex, int endIndex)
    {
        if (PropertyReadContext == PropertyReadContext.PropertyEvaluation &&
            !string.IsNullOrEmpty(CurrentlyEvaluatingPropertyElementName) &&
            MSBuildNameIgnoreCaseComparer.Default.Equals(CurrentlyEvaluatingPropertyElementName, propertyName,
                startIndex, endIndex - startIndex + 1))
        {
            return PropertyReadContext.PropertyEvaluationSelf;
        }

        return PropertyReadContext;
    }

    internal void TryAdd(string propertyName, IElementLocation elementLocation)
    {
        if (_properties is null)
        {
            _properties = new(StringComparer.OrdinalIgnoreCase);
        }
        else if (_properties.ContainsKey(propertyName))
        {
            return;
        }

        _properties.Add(propertyName, elementLocation);
    }

    internal bool TryGetPropertyElementLocation(string propertyName, [NotNullWhen(returnValue: true)] out IElementLocation? elementLocation)
    {
        if (_properties is null)
        {
            elementLocation = null;
            return false;
        }

        return _properties.TryGetValue(propertyName, out elementLocation);
    }

    internal void RemoveProperty(string propertyName)
    {
        _properties?.Remove(propertyName);
    }

    /// <summary>
    ///  What is the currently evaluating property element, this is so that we do not add a un initialized property if we are evaluating that property.
    /// </summary>
    internal string? CurrentlyEvaluatingPropertyElementName
    {
        get;
        set;
    }

    internal void CheckPreexistingUndefinedUsage(IPropertyElementWithLocation propertyElement, string evaluatedValue, LoggingContext loggingContext)
    {
        // If we are going to set a property to a value other than null or empty we need to check to see if it has been used
        // during evaluation.
        if (evaluatedValue.Length > 0 && _warnForUninitializedProperties)
        {
            // Is the property we are currently setting in the list of properties which have been used but not initialized
            IElementLocation? elementWhichUsedProperty;
            bool isPropertyInList = TryGetPropertyElementLocation(propertyElement.Name, out elementWhichUsedProperty);

            if (isPropertyInList)
            {
                // Once we are going to warn for a property once, remove it from the list so we do not add it again.
                RemoveProperty(propertyElement.Name);
                loggingContext.LogWarning(null, new BuildEventFileInfo(propertyElement.Location), "UsedUninitializedProperty", propertyElement.Name, elementWhichUsedProperty?.LocationString);
            }
        }

        CurrentlyEvaluatingPropertyElementName = null;
        PropertyReadContext = PropertyReadContext.Other;
    }

    private PropertyReadContext _propertyReadContext;
    private PropertyReadContext _previousPropertyReadContext = PropertyReadContext.Other;
    internal PropertyReadContext PropertyReadContext
    {
        private get => _propertyReadContext;
        set
        {
            _previousPropertyReadContext = _propertyReadContext;
            _propertyReadContext = value;
        }
    }

    internal void ResetPropertyReadContext(bool pop = true)
    {
        _propertyReadContext = pop ? _previousPropertyReadContext : PropertyReadContext.Other;
        _previousPropertyReadContext = PropertyReadContext.Other;
    }
}

/// <summary>
/// Type of the context in which a property is read.
/// </summary>
internal enum PropertyReadContext
{
    // we are not interested in distinguishing the item read etc.
    Other,
    ConditionEvaluation,
    ConditionEvaluationWithOneSideEmpty,
    PropertyEvaluation,
    PropertyEvaluationSelf,
}
