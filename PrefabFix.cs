using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityObject = UnityEngine.Object;

// The following is adapted from Valheim lib

// MIT License
//
// Copyright (c) 2021 ValheimLib
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

namespace BetterContinents
{
    public static class ReflectionHelper
    {
        public const BindingFlags AllBindingFlags = (BindingFlags) (-1);

        public static bool IsSameOrSubclass(this Type type, Type @base)
        {
            return type.IsSubclassOf(@base)
                   || type == @base;
        }

        // https://stackoverflow.com/a/21995826
        public static Type GetEnumeratedType(this Type type) =>
            type?.GetElementType() ?? 
            (typeof(IEnumerable).IsAssignableFrom(type) ? type.GetGenericArguments().FirstOrDefault() : null);

        public static class Cache
        {
            private static MethodInfo _enumerableToArray;
            public static MethodInfo EnumerableToArray
            {
                get
                {
                    if (_enumerableToArray == null)
                    {
                        _enumerableToArray = typeof(Enumerable).GetMethod("ToArray", AllBindingFlags);
                    }

                    return _enumerableToArray;
                }
            }

            private static MethodInfo _enumerableCast;
            public static MethodInfo EnumerableCast
            {
                get
                {
                    if (_enumerableCast == null)
                    {
                        _enumerableCast = typeof(Enumerable).GetMethod("Cast", AllBindingFlags);
                    }

                    return _enumerableCast;
                }
            }
        }
	}

    /// <summary>
    /// Helper class for everything Prefab related
    /// </summary>
    public static class Prefab
    {
        /// <summary>
        /// Prefix used by the Mock System to recognize Mock gameObject that must be replaced at some point.
        /// </summary>
        public const string MockPrefix = "BCMock_";

        private static GameObject _parent;

        /// <summary>
        /// Will try to find the real vanilla prefab from the given mock
        /// </summary>
        /// <param name="unityObject"></param>
        /// <param name="mockObjectType"></param>
        /// <returns>the real prefab</returns>
        public static UnityObject GetRealPrefabFromMock(UnityObject unityObject, Type mockObjectType)
        {
            if (unityObject)
            {
                var unityObjectName = unityObject.name;
                var isMock = unityObjectName.StartsWith(MockPrefix);
                if (isMock)
                {
                    unityObjectName = unityObjectName.Substring(MockPrefix.Length);

                    // Cut off the suffix in the name to correctly query the original material
                    if (unityObject is Material)
                    {
                        const string materialInstance = " (Instance)";
                        if (unityObjectName.EndsWith(materialInstance))
                        {
                            unityObjectName = unityObjectName.Substring(0, unityObjectName.Length - materialInstance.Length);
                            Debug.LogError(unityObjectName);
                        }
                    }

                    return Cache.GetObject(mockObjectType, unityObjectName);
                }
            }

            return null;
        }

        /// <summary>
        /// Will attempt to fix every field that are mocks gameObjects / Components from the given object.
        /// </summary>
        /// <param name="objectToFix"></param>
        private static void FixReferences(this object objectToFix)
        {
            objectToFix.FixReferences(0);
        }

