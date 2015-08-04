using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Dnx.Runtime.Infrastructure;

namespace Microsoft.Dnx.Runtime.Compilation
{
    /// <summary>
    /// Provides an interface to the Compilation Engine used to compile and load projects
    /// </summary>
    public interface ICompilationEngine
    {
        /// <summary>
        /// Creates a new <see cref="ICompilationSession"/> for the provided runtime graph.
        /// </summary>
        /// <param name="libraryManager">A <see cref="LibraryManager"/> containing the primary dependency graph for the compilation</param>
        /// <param name="projectGraphProvider">A <see cref="IProjectGraphProvider"/> that can be used to retrieve dependency graphs for projects referenced during this compilation</param>
        /// <returns></returns>
        ICompilationSession CreateSession(
            LibraryManager libraryManager,
            IProjectGraphProvider projectGraphProvider,
            IServiceProvider services);
    }
}
