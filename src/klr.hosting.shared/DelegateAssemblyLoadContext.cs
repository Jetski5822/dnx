﻿#if K10
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Hosting.Loader;

namespace klr.hosting
{
    public class DelegateAssemblyLoadContext : AssemblyLoadContext
    {
        private Func<AssemblyName, Assembly> _loaderCallback;

        public DelegateAssemblyLoadContext(Func<AssemblyName, Assembly> loaderCallback)
        {
            _loaderCallback = loaderCallback;
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            return _loaderCallback(assemblyName);
        }

        public Assembly LoadFile(string path)
        {
            return LoadFromFile(path);
        }

        public Assembly LoadBytes(byte[] assemblyBytes, byte[] pdbBytes)
        {
            if (pdbBytes == null)
            {
                return LoadFromStream(new MemoryStream(assemblyBytes));
            }

            return LoadFromStream(new MemoryStream(assemblyBytes), new MemoryStream(pdbBytes));
        }
    }
}
#endif