        // Thanks for not using the Resources folder IronGate
        // There is probably some oddities in there
        private static void FixReferences(this object objectToFix, int depth = 0)
        {
            // This is totally arbitrary.
            // I had to add a depth because of call stack exploding otherwise
            if (depth == 10)
                return;

            depth++;

            var type = objectToFix.GetType();

            const BindingFlags flags = ReflectionHelper.AllBindingFlags & ~BindingFlags.Static;

            var fields = type.GetFields(flags);
            var baseType = type.BaseType;
            while (baseType != null)
            {
                var parentFields = baseType.GetFields(flags);
                fields = fields.Union(parentFields).ToArray();
                baseType = baseType.BaseType;
            }

            foreach (var field in fields)
            {
                var fieldType = field.FieldType;

                var isUnityObject = fieldType.IsSameOrSubclass(typeof(UnityObject));
                if (isUnityObject)
                {
                    var mock = (UnityObject)field.GetValue(objectToFix);
                    var realPrefab = GetRealPrefabFromMock(mock, fieldType);
                    if (realPrefab)
                    {
                        field.SetValue(objectToFix, realPrefab);
                    }
                }
                else
                {
                    var enumeratedType = fieldType.GetEnumeratedType();
                    var isEnumerableOfUnityObjects = enumeratedType?.IsSameOrSubclass(typeof(UnityObject)) == true;
                    if (isEnumerableOfUnityObjects)
                    {
                        var currentValues = (IEnumerable<UnityObject>)field.GetValue(objectToFix);
                        if (currentValues != null)
                        {
                            var isArray = fieldType.IsArray;
                            var newI = isArray ? (IEnumerable<UnityObject>)Array.CreateInstance(enumeratedType, currentValues.Count()) : (IEnumerable<UnityObject>)Activator.CreateInstance(fieldType);
                            var list = new List<UnityObject>();
                            foreach (var unityObject in currentValues)
                            {
                                var realPrefab = GetRealPrefabFromMock(unityObject, enumeratedType);
                                if (realPrefab)
                                {
                                    list.Add(realPrefab);
                                }
                            }

                            if (list.Count > 0)
                            {
                                if (isArray)
                                {
                                    var toArray = ReflectionHelper.Cache.EnumerableToArray;
                                    var toArrayT = toArray.MakeGenericMethod(enumeratedType);

                                    // mono...
                                    var cast = ReflectionHelper.Cache.EnumerableCast;
                                    var castT = cast.MakeGenericMethod(enumeratedType);
                                    var correctTypeList = castT.Invoke(null, new object[] { list });

                                    var array = toArrayT.Invoke(null, new object[] { correctTypeList });
                                    field.SetValue(objectToFix, array);
                                }
                                else
                                {
                                    field.SetValue(objectToFix, newI.Concat(list));
                                }
                            }
                        }
                    }
                    else if (enumeratedType?.IsClass == true)
                    {
                        var currentValues = (IEnumerable<object>)field.GetValue(objectToFix);
                        if (currentValues != null)
                        {
                            foreach (var value in currentValues)
                            {
                                value.FixReferences(depth);
                            }
                        }
                    }
                    else if (fieldType.IsClass)
                    {
                        field.GetValue(objectToFix)?.FixReferences(depth);
                    }
                }
            }

            var properties = type.GetProperties(flags).ToList();
            baseType = type.BaseType;
            if (baseType != null)
            {
                var parentProperties = baseType.GetProperties(flags).ToList();
                foreach (var a in parentProperties)
                    properties.Add(a);
            }
            foreach (var property in properties)
            {
                var propertyType = property.PropertyType;

                var isUnityObject = propertyType.IsSameOrSubclass(typeof(UnityObject));
                if (isUnityObject)
                {
                    var mock = (UnityObject)property.GetValue(objectToFix, null);
                    var realPrefab = GetRealPrefabFromMock(mock, propertyType);
                    if (realPrefab)
                    {
                        property.SetValue(objectToFix, realPrefab, null);
                    }
                }
                else
                {
                    var enumeratedType = propertyType.GetEnumeratedType();
                    var isEnumerableOfUnityObjects = enumeratedType?.IsSameOrSubclass(typeof(UnityObject)) == true;
                    if (isEnumerableOfUnityObjects)
                    {
                        var currentValues = (IEnumerable<UnityObject>)property.GetValue(objectToFix, null);
                        if (currentValues != null)
                        {
                            var isArray = propertyType.IsArray;
                            var newI = isArray ? (IEnumerable<UnityObject>)Array.CreateInstance(enumeratedType, currentValues.Count()) : (IEnumerable<UnityObject>)Activator.CreateInstance(propertyType);
                            var list = new List<UnityObject>();
                            foreach (var unityObject in currentValues)
                            {
                                var realPrefab = GetRealPrefabFromMock(unityObject, enumeratedType);
                                if (realPrefab)
                                {
                                    list.Add(realPrefab);
                                }
                            }

                            if (list.Count > 0)
                            {
                                if (isArray)
                                {
                                    var toArray = ReflectionHelper.Cache.EnumerableToArray;
                                    var toArrayT = toArray.MakeGenericMethod(enumeratedType);

                                    // mono...
                                    var cast = ReflectionHelper.Cache.EnumerableCast;
                                    var castT = cast.MakeGenericMethod(enumeratedType);
                                    var correctTypeList = castT.Invoke(null, new object[] { list });

                                    var array = toArrayT.Invoke(null, new object[] { correctTypeList });
                                    property.SetValue(objectToFix, array, null);
                                }
                                else
                                {
                                    property.SetValue(objectToFix, newI.Concat(list), null);
                                }
                            }
                        }
                    }
                    else if (enumeratedType?.IsClass == true)
                    {
                        var currentValues = (IEnumerable<object>)property.GetValue(objectToFix, null);
                        foreach (var value in currentValues)
                        {
                            value.FixReferences(depth);
                        }
                    }
                    else if (propertyType.IsClass)
                    {
                        if (property.GetIndexParameters().Length == 0)
                        {
                            property.GetValue(objectToFix, null)?.FixReferences(depth);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Fix the components fields of a given gameObject
        /// </summary>
        /// <param name="gameObject"></param>
        public static void FixReferences(this GameObject gameObject, params Type[] componentTypes)
        {
            foreach (var component in componentTypes.SelectMany(ct => gameObject.GetComponentsInChildren(ct)))
            {
                component.FixReferences();
            }
        }

        /// <summary>
        /// Helper class for caching gameobjects in the current scene.
        /// </summary>
        public static class Cache
        {
            private static readonly Dictionary<Type, Dictionary<string, UnityObject>> DictionaryCache =
                new Dictionary<Type, Dictionary<string, UnityObject>>();

            private static void InitCache(Type type, Dictionary<string, UnityObject> map = null)
            {
                map ??= new Dictionary<string, UnityObject>();
                foreach (var unityObject in Resources.FindObjectsOfTypeAll(type))
                {
                    map[unityObject.name] = unityObject;
                }

                DictionaryCache[type] = map;
            }

            /// <summary>
            /// Get an instance of an UnityObject from the current scene with the given name
            /// </summary>
            /// <param name="type"></param>
            /// <param name="name"></param>
            /// <returns></returns>
            public static UnityObject GetObject(Type type, string name)
            {
                if (DictionaryCache.TryGetValue(type, out var map))
                {
                    if (map.Count == 0 || !map.Values.First())
                    {
                        InitCache(type, map);
                    }

                    if (map.TryGetValue(name, out var unityObject))
                    {
                        return unityObject;
                    }
                }
                else
                {
                    InitCache(type);
                    return GetObject(type, name);
                }

                return null;
            }
        }
    }
}