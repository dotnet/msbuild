// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.Globbing.Visitor
{
    internal abstract class GlobVisitor
    {
        public void Visit(IMSBuildGlob glob)
        {
            var msbuildGlob = glob as MSBuildGlob;
            if (msbuildGlob != null)
            {
                VisitMSBuildGlob(msbuildGlob);
            }

            var compositGlob = glob as CompositeGlob;
            if (compositGlob != null)
            {
                VisitCompositeGlob(compositGlob);

                foreach (var globPart in compositGlob.Globs)
                {
                    Visit(globPart);
                }
            }

            var globWithGaps = glob as MSBuildGlobWithGaps;
            if (globWithGaps != null)
            {
                VisitGlobWithGaps(globWithGaps);

                Visit(globWithGaps.MainGlob);
            }
        }

        protected virtual void VisitGlobWithGaps(MSBuildGlobWithGaps globWithGaps)
        {
        }

        protected virtual void VisitCompositeGlob(CompositeGlob compositGlob)
        {
        }

        protected virtual void VisitMSBuildGlob(MSBuildGlob msbuildGlob)
        {
        }
    }
}