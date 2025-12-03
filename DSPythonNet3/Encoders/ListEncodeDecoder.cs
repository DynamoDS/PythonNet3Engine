using System;
using System.Collections;
using System.Collections.Generic;
using Dynamo.Utilities;
using Python.Runtime;

namespace DSPythonNet3.Encoders
{
    internal class ListEncoderDecoder : IPyObjectEncoder, IPyObjectDecoder
    {
        private static readonly Type[] decodableTypes = new Type[]
        {
            typeof(IList), typeof(ArrayList),
            typeof(IList<>), typeof(List<>),
            typeof(IEnumerable), typeof(IEnumerable<>)
        };

        public bool CanEncode(Type type)
        {
            return typeof(IList).IsAssignableFrom(type);
        }

        public bool TryDecode<T>(PyObject pyObj, out T value)
        {
            if (!pyObj.IsIterable())
            {
                value = default;
                return false;
            }

            if (typeof(T).IsGenericType)
            {
                using (var pyList = PyList.AsList(pyObj))
                {
                    value = pyList.ToList<T>();
                }
                return true;
            }

            var converted = ConvertToArrayList(pyObj);
            value = (T)converted;
            return true;
        }

        public PyObject TryEncode(object value)
        {
            // This is a no-op to prevent Python.NET from encoding generic lists
            // https://github.com/pythonnet/pythonnet/pull/963#issuecomment-642938541
            return PyObject.FromManagedObject(value);
        }

        bool IPyObjectDecoder.CanDecode(PyType objectType, Type targetType)
        {
            if (targetType.IsGenericType)
            {
                targetType = targetType.GetGenericTypeDefinition();
            }
            return decodableTypes.IndexOf(targetType) >= 0;
        }

        private static IList ConvertToArrayList(PyObject pyObj)
        {
            using var pyList = PyList.AsList(pyObj);
            var result = new ArrayList();
            foreach (PyObject item in pyList)
            {
                using (item)
                {
                    result.Add(ConvertItem(item));
                }
            }
            return result;
        }

        private static object ConvertItem(PyObject item)
        {
            if (TryGetClrObject(item, out var clrObject))
            {
                return clrObject;
            }

            if (PyString.IsStringType(item))
            {
                return item.AsManagedObject(typeof(string));
            }

            if (PyList.IsListType(item) || PyTuple.IsTupleType(item))
            {
                return ConvertToArrayList(item);
            }

            return item.AsManagedObject(typeof(object));
        }

        private static bool TryGetClrObject(PyObject pyObj, out object clrObject)
        {
            try
            {
                clrObject = pyObj.GetManagedObject();
                return clrObject != null;
            }
            catch
            {
                clrObject = null;
                return false;
            }
        }
    }
}
