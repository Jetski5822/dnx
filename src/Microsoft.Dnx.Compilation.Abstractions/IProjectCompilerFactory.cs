using System;
using Microsoft.Dnx.Compilation.Caching;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Runtime.Infrastructure;

namespace Microsoft.Dnx.Compilation
{
    public interface IProjectCompilerFactory
    {
        IProjectCompiler CreateCompiler(
            ICache cache,
            ICacheContextAccessor cacheContextAccessor,
            INamedCacheDependencyProvider namedCacheProvider,
            IAssemblyLoadContextFactory loadContextFactory,
            IFileWatcher watcher,
            IApplicationEnvironment environment,
            IServiceProvider services);
    }
}
