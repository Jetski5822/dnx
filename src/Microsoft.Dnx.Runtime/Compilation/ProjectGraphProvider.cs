using System;
using System.Collections.Generic;

namespace Microsoft.Dnx.Runtime
{
    public class ProjectGraphProvider : IProjectGraphProvider
    {
        private readonly IServiceProvider _hostServices;

        public ProjectGraphProvider(IServiceProvider hostServices)
        {
            _hostServices = hostServices;
        }

        public IEnumerable<RuntimeLibrary> GetProjectGraph(Project project, CompilationTarget target)
        {
            // TODO: Cache sub-graph walk?

            // Create a child app context for this graph walk
            var context = new ApplicationHostContext(
                _hostServices,
                project.ProjectDirectory,
                packagesDirectory: null,
                configuration: target.Configuration,
                targetFramework: target.TargetFramework);

            // Walk the graph
            context.DependencyWalker.Walk(project.Name, project.Version, target.TargetFramework);

            // Return the results
            return context.DependencyWalker.Libraries;
        }
    }
}