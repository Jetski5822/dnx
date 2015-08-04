using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.Dnx.Compilation.Caching;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Runtime.Compilation;
using Microsoft.Dnx.Runtime.Infrastructure;

namespace Microsoft.Dnx.Compilation
{
    public class CompilationSession : ICompilationSession
    {
        private readonly Dictionary<TypeInformation, IProjectCompilerFactory> _compilerFactories = new Dictionary<TypeInformation, IProjectCompilerFactory>();

        private readonly LibraryManager _libraryManager;
        private readonly IProjectGraphProvider _projectGraphProvider;
        private readonly IApplicationEnvironment _applicationEnvironment;
        private readonly IAssemblyLoadContextFactory _loadContextFactory;
        private readonly IServiceProvider _services;
        private readonly Lazy<IAssemblyLoadContext> _compilerLoadContext;

        public CompilationSession(
            ICache cache, 
            ICacheContextAccessor cacheContextAccessor, 
            INamedCacheDependencyProvider namedCacheDependencyProvider, 
            IApplicationEnvironment applicationEnvironment,
            IAssemblyLoadContextFactory loadContextFactory,
            IFileWatcher fileWatcher,
            LibraryManager libraryManager, 
            IProjectGraphProvider projectGraphProvider,
            IServiceProvider services)
        {
            _services = services;
            _libraryManager = libraryManager;
            _projectGraphProvider = projectGraphProvider;
            _applicationEnvironment = applicationEnvironment;
            _loadContextFactory = loadContextFactory;
            _compilerLoadContext = new Lazy<IAssemblyLoadContext>(() => _loadContextFactory.Create(services));
            LibraryExporter = new LibraryExporter(libraryManager, this, projectGraphProvider);

            Cache = cache;
            CacheContextAccessor = cacheContextAccessor;
            NamedCacheDependencyProvider = namedCacheDependencyProvider;
            FileWatcher = fileWatcher;
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
            var factory = _compilerFactories.GetOrAdd(provider, typeInfo =>
            {
                var factoryType = _compilerLoadContext.Value.Load(typeInfo.AssemblyName).GetType(typeInfo.TypeName);
                return (IProjectCompilerFactory)Activator.CreateInstance(factoryType);
            });
            return factory.CreateCompiler(
                Cache,
                CacheContextAccessor,
                NamedCacheDependencyProvider,
                _loadContextFactory,
                FileWatcher,
                _applicationEnvironment,
                _services);
        }
    }
}
