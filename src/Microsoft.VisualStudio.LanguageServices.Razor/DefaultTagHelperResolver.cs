// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.VisualStudio.LanguageServices.Razor.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.VisualStudio.LanguageServices.Razor
{
    internal class DefaultTagHelperResolver : TagHelperResolver
    {
        private readonly ErrorReporter _errorReporter;
        private readonly Workspace _workspace;
        private readonly Lazy<ICustomTemplateEngineFactory, ICustomTemplateEngineFactoryMetadata>[] _customFactories;
        private readonly RazorTemplateEngineFactoryService _factory;

        public DefaultTagHelperResolver(
            ErrorReporter errorReporter,
            Workspace workspace,
            Lazy<ICustomTemplateEngineFactory, ICustomTemplateEngineFactoryMetadata>[] customFactories,
            RazorTemplateEngineFactoryService factory)
        {
            _errorReporter = errorReporter;
            _workspace = workspace;
            _customFactories = customFactories;
            _factory = factory;
        }

        public async override Task<TagHelperResolutionResult> GetTagHelpersAsync(
            ProjectSnapshot project, 
            CancellationToken cancellationToken = default)
        {
            if (project == null)
            {
                throw new ArgumentNullException(nameof(project));
            }

            if (!project.IsInitialized || project.Configuration == null)
            {
                return TagHelperResolutionResult.Empty;
            }

            bool supportsSerialization = false;
            ICustomTemplateEngineFactory selected = null;
            for (var i = 0; i < _customFactories.Length; i++)
            {
                var customFactory = _customFactories[i];
                if (string.Equals(project.Configuration.ConfigurationName, customFactory.Metadata.ConfigurationName))
                {
                    selected = customFactory.Value;
                    supportsSerialization = customFactory.Metadata.SupportsSerialization;
                    break;
                }
            }

            TagHelperResolutionResult result = null;
            if (selected == null || supportsSerialization)
            {
                result = await GetTagHelpersOutOfProcessAsync(selected, project);
                if (result != null)
                {
                    return result;
                }
            }

            try
            {   
                // fall back to in process if needed.
                result = await GetTagHelpersInProcessAsync(selected, project);
                return result;
            }
            catch (Exception exception)
            {
                _errorReporter.ReportError(exception, project.WorkspaceProject);

                throw new InvalidOperationException(
                    Resources.FormatUnexpectedException(
                        typeof(DefaultTagHelperResolver).FullName,
                        nameof(GetTagHelpersAsync)),
                    exception);
            }
        }

        private async Task<TagHelperResolutionResult> GetTagHelpersOutOfProcessAsync(
            ICustomTemplateEngineFactory factory, 
            ProjectSnapshot project)
        {
            // We're being overly defensive here because the OOP host can return null for the client/session/operation
            // when it's disconnected (user stops the process).
            //
            // This will change in the future to an easier to consume API but for VS RTM this is what we have.
            var client = await RazorLanguageServiceClientFactory.CreateAsync(_workspace, CancellationToken.None);
            if (client != null)
            {
                using (var session = await client.CreateSessionAsync(project.WorkspaceProject.Solution))
                {
                    if (session != null)
                    {
                        var args = new object[]
                        {
                            project.WorkspaceProject.Id.Id,
                            project.WorkspaceProject.Name,
                            factory == null ? null : factory.GetType().AssemblyQualifiedName,
                            Serialize(project),
                        };

                        var json = await session.InvokeAsync<JObject>("GetTagHelpersAsync", args, CancellationToken.None).ConfigureAwait(false);
                        var result = Deserialize(json);
                        if (result != null)
                        {
                            return result;
                        }
                    }
                }
            }

            return null;
        }

        private async Task<TagHelperResolutionResult> GetTagHelpersInProcessAsync(
            ICustomTemplateEngineFactory factory,
            ProjectSnapshot project)
        {
            RazorTemplateEngine templateEngine;
            if (factory == null)
            {
                var engine = RazorEngine.CreateCore(project.Configuration, (b) =>
                {
                    b.Features.Add(new DefaultTagHelperDescriptorProvider() { DesignTime = true });
                });
                templateEngine =  new RazorTemplateEngine(engine, EmptyProject.Instance);
            }
            else
            {
                templateEngine = factory.Create(project.Configuration, EmptyProject.Instance, (b) =>
                {
                    b.Features.Add(new DefaultTagHelperDescriptorProvider() { DesignTime = true, });
                });
            }
            
            var descriptors = new List<TagHelperDescriptor>();

            var providers = templateEngine.Engine.Features.OfType<ITagHelperDescriptorProvider>().ToArray();

            var results = new List<TagHelperDescriptor>();
            var context = TagHelperDescriptorProviderContext.Create(results);
            context.SetCompilation(await project.WorkspaceProject.GetCompilationAsync());

            for (var i = 0; i < providers.Length; i++)
            {
                var provider = providers[i];
                provider.Execute(context);
            }

            var diagnostics = new List<RazorDiagnostic>();
            var resolutionResult = new TagHelperResolutionResult(results, diagnostics);

            return resolutionResult;
        }

        private JObject Serialize(ProjectSnapshot snapshot)
        {
            var serializer = new JsonSerializer();
            serializer.Converters.Add(RazorExtensionJsonConverter.Instance);
            serializer.Converters.Add(RazorConfigurationJsonConverter.Instance);
            serializer.Converters.Add(ProjectSnapshotJsonConverter.Instance);

            return JObject.FromObject(snapshot, serializer);
        }

        private TagHelperResolutionResult Deserialize(JObject jsonObject)
        {
            var serializer = new JsonSerializer();
            serializer.Converters.Add(TagHelperDescriptorJsonConverter.Instance);
            serializer.Converters.Add(RazorDiagnosticJsonConverter.Instance);
            serializer.Converters.Add(RazorExtensionJsonConverter.Instance);
            serializer.Converters.Add(RazorConfigurationJsonConverter.Instance);
            serializer.Converters.Add(ProjectSnapshotJsonConverter.Instance);

            using (var reader = jsonObject.CreateReader())
            {
                return serializer.Deserialize<TagHelperResolutionResult>(reader);
            }
        }

        private class EmptyProject : RazorProject
        {
            public static readonly EmptyProject Instance = new EmptyProject();

            public override IEnumerable<RazorProjectItem> EnumerateItems(string basePath)
            {
                return Array.Empty<RazorProjectItem>();
            }

            public override RazorProjectItem GetItem(string path)
            {
                return null;
            }
        }
    }
}
