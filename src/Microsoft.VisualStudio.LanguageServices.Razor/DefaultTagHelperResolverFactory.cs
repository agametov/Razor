// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Razor;

namespace Microsoft.VisualStudio.LanguageServices.Razor
{
    [ExportLanguageServiceFactory(typeof(TagHelperResolver), RazorLanguage.Name, ServiceLayer.Default)]
    internal class DefaultTagHelperResolverFactory : ILanguageServiceFactory
    {
        private readonly Lazy<ICustomTemplateEngineFactory, ICustomTemplateEngineFactoryMetadata>[] _customFactories;

        [ImportingConstructor]
        public DefaultTagHelperResolverFactory([ImportMany] IEnumerable<Lazy<ICustomTemplateEngineFactory, ICustomTemplateEngineFactoryMetadata>> customFactories)
        {
            if (customFactories == null)
            {
                throw new ArgumentNullException(nameof(customFactories));
            }

            _customFactories = customFactories.ToArray();

        }
        public ILanguageService CreateLanguageService(HostLanguageServices languageServices)
        {
            return new DefaultTagHelperResolver(
                languageServices.WorkspaceServices.GetRequiredService<ErrorReporter>(),
                languageServices.WorkspaceServices.Workspace,
                _customFactories,
                languageServices.GetRequiredService<RazorTemplateEngineFactoryService>());
        }
    }
}