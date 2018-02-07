// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.VisualStudio.LanguageServices.Razor.Serialization;

namespace Microsoft.CodeAnalysis.Remote.Razor
{
    internal class RazorLanguageService : ServiceHubServiceBase
    {
        public RazorLanguageService(Stream stream, IServiceProvider serviceProvider)
            : base(serviceProvider, stream)
        {
            Rpc.JsonSerializer.Converters.Add(TagHelperDescriptorJsonConverter.Instance);
            Rpc.JsonSerializer.Converters.Add(RazorDiagnosticJsonConverter.Instance);
            Rpc.JsonSerializer.Converters.Add(RazorExtensionJsonConverter.Instance);
            Rpc.JsonSerializer.Converters.Add(RazorConfigurationJsonConverter.Instance);
            Rpc.JsonSerializer.Converters.Add(ProjectSnapshotJsonConverter.Instance);

            // Due to this issue - https://github.com/dotnet/roslyn/issues/16900#issuecomment-277378950
            // We need to manually start the RPC connection. Otherwise we'd be opting ourselves into 
            // race condition prone call paths.
            Rpc.StartListening();
        }

        public async Task<TagHelperResolutionResult> GetTagHelpersAsync(
            Guid projectIdBytes, 
            string projectDebugName, 
            string factoryTypeName, 
            ProjectSnapshot snapshot, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var projectId = ProjectId.CreateFromSerialized(projectIdBytes, projectDebugName);

            var solution = await GetSolutionAsync(cancellationToken).ConfigureAwait(false);
            var project = solution.GetProject(projectId);

            ((SerializedProjectSnapshot)snapshot).InitializeWorkspaceProject(project);

            RazorTemplateEngine templateEngine = null;
            if (factoryTypeName != null)
            {
                var factoryType = Type.GetType(factoryTypeName, throwOnError: true);
                var factory = (ICustomTemplateEngineFactory)Activator.CreateInstance(factoryType);

                templateEngine = factory.Create(snapshot.Configuration, EmptyProject.Instance, (b) => 
                {
                    b.Features.Add(new DefaultTagHelperDescriptorProvider() { DesignTime = true });
                });
            }

            if (templateEngine == null)
            {
                templateEngine = CreateFallbackEngine(snapshot.Configuration);
            }

            var descriptors = new List<TagHelperDescriptor>();

            var providers = templateEngine.Engine.Features.OfType<ITagHelperDescriptorProvider>().ToArray();

            var results = new List<TagHelperDescriptor>();
            var context = TagHelperDescriptorProviderContext.Create(results);
            context.SetCompilation(await snapshot.WorkspaceProject.GetCompilationAsync());

            for (var i = 0; i < providers.Length; i++)
            {
                var provider = providers[i];
                provider.Execute(context);
            }

            var diagnostics = new List<RazorDiagnostic>();
            var resolutionResult = new TagHelperResolutionResult(results, diagnostics);

            return resolutionResult;
        }

        public Task<IEnumerable<DirectiveDescriptor>> GetDirectivesAsync(Guid projectIdBytes, string projectDebugName, CancellationToken cancellationToken = default(CancellationToken))
        {
            var projectId = ProjectId.CreateFromSerialized(projectIdBytes, projectDebugName);

            var engine = RazorEngine.Create();
            var directives = engine.Features.OfType<IRazorDirectiveFeature>().FirstOrDefault()?.Directives;
            return Task.FromResult(directives ?? Enumerable.Empty<DirectiveDescriptor>());
        }

        public Task<GeneratedDocument> GenerateDocumentAsync(Guid projectIdBytes, string projectDebugName, string filePath, string text, CancellationToken cancellationToken = default(CancellationToken))
        {
            var projectId = ProjectId.CreateFromSerialized(projectIdBytes, projectDebugName);

            var engine = RazorEngine.Create();

            RazorSourceDocument source;
            using (var stream = new MemoryStream())
            {
                var bytes = Encoding.UTF8.GetBytes(text);
                stream.Write(bytes, 0, bytes.Length);

                stream.Seek(0L, SeekOrigin.Begin);
                source = RazorSourceDocument.ReadFrom(stream, filePath, Encoding.UTF8);
            }

            var code = RazorCodeDocument.Create(source);
            engine.Process(code);

            var csharp = code.GetCSharpDocument();
            if (csharp == null)
            {
                throw new InvalidOperationException();
            }

            return Task.FromResult(new GeneratedDocument() { Text = csharp.GeneratedCode, });
        }

        private RazorTemplateEngine CreateFallbackEngine(RazorConfiguration configuration)
        {
            var engine = RazorEngine.CreateCore(configuration, (b) => 
            {
                b.Features.Add(new DefaultTagHelperDescriptorProvider() { DesignTime = true });
            });
            return new RazorTemplateEngine(engine, EmptyProject.Instance);
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
