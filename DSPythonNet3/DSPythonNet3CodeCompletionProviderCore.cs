using Autodesk.DesignScript.Interfaces;
using Dynamo.Logging;
using Dynamo.PythonServices;
using Python.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using PythonEngine = Python.Runtime.PythonEngine;

namespace DSPythonNet3
{
    internal class DSPythonNet3CodeCompletionProviderCore : PythonCodeCompletionProviderCommon, ILogSource, IDisposable
    {
        #region Private members
        // Get clr type if name is an instance of type and if name is not a builtin python type.
        // Builtin python types might have a corresponding clr type but we pythonnet only knows about the python type.
        internal static readonly string clrTypeLookup = "clr.GetClrType({0}) if isinstance({0}, type) and (\"{0}\" not in __builtins__) else None";

        /// <summary>
        /// The engine used for autocompletion.  This essentially keeps
        /// track of the state of the editor, allowing access to variable types and
        /// imported symbols.
        /// </summary>
        private PyModule scope;

        private string uniquePythonScopeName = "uniqueScope";
        private static string globalScopeName = "cPythonCompletionGlobal";
        private static PyModule uniqueScope;

        // Dictionary basic of Python types to instances of those types
        private Dictionary<Type, PyObject> basicPyObjects;

        /// <summary>
        /// The scope used by the engine.  This is where all the loaded symbols
        /// are stored.  It's essentially an environment dictionary.
        /// </summary>
        internal PyModule? Scope
        {
            get { return scope; }
            set { scope = value; }
        }

        private static PyModule GlobalScope;

