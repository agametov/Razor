// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.VisualStudio.Editor.Razor
{
    internal class DefaultTemplateEngineFactoryService : RazorTemplateEngineFactoryService
    {
        private readonly static RazorConfiguration DefaultConfiguration = FallbackRazorConfiguration.MVC_2_0;

        private readonly ProjectSnapshotManager _projectManager;
        private readonly Lazy<ICustomTemplateEngineFactory, ICustomTemplateEngineFactoryMetadata>[] _factories;

        public DefaultTemplateEngineFactoryService(
           ProjectSnapshotManager projectManager,
           Lazy<ICustomTemplateEngineFactory, ICustomTemplateEngineFactoryMetadata>[] customFactories)
        {
            if (projectManager == null)
            {
                throw new ArgumentNullException(nameof(projectManager));
            }

            if (customFactories == null)
            {
                throw new ArgumentNullException(nameof(customFactories));
            }

            _projectManager = projectManager;
            _factories = customFactories;
        }

        public override RazorTemplateEngine Create(string projectPath, Action<IRazorEngineBuilder> configure)
        {
            if (projectPath == null)
            {
                throw new ArgumentNullException(nameof(projectPath));
            }

            // In 15.5 we expect projectPath to be a directory, NOT the path to the csproj.
            var project = FindProject(projectPath);
            var configuration = project?.Configuration ?? DefaultConfiguration;

            for (var i = 0; i < _factories.Length; i++)
            {
                var factory = _factories[i];
                if (string.Equals(configuration.ConfigurationName, factory.Metadata.ConfigurationName))
                {
                    return factory.Value.Create(configuration, RazorProject.Create(projectPath), configure ?? ((b) => { }));
                }
            }

            // If there's no factory to handle the configuration then fall back to a very basic configuration.
            //
            // This will stop a crash from happening in this case (misconfigured project), but will still make
            // it obvious to the user that something is wrong.
            var engine = RazorEngine.CreateCore(configuration, b =>
            {
                configure?.Invoke(b);
            });

            var templateEngine = new RazorTemplateEngine(engine, RazorProject.Create(projectPath));
            templateEngine.Options.ImportsFileName = "_ViewImports.cshtml";
            return templateEngine;
        }

        private ProjectSnapshot FindProject(string directory)
        {
            directory = NormalizeDirectoryPath(directory);

            var projects = _projectManager.Projects;
            for (var i = 0; i < projects.Count; i++)
            {
                var project = projects[i];
                if (project.WorkspaceProject?.FilePath != null)
                {
                    if (string.Equals(directory, NormalizeDirectoryPath(Path.GetDirectoryName(project.WorkspaceProject.FilePath)), StringComparison.OrdinalIgnoreCase))
                    {
                        return project;
                    }
                }
            }

            return null;
        }

        private string NormalizeDirectoryPath(string path)
        {
            return path.Replace('\\', '/').TrimEnd('/');
        }
    }
}
