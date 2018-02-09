// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.AspNetCore.Razor.Language
{
    internal abstract class RazorParserFeatureFlags
    {
        public static RazorParserFeatureFlags Create(RazorLanguageVersion version)
        {
            bool allowMinimizedBooleanTagHelperAttributes = version.CompareTo(RazorLanguageVersion.Version_2_1) >= 0;

            return new DefaultRazorParserFeatureFlags(allowMinimizedBooleanTagHelperAttributes);
        }

        public abstract bool AllowMinimizedBooleanTagHelperAttributes { get; }

        private class DefaultRazorParserFeatureFlags : RazorParserFeatureFlags
        {
            public DefaultRazorParserFeatureFlags(bool allowMinimizedBooleanTagHelperAttributes)
            {
                AllowMinimizedBooleanTagHelperAttributes = allowMinimizedBooleanTagHelperAttributes;
            }

            public override bool AllowMinimizedBooleanTagHelperAttributes { get; }
        }
    }
}