        private PyModule CreateUniquePythonScope()
        {
            if (uniqueScope == null)
            {
                uniqueScope = Py.CreateScope(uniquePythonScopeName);
                uniqueScope.Exec(@"
import clr
import sys
");
            }

            return uniqueScope;
        }

        private static PyModule CreateGlobalScope()
        {
            var scope = Py.CreateScope(globalScopeName);
            // Allows discoverability of modules by inspecting their attributes
            scope.Exec(@"
import clr
clr.setPreload(True)
");

            return scope;
        }

        private object ExecutePythonScriptCode(string code)
        {
            DSPythonNet3Evaluator.InitializePython();

            using (Py.GIL())
            {
                var result = Scope.Eval(code);
                return DSPythonNet3Evaluator.OutputMarshaler.Marshal(result);
            }
        }

        public new IEnumerable<Tuple<string, string, bool, ExternalCodeCompletionType>> EnumerateMembers(Type type, string name)
        {
            return base.EnumerateMembers(type, name);
        }

        /// <summary>
        /// List all of the members in a PythonObject. 
        /// This method calls into Python objects and thus needs to be wrappd in a GIL block.
        /// </summary>
        /// <param name="pyObject">A reference to the module</param>
        /// <param name="name">The name of the module</param>
        /// <returns>A list of completion data for the module</returns>
        private IEnumerable<Tuple<string, string, bool, ExternalCodeCompletionType>> EnumerateMembers(object pyObject, string name)
        {
            var items = new List<Tuple<string, string, bool, ExternalCodeCompletionType>>();
            var d = pyObject as PyObject;
            if (d == null)
            {
                return items;
            }

            bool isCLRType = false;
            try
            {
                // Usefull when parsing CLR namespaces (ex. System.Collections.)
                isCLRType = d.GetAttr("__class__").ToString().Contains("CLR");
            }
            catch { }

            foreach (var member in d.Dir())
            {
                try
                {
                    var completionType = ExternalCodeCompletionType.Field;

                    var memberName = member.ToString();
                    var attr = d.GetAttr(memberName).ToString();
                    if (isCLRType &&
                        memberName.StartsWith("__") && memberName.EndsWith("__"))
                    {//Ignone python specific members when dealing with wrapped .NET objects
                        continue;
                    }

                    if (attr.IndexOf(method, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        attr.IndexOf(inBuiltMethod, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        attr.IndexOf("function", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        completionType = ExternalCodeCompletionType.Method;
                    }
                    else if (attr.IndexOf("class", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        completionType = ExternalCodeCompletionType.Class;
                    }
                    else if (attr.IndexOf("namespace", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             attr.IndexOf("module", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        completionType = ExternalCodeCompletionType.Namespace;
                    }

                    items.Add(Tuple.Create(member.ToString(), name, false, completionType));
                }
                catch (Exception ex)
                {
                    Log(ex.ToString());
                }
            }

            return items;
        }

        /// <summary>
        /// Lookup a variable in the python environment
        /// </summary>
        /// <param name="name">A name for a type, possibly delimited by periods.</param>
        /// <returns>The Python object</returns>
        private PyObject LookupObject(string name)
        {
            var periodIndex = name.IndexOf('.');
            using (Py.GIL())
            {
                if (periodIndex == -1)
                {
                    if (Scope.TryGet(name, out PyObject varOutput) && varOutput != null)
                    {
                        return varOutput;
                    }
                }

                try
                {
                    // There is an issue with calling Dir() on nested PyObjects (not all members are discovered)
                    // As a workaround we can retrieve the PyObject by evaluating directly in Python
                    return Scope.Eval(string.Format("{0}", name));
                }
                catch
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Generate completion data for the specified text by relying on the autocomplete from Jedi package.
        /// </summary>
        /// <param name="code">The code to parse</param>
        /// <returns>Return a list of IExternalCodeCompletionData </returns>
        private IExternalCodeCompletionData[] GetCompletionDataFromJedi(string code)
        {
            var items = new List<PythonCodeCompletionDataCore>();

            try
            {
                using (Py.GIL())
                using (var jediScope = Py.CreateScope())
                {
                    jediScope.Set("code", code);

                    string jediSnippet = @"
import jedi

def _no_docs_merge_name_docs(*args, **kwargs):
    return str()

jedi.inference.names._merge_name_docs = _no_docs_merge_name_docs

script = jedi.Script(code)
lines = code.split('\n')
line_no = len(lines)
column_no = len(lines[-1])

completions_list = script.complete(line=line_no, column=column_no)

completion_data = []
for c in completions_list:
    completion_data.append({
        'name': c.name,
        'type': c.type,
    })
";

                    jediScope.Exec(jediSnippet);

                    PyObject pyCompletions = jediScope.Get("completion_data");

                    var completionCandidates = pyCompletions.As<List<Dictionary<string, string>>>();

                    string name = GetLastName(code);
                    var completionItems = new List<PythonCodeCompletionDataCore>();
                    foreach (var item in completionCandidates)
                    {
                        string? member = item.GetValueOrDefault("name");
                        if (string.IsNullOrEmpty(member))
                            continue;

                        string? type = item.GetValueOrDefault("type");
                        if (string.IsNullOrEmpty(type))
                            continue;

                        ExternalCodeCompletionType realType = ExternalCodeCompletionType.Field;
                        switch (type.ToLower())
                        {
                            case "module":
                            case "namespace":
                                realType = ExternalCodeCompletionType.Namespace;
                                break;
                            case "class":
                                realType = ExternalCodeCompletionType.Class;
                                break;
                            case "function":
                                realType = ExternalCodeCompletionType.Method;
                                break;
                            case "property":
                                realType = ExternalCodeCompletionType.Property;
                                break;
                            case "statement":
                                realType = ExternalCodeCompletionType.Field;
                                break;
                            default:
                                break;
                        }

                        PythonCodeCompletionDataCore newCompletionItem = new PythonCodeCompletionDataCore(member, name, false, realType, this);
                        items.Add(newCompletionItem);
                    }
                }
            }
            catch { }

            return [.. items];
        }
        #endregion

        #region PythonCodeCompletionProviderCommon public methods implementations

        /// <summary>
        /// Generate completion data for the specified text, while import the given types into the
        /// scope and discovering variable assignments.
        /// </summary>
        /// <param name="code">The code to parse</param>
        /// <param name="expand">Determines if the entire namespace should be used</param>
        /// <returns>Return a list of IExternalCodeCompletionData </returns>
        public override IExternalCodeCompletionData[] GetCompletionData(string code, bool expand = false)
        {
            IEnumerable<PythonCodeCompletionDataCore> items = new List<PythonCodeCompletionDataCore>();

            if (!PythonEngine.IsInitialized)
            {
                PythonEngine.Initialize();
                PythonEngine.BeginAllowThreads();
            }

            try
            {
                using (Py.GIL())
                {
                    if (code.Contains("\"\"\""))
                    {
                        code = StripDocStrings(code);
                    }

                    UpdateImportedTypes(code);
                    UpdateVariableTypes(code);  // Possibly use hindley-milner in the future

                    // If expand param is true use the entire namespace from the line of code
                    // Else just return the last name of the namespace
                    string name = expand ? GetLastNameSpace(code) : GetLastName(code);
                    if (!string.IsNullOrEmpty(name))
                    {
                        try
                        {
                            // Attempt to get type using naming
                            Type type = expand ? TryGetTypeFromFullName(name) : TryGetType(name);
                            // CLR type
                            if (type != null)
                            {
                                items = EnumerateMembers(type, name).Select(x => new PythonCodeCompletionDataCore(x.Item1, x.Item2, x.Item3, x.Item4, this));
                            }
                            // Variable assignment types
                            else if (VariableTypes.TryGetValue(name, out type))
                            {
                                if (basicPyObjects.TryGetValue(type, out PyObject basicObj))
                                {
                                    items = EnumerateMembers(basicObj, name).Select(x => new PythonCodeCompletionDataCore(x.Item1, x.Item2, x.Item3, x.Item4, this));
                                }
                                else
                                {
                                    items = EnumerateMembers(type, name).Select(x => new PythonCodeCompletionDataCore(x.Item1, x.Item2, x.Item3, x.Item4, this));
                                }
                            }
                            else
                            {
                                // Try to find the corresponding PyObject from the python environment
                                // Most types should be successfully retrieved at this stage
                                var pyObj = LookupObject(name);
                                if (pyObj != null)
                                {
                                    items = EnumerateMembers(pyObj, name).Select(x => new PythonCodeCompletionDataCore(x.Item1, x.Item2, x.Item3, x.Item4, this));
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Log(ex.ToString());
                        }
                    }
                }
            }
            catch { }

            // If unable to find matching results and expand was set to false,
            // try again using the full namespace (set expand to true)
            if (!items.Any())
            {
                if (!expand)
                {
                    return GetCompletionData(code, true);
                }
                else
                {
                    return GetCompletionDataFromJedi(code);
                }
            }

            return items.ToArray();
        }

        protected override object GetDescriptionObject(string docCommand)
        {
            try
            {
                return ExecutePythonScriptCode(docCommand);
            }
            catch
            {
                //This empty catch block is intentional- 
                //because we are using a python engine to evaluate the completions
                //but this engine has not actually loaded the types, it will throw lots of exceptions
                //we wish to suppress.
            }

            return null;
        }

        /// <summary>
        /// Used to determine if this IExternalCodeCompletionProviderCore can provide completions for the given engine.
        /// </summary>
        /// <param name="engineName"></param>
        /// <returns></returns>
        public override bool IsSupportedEngine(string engineName)
        {
            if (engineName == "PythonNet3")
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Used to load initialize libraries and types that should be available by default.
        /// </summary>
        /// <param name="dynamoCorePath"></param>
        public override void Initialize(string dynamoCorePath)
        { }
        #endregion

        #region PythonCodeCompletionProviderCommon protected methods implementations
        protected override object EvaluateScript(string script, PythonScriptType evalType)
        {
            using (Py.GIL())
            {
                switch (evalType)
                {
                    case PythonScriptType.Expression:
                        var result = Scope?.Eval(script);
                        return DSPythonNet3Evaluator.OutputMarshaler.Marshal(result);
                    default:
                        Scope?.Exec(script);
                        return null;
                }
            }
        }

        protected override void LogError(string msg)
        {
            Log(msg);
        }

        protected internal override bool ScopeHasVariable(string name)
        {
            using (Py.GIL())
            {
                return Scope.Contains(name);
            }
        }

        protected override Type GetCLRType(string name)
        {
            using (Py.GIL())
            {
                var result = Scope.Eval(string.Format(clrTypeLookup, name));
                return result?.GetManagedObject() as Type;//Get the .Net wrapped object
            }
        }
        #endregion

        #region constructor
        public DSPythonNet3CodeCompletionProviderCore()
        {
            VariableTypes = new Dictionary<string, Type>();
            ImportedTypes = new Dictionary<string, Type>();
            ClrModules = new HashSet<string>();
            BadStatements = new Dictionary<string, int>();
            Guid pythonScopeGUID = Guid.NewGuid();
            uniquePythonScopeName += pythonScopeGUID.ToString();

            // Special case for python variables defined as null
            ImportedTypes["None"] = null;

            BasicVariableTypes = new List<Tuple<Regex, Type>>();

            BasicVariableTypes.Add(Tuple.Create(STRING_VARIABLE, typeof(PyString)));
            BasicVariableTypes.Add(Tuple.Create(DOUBLE_VARIABLE, typeof(PyFloat)));
            BasicVariableTypes.Add(Tuple.Create(INT_VARIABLE, typeof(PyInt)));
            BasicVariableTypes.Add(Tuple.Create(LIST_VARIABLE, typeof(PyList)));
            BasicVariableTypes.Add(Tuple.Create(DICT_VARIABLE, typeof(PyDict)));

            DSPythonNet3Evaluator.InitializePython();

            try
            {
                using (Py.GIL())
                {
                    if (GlobalScope == null)
                    {
                        GlobalScope = CreateGlobalScope();
                    }

                    if (Scope == null)
                    {
                        Scope = CreateUniquePythonScope();
                    }

                    var assemblies = AppDomain.CurrentDomain.GetAssemblies();

                    // Determine if the Revit API is available in the given context
                    if (assemblies.Any(x => x.GetName().Name == "RevitAPI"))
                    {
                        // Try to load Revit Type for autocomplete
                        try
                        {
                            var revitImports =
                                "import clr\nclr.AddReference('RevitAPI')\nclr.AddReference('RevitAPIUI')\nfrom Autodesk.Revit.DB import *\nimport Autodesk\n";

                            Scope.Exec(revitImports);

                            ClrModules.Add("RevitAPI");
                            ClrModules.Add("RevitAPIUI");
                        }
                        catch
                        {
                            Log("Failed to load Revit types for autocomplete. Python autocomplete will not see Autodesk namespace types.");
                        }
                    }

                    // Determine if the ProtoGeometry is available in the given context
                    if (assemblies.Any(x => x.GetName().Name == "ProtoGeometry"))
                    {
                        // Try to load ProtoGeometry Type for autocomplete
                        try
                        {
                            var libGImports =
                                "import clr\nclr.AddReference('ProtoGeometry')\nfrom Autodesk.DesignScript.Geometry import *\n";

                            Scope.Exec(libGImports);
                            ClrModules.Add("ProtoGeometry");
                        }
                        catch (Exception e)
                        {
                            Log(e.ToString());
                            Log("Failed to load ProtoGeometry types for autocomplete. Python autocomplete will not see Autodesk namespace types.");
                        }
                    }

                    // PythonNet evaluates basic types as Python native types:
                    // Ex: a = ""
                    // type(a) will have the value {class str} and not the clr System.String
                    //
                    // Populate the basic types after python is initialized
                    // These instance types will correspond to Python basic types (no clr)
                    // ex. PyString("") => Python's {class str}
                    // PyInt(0) => Python's {class int}
                    basicPyObjects = new Dictionary<Type, PyObject>();
                    basicPyObjects[typeof(PyString)] = new PyString("");
                    basicPyObjects[typeof(PyFloat)] = new PyFloat(0);
                    basicPyObjects[typeof(PyInt)] = new PyInt(0);
                    basicPyObjects[typeof(PyList)] = new PyList();
                    basicPyObjects[typeof(PyDict)] = new PyDict();
                }
            }
            catch { }
        }
        #endregion

        #region ILogSource Implementation
        /// <summary>
        /// Raise this event to request loggers log this message.
        /// </summary>
        public event Action<ILogMessage> MessageLogged;

        internal void Log(string message)
        {
            MessageLogged?.Invoke(LogMessage.Info(message));
        }

        public void Dispose()
        {
            if (Scope != null)
            {
                if (!PythonEngine.IsInitialized)
                {
                    PythonEngine.Initialize();
                    PythonEngine.BeginAllowThreads();
                }
                try
                {
                    using (Py.GIL())
                    {
                        Scope.Dispose();
                        Scope = null;
                    }
                }
                catch { }
            }
        }
        #endregion
    }
}
