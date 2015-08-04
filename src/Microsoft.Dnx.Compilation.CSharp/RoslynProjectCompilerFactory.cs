using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Dnx.Compilation.Caching;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Runtime.Infrastructure;

namespace Microsoft.Dnx.Compilation.CSharp
{
    public class RoslynProjectCompilerFactory : IProjectCompilerFactory
    {
        public IProjectCompiler CreateCompiler(
            ICache cache, 
            ICacheContextAccessor cacheContextAccessor, 
            INamedCacheDependencyProvider namedCacheProvider, 
            IAssemblyLoadContextFactory loadContextFactory, 
            IFileWatcher watcher, 
            IApplicationEnvironment environment, 
            IServiceProvider services)
        {
            return new RoslynProjectCompiler(
                cache,
                cacheContextAccessor,
                namedCacheProvider,
                loadContextFactory,
                watcher,
                environment,
                services);
        }
    }
}
