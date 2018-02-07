// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.CodeAnalysis.Razor
{
    public interface ICustomTemplateEngineFactory
    {
        RazorTemplateEngine Create(RazorConfiguration configuration, RazorProject project, Action<IRazorEngineBuilder> configure);
    }
}