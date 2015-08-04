using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.Dnx.Compilation.Caching;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Runtime.Common.DependencyInjection;
using Microsoft.Dnx.Runtime.Compilation;
using Microsoft.Dnx.Runtime.Infrastructure;

namespace Microsoft.Dnx.Compilation
{
    public class CompilationSession : ICompilationSession
    {
        private readonly Dictionary<TypeInformation, IProjectCompiler> _compilers = new Dictionary<TypeInformation, IProjectCompiler>();

        private readonly IProjectGraphProvider _projectGraphProvider;
        private readonly IServiceProvider _services;
        private readonly Lazy<IAssemblyLoadContext> _compilerLoadContext;

        public CompilationSession(
            ICache cache, 
            ICacheContextAccessor cacheContextAccessor, 
            INamedCacheDependencyProvider namedCacheDependencyProvider, 
            IFileWatcher fileWatcher,
            LibraryManager libraryManager, 
            IProjectGraphProvider projectGraphProvider,
            IServiceProvider runtimeServices)
        {
            _projectGraphProvider = projectGraphProvider;
            LibraryExporter = new LibraryExporter(libraryManager, this, projectGraphProvider);
            _compilerLoadContext = new Lazy<IAssemblyLoadContext>(() =>
            {
                var factory = (IAssemblyLoadContextFactory)_services.GetService(typeof(IAssemblyLoadContextFactory));
                return factory.Create(_services);
            });

            Cache = cache;
            CacheContextAccessor = cacheContextAccessor;
            NamedCacheDependencyProvider = namedCacheDependencyProvider;
            FileWatcher = fileWatcher;

            // Register compiler services
            // TODO(anurse): Switch to project factory model to avoid needing to do this.
            var services = new ServiceProvider(runtimeServices);
            services.Add(typeof(ICache), cache);
            services.Add(typeof(ICacheContextAccessor), cacheContextAccessor);
            services.Add(typeof(INamedCacheDependencyProvider), namedCacheDependencyProvider);
            services.Add(typeof(IFileWatcher), fileWatcher);
            _services = services;
        }

        public ICache Cache { get; }

        public ICacheContextAccessor CacheContextAccessor { get; }

        public INamedCacheDependencyProvider NamedCacheDependencyProvider { get; }

        public IFileWatcher FileWatcher { get; }

        public LibraryExporter LibraryExporter { get; }

        public event Action<string> OnInputFileChanged;

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public Assembly CompileAndLoadProject(Project project, CompilationTarget target, IAssemblyLoadContext loadContext)
        {
            // Export the project
            var export = ProjectExporter.ExportProject(project, target, this, _projectGraphProvider);

            // Load the metadata reference
            foreach (var projectReference in export.MetadataReferences.OfType<IMetadataProjectReference>())
            {
                if (string.Equals(projectReference.Name, project.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return projectReference.Load(loadContext);
                }
            }

            return null;
        }

        public IProjectCompiler GetCompiler(TypeInformation provider)
        {
            // Load the factory
            return _compilers.GetOrAdd(provider, typeInfo =>
                CompilerServices.CreateService<IProjectCompiler>(_services, _compilerLoadContext.Value, typeInfo));
        }
    }
}
