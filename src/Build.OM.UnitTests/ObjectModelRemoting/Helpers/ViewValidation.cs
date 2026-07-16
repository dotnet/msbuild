// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.UnitTests.OM.ObjectModelRemoting
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Build.Construction;
    using Microsoft.Build.Evaluation;

    internal enum ObjectType
    {
        Real = 1,
        View = 2
    }

    internal class LinkPair<T>
    {
        public LinkPair(T view, T real)
        {
            ViewValidation.VerifyLinkedNotNull(view);
            ViewValidation.VerifyNotLinkedNotNull(real);
            this.View = view;
            this.Real = real;
        }

        public T Get(ObjectType type) => type == ObjectType.Real ? this.Real : this.View;
        public T View { get; }
        public T Real { get; }

        public void VerifyNotSame(LinkPair<T> other)
        {
            Assert.AreNotEqual((object)this.View, (object)other.View);
            Assert.AreNotEqual((object)this.Real, (object)other.Real);
        }

        public void VerifySame(LinkPair<T> other)
        {
            Assert.AreEqual((object)this.View, (object)other.View);
            Assert.AreEqual((object)this.Real, (object)other.Real);
        }

        public void VerifySetter(bool finalValue, Func<T, bool> getter, Action<T, bool> setter)
        {
            var current = getter(this.Real);
            Assert.AreEqual(current, getter(this.View));

            // set via the view
            setter(this.View, !current);

            Assert.AreEqual(!current, getter(this.View));
            Assert.AreEqual(!current, getter(this.Real));

            // set via the real.
            setter(this.Real, current);

            Assert.AreEqual(current, getter(this.View));
            Assert.AreEqual(current, getter(this.Real));

            setter(this.View, finalValue);
            Assert.AreEqual(finalValue, getter(this.View));
            Assert.AreEqual(finalValue, getter(this.Real));
        }

        public void VerifySetter(string newValue, Func<T, string> getter, Action<T, string> setter)
        {
            var newValue1 = newValue.Ver(1);
            var current = getter(this.Real);
            Assert.AreEqual(current, getter(this.View));
            Assert.AreNotEqual(current, newValue);
            Assert.AreNotEqual(current, newValue1);

            // set via the view
            setter(this.View, newValue1);

            Assert.AreEqual(newValue1, getter(this.View));
            Assert.AreEqual(newValue1, getter(this.Real));

            // set via the real.
            setter(this.Real, newValue);

            Assert.AreEqual(newValue, getter(this.View));
            Assert.AreEqual(newValue, getter(this.Real));
            this.Verify();
        }

        public virtual void Verify()
        {
            ViewValidation.VerifyFindType(this.View, this.Real);
        }
    }

    internal sealed class ValidationContext
    {
        public ValidationContext() { }
        public ValidationContext(ProjectPair pair) { this.Pair = pair; }
        public ProjectPair Pair { get; set; }
        public Action<ElementLocation, ElementLocation> ValidateLocation { get; set; }
    }

    internal static partial class ViewValidation
    {
        private static bool VerifyCheckType<T>(object view, object real, ValidationContext context, Action<T, T, ValidationContext> elementValidator)
        {
            if (view is T viewTypedXml)
            {
                Assert.IsTrue(real is T);
                elementValidator(viewTypedXml, (T)real, context);
                return true;
            }
            else
            {
                Assert.IsFalse(real is T);
                return false;
            }
        }

        // "Slow" Verify, probing all known link types
        public static void VerifyFindType(object view, object real, ValidationContext context = null)
        {
            if (view == null && real == null)
            {
                return;
            }

            VerifyLinkedNotNull(view);
            VerifyNotLinkedNotNull(real);

            // construction
            if (VerifyCheckType<ProjectMetadataElement>(view, real, context, Verify))
            {
                return;
            }

            if (VerifyCheckType<ProjectChooseElement>(view, real, context, Verify))
            {
                return;
            }

            if (VerifyCheckType<ProjectWhenElement>(view, real, context, Verify))
            {
                return;
            }

            if (VerifyCheckType<ProjectOtherwiseElement>(view, real, context, Verify))
            {
                return;
            }

            if (VerifyCheckType<ProjectTaskElement>(view, real, context, Verify))
            {
                return;
            }

            if (VerifyCheckType<ProjectOutputElement>(view, real, context, Verify))
            {
                return;
            }

            if (VerifyCheckType<ProjectUsingTaskBodyElement>(view, real, context, Verify))
            {
                return;
            }

            if (VerifyCheckType<ProjectUsingTaskParameterElement>(view, real, context, Verify))
            {
                return;
            }

            if (VerifyCheckType<UsingTaskParameterGroupElement>(view, real, context, Verify))
            {
                return;
            }

            if (VerifyCheckType<ProjectUsingTaskElement>(view, real, context, Verify))
            {
                return;
            }

            if (VerifyCheckType<ProjectTargetElement>(view, real, context, Verify))
            {
                return;
            }

            if (VerifyCheckType<ProjectRootElement>(view, real, context, Verify))
            {
                return;
            }

            if (VerifyCheckType<ProjectExtensionsElement>(view, real, context, Verify))
            {
                return;
            }

            if (VerifyCheckType<ProjectImportElement>(view, real, context, Verify))
            {
                return;
            }

            if (VerifyCheckType<ProjectImportGroupElement>(view, real, context, Verify))
            {
                return;
            }

            if (VerifyCheckType<ProjectItemDefinitionElement>(view, real, context, Verify))
            {
                return;
            }

            if (VerifyCheckType<ProjectItemDefinitionGroupElement>(view, real, context, Verify))
            {
                return;
            }

            if (VerifyCheckType<ProjectItemElement>(view, real, context, Verify))
            {
                return;
            }

            if (VerifyCheckType<ProjectItemGroupElement>(view, real, context, Verify))
            {
                return;
            }

            if (VerifyCheckType<ProjectPropertyElement>(view, real, context, Verify))
            {
                return;
            }

            if (VerifyCheckType<ProjectPropertyGroupElement>(view, real, context, Verify))
            {
                return;
            }

            if (VerifyCheckType<ProjectSdkElement>(view, real, context, Verify))
            {
                return;
            }

            if (VerifyCheckType<ProjectOnErrorElement>(view, real, context, Verify))
            {
                return;
            }

            // evaluation
            if (VerifyCheckType<ProjectProperty>(view, real, context, Verify))
            {
                return;
            }

            if (VerifyCheckType<ProjectMetadata>(view, real, context, Verify))
            {
                return;
            }

            if (VerifyCheckType<ProjectItemDefinition>(view, real, context, Verify))
            {
                return;
            }

            if (VerifyCheckType<ProjectItem>(view, real, context, Verify))
            {
                return;
            }

            if (VerifyCheckType<Project>(view, real, context, Verify))
            {
                return;
            }

            throw new NotImplementedException($"Unknown type:{view.GetType().Name}");
        }

        public static void VerifyMetadata(IEnumerable<KeyValuePair<string, string>> expected, Func<string, string> getMetadata, Func<string, bool> hasMetadata = null)
        {
            if (expected == null)
            {
                return;
            }

            foreach (var md in expected)
            {
                if (hasMetadata != null)
                {
                    Assert.IsTrue(hasMetadata(md.Key));
                }

                Assert.AreEqual(md.Value, getMetadata(md.Key));
            }
        }

        public static void Verify<T>(IEnumerable<T> viewCollection, IEnumerable<T> realCollection, Action<T, T, ValidationContext> validator, ValidationContext context = null)
        {
            if (viewCollection == null && realCollection == null)
            {
                return;
            }

            Assert.IsNotNull(viewCollection);
            Assert.IsNotNull(realCollection);

            var viewXmlList = viewCollection.ToList();
            var realXmlList = realCollection.ToList();
            Assert.AreEqual(realXmlList.Count, viewXmlList.Count);
            for (int i = 0; i < realXmlList.Count; i++)
            {
                validator(viewXmlList[i], realXmlList[i], context);
            }
        }

        public static void Verify<T>(IDictionary<string, T> viewCollection, IDictionary<string, T> realCollection, Action<T, T, ValidationContext> validator, ValidationContext context = null)
        {
            if (viewCollection == null && realCollection == null)
            {
                return;
            }

            Assert.IsNotNull(viewCollection);
            Assert.IsNotNull(realCollection);

            Assert.AreEqual(realCollection.Count, viewCollection.Count);
            foreach (var k in realCollection.Keys)
            {
                Assert.IsTrue(viewCollection.TryGetValue(k, out var vv));
                Assert.IsTrue(realCollection.TryGetValue(k, out var rv));
                validator(vv, rv, context);
            }
        }

        public static void Verify<T>(IEnumerable<T> viewXmlCollection, IEnumerable<T> realXmlCollection, ValidationContext context = null)
        {
            var viewXmlList = viewXmlCollection.ToList();
            var realXmlList = realXmlCollection.ToList();
            Assert.AreEqual(realXmlList.Count, viewXmlList.Count);
            for (int i = 0; i < realXmlList.Count; i++)
            {
                VerifyFindType(viewXmlList[i], realXmlList[i], context);
            }
        }
    }
}
