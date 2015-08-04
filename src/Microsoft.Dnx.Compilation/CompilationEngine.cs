using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.Dnx.Compilation.Caching;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Runtime.Compilation;
using Microsoft.Dnx.Runtime.Infrastructure;

namespace Microsoft.Dnx.Compilation
{
    public class CompilationEngine : ICompilationEngine
    {
        private readonly CacheContextAccessor _cacheContextAccessor;

        public CompilationEngine(IFileWatcher fileWatcher)
        {
            _cacheContextAccessor = new CacheContextAccessor();
            Cache = new Cache(_cacheContextAccessor);
            NamedCacheDependencyProvider = new NamedCacheDependencyProvider();
            FileWatcher = fileWatcher;
        }

        public ICache Cache { get; }

        public IFileWatcher FileWatcher { get; }

        public INamedCacheDependencyProvider NamedCacheDependencyProvider { get; }

        public ICompilationSession CreateSession(
            LibraryManager libraryManager, 
            IProjectGraphProvider projectGraphProvider,
            IServiceProvider services)
        {
            return new CompilationSession(
                Cache,
                _cacheContextAccessor,
                NamedCacheDependencyProvider,
                FileWatcher,
                libraryManager,
                projectGraphProvider,
                services);
        }
    }
}
