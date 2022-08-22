// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable disable

namespace Microsoft.Build.Globbing.Visitor
{
    internal abstract class GlobVisitor
    {
        public void Visit(IMSBuildGlob glob)
        {
            if (glob is MSBuildGlob msbuildGlob)
            {
                VisitMSBuildGlob(msbuildGlob);
            }

            if (glob is CompositeGlob compositeGlob)
            {
                VisitCompositeGlob(compositeGlob);

                foreach (var globPart in compositeGlob.Globs)
                {
                    Visit(globPart);
                }
            }

            if (glob is MSBuildGlobWithGaps globWithGaps)
            {
                VisitGlobWithGaps(globWithGaps);

                Visit(globWithGaps.MainGlob);
            }
        }

        protected virtual void VisitGlobWithGaps(MSBuildGlobWithGaps globWithGaps)
        {
        }

        protected virtual void VisitCompositeGlob(CompositeGlob compositeGlob)
        {
        }

        protected virtual void VisitMSBuildGlob(MSBuildGlob msbuildGlob)
        {
        }
    }
}
