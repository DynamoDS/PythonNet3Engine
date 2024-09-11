using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Dynamo.Extensions;
using Dynamo.Graph.Workspaces;
using Dynamo.Logging;
using Dynamo.PythonServices;

namespace DSPythonNet3Extension
{
    public class DSPythonNet3Extension : IExtension, ILogSource
    {
        private const string PythonEvaluatorAssembly = "DSPythonNet3";

        #region ILogSource

        public event Action<ILogMessage> MessageLogged;
        internal void OnMessageLogged(ILogMessage msg)
        {
            if (this.MessageLogged != null)
            {
                MessageLogged?.Invoke(msg);
            }
        }
        #endregion

        public string UniqueId => "D3E5984C-9930-464D-B7B6-9CC0703945DE";

        public string Name => "DSPythonNet3Extension";

        public void Dispose()
        {
            
        }

        /// <summary>
        /// Action to be invoked when the Dynamo has started up and is ready
        /// for user interaction.   
        /// </summary>
        /// <param name="rp"></param>
        public void Ready(ReadyParams rp)
        {
            var extraPath = Path.Combine(new FileInfo(Assembly.GetExecutingAssembly().Location).Directory.Parent.FullName, "extra");
            var alc = new IsolatedPythonContext(Path.Combine(extraPath, $"{PythonEvaluatorAssembly}.dll"));
            var assem = alc.LoadFromAssemblyName(new AssemblyName(PythonEvaluatorAssembly));

            //load the engine into Dynamo ourselves.
            LoadPythonEngine(assem);

            if (rp.CurrentWorkspaceModel is HomeWorkspaceModel hwm)
            {
                foreach (var n in hwm.Nodes)
                {
                    n.MarkNodeAsModified(true);
                }
                hwm.Run();
            }
        }

        private static void LoadPythonEngine(Assembly assembly)
        {
            if (assembly == null)
            {
                throw new ArgumentNullException($"Error while loading python engine - assembly {PythonEvaluatorAssembly}.dll was not loaded successfully.");
            }

            // Currently we are using try-catch to validate loaded assembly and Singleton Instance method exist
            // but we can optimize by checking all loaded types against evaluators interface later
            try
            {
                Type eType = null;
                PropertyInfo instanceProp = null;
                try
                {
                    eType = assembly.GetTypes().FirstOrDefault(x => typeof(PythonEngine).IsAssignableFrom(x) && !x.IsInterface && !x.IsAbstract);
                    if (eType == null) return;

                    instanceProp = eType?.GetProperty("Instance", BindingFlags.NonPublic | BindingFlags.Static);
                    if (instanceProp == null) return;
                }
                catch
                {
                    // Ignore exceptions from iterating assembly types.
                    return;
                }

                PythonEngine engine = (PythonEngine)instanceProp.GetValue(null);
                if (engine == null)
                {
                    throw new Exception($"Could not get a valid PythonEngine instance by calling the {eType.Name}.Instance method");
                }

                if (PythonEngineManager.Instance.AvailableEngines.All(x => x.Name != engine.Name))
                {
                    PythonEngineManager.Instance.AvailableEngines.Add(engine);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to add a Python engine from assembly {assembly.GetName().Name}.dll with error: {ex.Message}");
            }
        }

        public void Shutdown()
        {

        }

        public void Startup(StartupParams sp)
        {
        }
    }
    internal class IsolatedPythonContext : AssemblyLoadContext
    {
        private AssemblyDependencyResolver resolver;

        public IsolatedPythonContext(string libPath)
        {
            resolver = new AssemblyDependencyResolver(libPath);
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            string assemblyPath = resolver.ResolveAssemblyToPath(assemblyName);
            if (assemblyPath != null)
            {
                return LoadFromAssemblyPath(assemblyPath);
            }

            return null;
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            string libraryPath = resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            if (libraryPath != null)
            {
                return LoadUnmanagedDllFromPath(libraryPath);
            }

            return IntPtr.Zero;
        }
    }
}
