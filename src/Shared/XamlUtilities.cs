using System;
using System.Xml;
using System.Text.RegularExpressions;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework.XamlTypes;

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// This class contains utility methods for Xaml types.
    /// </summary>
    static internal class XamlUtilities
    {
        /// <summary>
        /// Gets an identifier for a property based on its name, its containing object's name and/or type. This is intended to
        /// help the user zero in on the offending line/element in a xaml file, when we don't have access to a parser to
        /// report line numbers.
        /// </summary>
        /// <param name="propertyName"> The name of the property. </param>
        /// <param name="containingObjectName"> The name of the containing object. </param>
        /// <param name="containingObject"> The object which contains this property. </param>
        /// <returns> Returns "(containingObject's type name)containingObjectName.PropertyName". </returns>
        internal static string GetPropertyId(string propertyName, string containingObjectName, object containingObject)
        {
            ErrorUtilities.VerifyThrowArgumentLength(propertyName, "propertyName");
            ErrorUtilities.VerifyThrowArgumentLength(containingObjectName, "containingObjectName");
            ErrorUtilities.VerifyThrowArgumentNull(containingObject, "containingObject");

            StringBuilder propertyId = new StringBuilder();

            propertyId.Append("(");
            propertyId.Append(containingObject.GetType().Name);
            propertyId.Append(")");

            if (!string.IsNullOrEmpty(containingObjectName))
            {
                propertyId.Append(containingObjectName);
            }

            propertyId.Append(".");

            propertyId.Append(propertyName);

            return propertyId.ToString();
        }

        /// <summary>
        /// Returns an identifier for a property based on its name and its containing object's type. Use this
        /// overload when the containing object's name is not known (which can be the case when the property
        /// being tested is the Name property itself).
        /// </summary>
        /// <param name="propertyName"> The name of the property. </param>
        /// <param name="containingObject"> The object which contains this property. </param>
        /// <returns> Returns "(containingObject's type name)unknown.PropertyName". </returns>
        internal static string GetPropertyId(string propertyName, object containingObject)
        {
            string propertyId = GetPropertyId(propertyName, "unknown", containingObject);
            return propertyId;
        }

        /// <summary>
        /// Throws an <see cref="ArgumentNullException"/> if the given property is null.
        /// </summary>
        /// <param name="property">The property to test.</param>
        /// <param name="propertyId">An identifier of the property to check.</param>
        internal static void VerifyThrowPropertyNotSet(object property, string propertyId)
        {
            ErrorUtilities.VerifyThrowArgumentLength(propertyId, "propertyId");

            if (property == null)
            {
                ErrorUtilities.VerifyThrowArgumentNull(null, propertyId, Strings.PropertyValueMustBeSet);
            }
        }

        /// <summary>
        /// Throws an <see cref="ArgumentNullException"/> if the given property is null.
        /// </summary>
        /// <param name="property">The property to test.</param>
        /// <param name="propertyId">An identifier of the property to check.</param>
        /// <param name="unformattedMessage"> The text message to display. </param>
        internal static void VerifyThrowPropertyNotSet(object property, string propertyId, string unformattedMessage)
        {
            ErrorUtilities.VerifyThrowArgumentLength(propertyId, "propertyId");

            if (property == null)
            {
                ErrorUtilities.VerifyThrowArgumentNull(null, propertyId, unformattedMessage);
            }
        }

        /// <summary>
        /// Throws an <see cref="ArgumentException"/> if the given property's value is the empty string.
        /// </summary>
        /// <param name="property">The parameter to test.</param>
        /// <param name="propertyId">An identifier of the property to check.</param>
        internal static void VerifyThrowPropertyEmptyString(string property, string propertyId)
        {
            ErrorUtilities.VerifyThrowArgumentNull(property, "property");
            ErrorUtilities.VerifyThrowArgumentLength(propertyId, "propertyId");

            if (property.Length == 0)
            {
                ErrorUtilities.ThrowArgument(Strings.PropertyCannotBeSetToTheEmptyString, propertyId);
            }
        }

        /// <summary>
        /// Throws an <see cref="ArgumentNullException"/> if the given property is null and
        /// <see cref="ArgumentException"/> if the given property's value is the empty string.
        /// </summary>
        /// <param name="property">The parameter to test.</param>
        /// <param name="propertyId">An identifier of the property to check.</param>
        internal static void VerifyThrowPropertyNotSetOrEmptyString(string property, string propertyId)
        {
            VerifyThrowPropertyNotSet(property, propertyId);
            VerifyThrowPropertyEmptyString(property, propertyId);
        }

        /// <summary>
        /// Throws an <see cref="ArgumentException"/> if the given list property has zero elements.
        /// </summary>
        /// <param name="listProperty"> The list parameter to test. </param>
        /// <param name="propertyId"> An identifier of the property to check. </param>
        internal static void VerifyThrowListPropertyEmpty(IList listProperty, string propertyId)
        {
            ErrorUtilities.VerifyThrowArgumentNull(listProperty, "listProperty");
            ErrorUtilities.VerifyThrowArgumentLength(propertyId, "propertyId");

            if (listProperty.Count == 0)
            {
                ErrorUtilities.ThrowArgument(Strings.ListPropertyShouldHaveAtLeastOneElement, propertyId);
            }
        }

        #region Extension Methods

        /// <summary>
        /// Validates the properties of this object. This method should be called
        /// after initialization is complete.
        /// </summary>
        internal void Validate(this Argument type)
        {
            string propertyId = GetPropertyId("Property", type);
            VerifyThrowPropertyNotSetOrEmptyString(type.Property, propertyId);
        }

        /// <summary>
        /// Validates the properties of this object. This method should be called
        /// after initialization is complete.
        /// </summary>
        internal void Validate(this BaseProperty type)
        {
            string namePropertyId = GetPropertyId("Name", type);
            VerifyThrowPropertyNotSetOrEmptyString(type.Name, namePropertyId);

            string categoryPropertyId = GetPropertyId("Category", type.Name, type);
            VerifyThrowPropertyEmptyString(typeCategory, categoryPropertyId);

            // Validate children.
            if (null != type.DataSource)
            {
                type.DataSource.Validate();
            }

            foreach (Argument argument in type.Arguments)
            {
                argument.Validate();
            }

            foreach (ValueEditor editor in type.ValueEditors)
            {
                editor.Validate();
            }

            // Validate any known derivations.
            BoolProperty boolProp = type as BoolProperty;
            if (null != boolProp)
            {
                return;
            }

            DynamicEnumProperty dynamicEnumProp = type as DynamicEnumProperty;
            if (dynamicEnumProp != null)
            {
                dynamicEnumProp.Validate();
                return;
            }

            EnumProperty enumProp = type as EnumProperty;
            if (enumProp != null)
            {
                enumProp.Validate();
                return;
            }

            IntProperty intProp = type as IntProperty;
            if (intProp != null)
            {
                intProp.Validate();
                return;
            }

            StringListProperty stringListProp = type as StringListProperty;
            if (stringListProp != null)
            {
                return;
            }

            StringProperty stringProp = type as StringProperty;
            if (stringProp != null)
            {
                return;
            }

            // Unknown derivation, but that's ok.
        }


        /// <summary>
        /// Validates the properties of this object. This method should be called
        /// after initialization is complete.
        /// </summary>
        internal void Validate(this Category type)
        {
            string namePropertyId = GetPropertyId("Name", type);
            VerifyThrowPropertyNotSetOrEmptyString(type.Name, namePropertyId);
        }


        /// <summary>
        /// Validates the properties of this object. This method should be called
        /// after initialization is complete.
        /// </summary>
        internal void Validate(this DataSource type)
        {
            string persistencePropertyId = GetPropertyId("Persistence", type);
            VerifyThrowPropertyNotSetOrEmptyString(type.Persistence, persistencePropertyId);
        }

        /// <summary>
        /// Validate the content type data integrity afte deserializing from XAML file
        /// </summary>
        internal void Validate(this ContentType type)
        {
            // content type must at least declare name, and msbuild ItemType to be workable at minimum level
            string namePropertyId = GetPropertyId("Name", type);
            VerifyThrowPropertyNotSetOrEmptyString(type.Name, namePropertyId);

            string itemTypePropertyId = GetPropertyId("ItemType", type);
            VerifyThrowPropertyNotSetOrEmptyString(type.ItemType, itemTypePropertyId);
        }

        /// <summary>
        /// Validates the properties of this object. This method should be called
        /// after initialization is complete.
        /// </summary>
        internal void Validate(this DynamicEnumProperty type)
        {
            (type as BaseProperty).Validate();
            ErrorUtilities.VerifyThrowArgumentLength(type.EnumProvider, "EnumProvider");
        }


        /// <summary>
        /// Validates the properties of this object. This method should be called
        /// after initialization is complete.
        /// </summary>
        private void Validate(this EnumProperty type)
        {
            (type as BaseProperty).Validate();

            // Validate that the "Default" field is not set on this property.
            string defaultPropertyId = GetPropertyId("Default", type.Name, type);
            if (null != Default)
            {
                ErrorUtilities.ThrowArgument(Strings.CannotSetDefaultPropertyOnEnumProperty, typeof(EnumProperty).Name, typeof(EnumValue).Name);
            }

            // Make sure that at least one value was defined in AdmissibleValues.
            string admissibleValuesId = GetPropertyId("AdmissibleValues", type.Name, type);
            VerifyThrowListPropertyEmpty(type.AdmissibleValues, admissibleValuesId);

            // Validate that only one of the EnumValues under AdmissibleValues is marked IsDefault.
            string admissibleValuesPropertyId = GetPropertyId("AdmissibleValues", type.Name, type);

            bool seen = false;
            foreach (EnumValue enumValue in type.AdmissibleValues)
            {
                if (enumValue.IsDefault)
                {
                    if (!seen)
                    {
                        seen = true;
                    }
                    else
                    {
                        ErrorUtilities.ThrowArgument(Strings.OnlyOneEnumValueCanBeSetAsDefault, typeof(EnumValue).Name, admissibleValuesPropertyId);
                    }
                }
            }
        }


        /// <summary>
        /// Validates the properties of this object. This method should be called
        /// after initialization is complete.
        /// </summary>
        internal void Validate(this IntProperty type)
        {
            (type as BaseProperty).Validate();

            if (null != type.MaxValue && null != type.MinValue)
            {
                if (type.MinValue > type.MaxValue)
                {
                    string minValuePropertyId = GetPropertyId("MinValue", type.Name, type);
                    ErrorUtilities.ThrowArgument(Strings.MinValueShouldNotBeGreaterThanMaxValue, minValuePropertyId);
                }
            }
        }

        /// <summary>
        /// Validate the content type data integrity afte deserializing from XAML file
        /// </summary>
        internal void Validate(this ItemType type)
        {
            // content type must at least declare name, and msbuild ItemType to be workable at minimum level
            string namePropertyId = GetPropertyId("Name", type);
            VerifyThrowPropertyNotSetOrEmptyString(type.Name, namePropertyId);

            if (type.DisplayName == null)
            {
                type.DisplayName = type.Name;
            }
        }

        /// <summary>
        /// Validates the properties of this object. This method should be called
        /// after initialization is complete.
        /// </summary>
        internal void Validate(this Rule type)
        {
            // Validate "Name" property.
            string namePropertyId = GetPropertyId("Name", type);
            VerifyThrowPropertyNotSetOrEmptyString(type.Name, namePropertyId);

            // Make sure that at least one Property was defined in this Rule.
            string propertiesId = XamlErrorUtilities.GetPropertyId("Properties", Name, this);
            VerifyThrowListPropertyEmpty(type.Properties, propertiesId);

            // Validate the child objects
            foreach (BaseProperty property in type.Properties)
            {
                property.Validate();
            }

            foreach (Category category in type.Categories)
            {
                category.Validate();
            }

            // If the DataSource property is not defined on this Rule, check that a DataSource is 
            // specified locally on every property.
            if (null == type.DataSource)
            {
                foreach (BaseProperty property in type.Properties)
                {
                    string dataSourcePropertyId = GetPropertyId("DataSource", property.Name, property);
                    VerifyThrowPropertyNotSet(property.DataSource, dataSourcePropertyId, Strings.DataSourceMustBeDefinedOnPropertyOrOnRule);
                }
            }
            else
            {
                type.DataSource.Validate();
            }

            // Create a HashSet for O(1) lookup.
            HashSet<string> propertyNames = new HashSet<string>();
            foreach (BaseProperty property in type.Properties)
            {
                if (!propertyNames.Contains(property.Name))
                {
                    propertyNames.Add(property.Name);
                }
            }

            // Validate that every argument refers to a valid property.
            foreach (BaseProperty property in type.Properties)
            {
                if (property.Arguments == null || property.Arguments.Count == 0)
                {
                    continue;
                }

                foreach (Argument argument in property.Arguments)
                {
                    if (!propertyNames.Contains(argument.Property))
                    {
                        ErrorUtilities.ThrowArgument(Strings.PropertyReferredToByArgumentDoesNotExist, argument.Property);
                    }
                }
            }
        }

        /// <summary>
        /// Validates the properties of this object. This method should be called
        /// after initialization is complete.
        /// </summary>
        internal void Validate(this RuleBag type)
        {
            // Make sure that at least one Rule was defined in this RuleBag.
            string rulesId = GetPropertyId("Rules", type);
            VerifyThrowListPropertyEmpty(type.Rules, rulesId);

            foreach (Rule rule in Rules)
            {
                rule.Validate();
            }
        }

        /// <summary>
        /// Validates the properties of this object. This method should be called
        /// after initialization is complete.
        /// </summary>
        internal void Validate(this ValueEditor type)
        {
            string propertyId = GetPropertyId("EditorType", type);
            VerifyThrowPropertyNotSetOrEmptyString(type.EditorType, propertyId);
        }

        #endregion

    }
}
