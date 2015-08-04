using System;
using System.Collections.Generic;
using System.Reflection;

namespace Microsoft.Dnx.Runtime.Compilation
{
    public interface ICompilationSession : IDisposable
    {
        event Action<string> OnInputFileChanged;

        Assembly CompileAndLoadProject(Project project, CompilationTarget target, IAssemblyLoadContext loadContext);
    }
}
