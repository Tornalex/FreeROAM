using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.ExceptionServices;
using NUnit.Framework;

namespace SpearWander.PlayModeTests
{
    /// <summary>
    /// Explicit reflection access to the predefined Assembly-CSharp.
    /// Runtime scripts cannot be referenced directly from this test assembly.
    /// </summary>
    internal static class ReflectionTestUtilities
    {
        private const string RuntimeAssemblyName = "Assembly-CSharp";

        private const BindingFlags StaticMembers =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        private const BindingFlags InstanceMembers =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        public static Type ResolveRuntimeType(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
                throw new AssertionException("Runtime type name must not be null or empty.");

            Assembly assembly = FindRuntimeAssembly();
            Type type = assembly.GetType(fullName, throwOnError: false, ignoreCase: false);
            if (type == null)
            {
                throw new AssertionException(
                    $"Type '{fullName}' was not found in loaded assembly '{RuntimeAssemblyName}'.");
            }

            return type;
        }

        public static Assembly FindRuntimeAssembly()
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                if (string.Equals(
                        assemblies[i].GetName().Name,
                        RuntimeAssemblyName,
                        StringComparison.Ordinal))
                {
                    return assemblies[i];
                }
            }

            throw new AssertionException(
                $"Loaded assembly '{RuntimeAssemblyName}' could not be found.");
        }

        public static object GetStaticField(Type type, string name)
        {
            FieldInfo field = FindField(type, name, isStatic: true);
            return field.GetValue(null);
        }

        public static T GetStaticField<T>(Type type, string name)
        {
            return Cast<T>(GetStaticField(type, name), type, name);
        }

        public static void SetStaticField(Type type, string name, object value)
        {
            FieldInfo field = FindField(type, name, isStatic: true);
            field.SetValue(null, value);
        }

        public static object GetInstanceField(object target, string name)
        {
            if (target == null)
                throw new AssertionException($"Cannot read instance field '{name}' from a null target.");

            FieldInfo field = FindField(target.GetType(), name, isStatic: false);
            return field.GetValue(target);
        }

        public static T GetInstanceField<T>(object target, string name)
        {
            return Cast<T>(GetInstanceField(target, name), target.GetType(), name);
        }

        public static void SetInstanceField(object target, string name, object value)
        {
            if (target == null)
                throw new AssertionException($"Cannot write instance field '{name}' on a null target.");

            FieldInfo field = FindField(target.GetType(), name, isStatic: false);
            field.SetValue(target, value);
        }

        public static object GetProperty(object target, string name)
        {
            if (target == null)
                throw new AssertionException($"Cannot read property '{name}' from a null target.");

            PropertyInfo property = FindProperty(target.GetType(), name, isStatic: false);
            try
            {
                return property.GetValue(target);
            }
            catch (TargetInvocationException exception)
            {
                RethrowInner(exception);
                return null;
            }
        }

        public static T GetProperty<T>(object target, string name)
        {
            return Cast<T>(GetProperty(target, name), target.GetType(), name);
        }

        public static object GetStaticProperty(Type type, string name)
        {
            PropertyInfo property = FindProperty(type, name, isStatic: true);
            try
            {
                return property.GetValue(null);
            }
            catch (TargetInvocationException exception)
            {
                RethrowInner(exception);
                return null;
            }
        }

        public static T GetStaticProperty<T>(Type type, string name)
        {
            return Cast<T>(GetStaticProperty(type, name), type, name);
        }

        public static object InvokeStatic(
            Type type,
            string name,
            params object[] arguments)
        {
            MethodInfo method = FindMethod(type, name, StaticMembers, arguments);
            return Invoke(method, null, arguments);
        }

        public static T InvokeStatic<T>(
            Type type,
            string name,
            params object[] arguments)
        {
            object result = InvokeStatic(type, name, arguments);
            return CastResult<T>(result, type, name);
        }

        public static object InvokeInstance(
            object target,
            string name,
            params object[] arguments)
        {
            if (target == null)
                throw new AssertionException($"Cannot invoke instance method '{name}' on a null target.");

            MethodInfo method = FindMethod(target.GetType(), name, InstanceMembers, arguments);
            return Invoke(method, target, arguments);
        }

        public static T InvokeInstance<T>(
            object target,
            string name,
            params object[] arguments)
        {
            if (target == null)
                throw new AssertionException($"Cannot invoke instance method '{name}' on a null target.");

            object result = InvokeInstance(target, name, arguments);
            return CastResult<T>(result, target.GetType(), name);
        }

        private static object Invoke(
            MethodInfo method,
            object target,
            object[] arguments)
        {
            try
            {
                return method.Invoke(target, arguments);
            }
            catch (TargetInvocationException exception)
            {
                RethrowInner(exception);
                return null;
            }
        }

        private static FieldInfo FindField(Type type, string name, bool isStatic)
        {
            BindingFlags flags = isStatic ? StaticMembers : InstanceMembers;
            Type current = type;

            while (current != null)
            {
                FieldInfo field = current.GetField(name, flags);
                if (field != null)
                    return field;

                if (isStatic)
                    break;

                current = current.BaseType;
            }

            throw new AssertionException(
                $"Field '{type.FullName}.{name}' was not found " +
                $"({(isStatic ? "static" : "instance")}).");
        }

        private static PropertyInfo FindProperty(Type type, string name, bool isStatic)
        {
            BindingFlags flags = isStatic ? StaticMembers : InstanceMembers;
            Type current = type;

            while (current != null)
            {
                PropertyInfo property = current.GetProperty(name, flags);
                if (property != null)
                    return property;

                if (isStatic)
                    break;

                current = current.BaseType;
            }

            throw new AssertionException(
                $"Property '{type.FullName}.{name}' was not found " +
                $"({(isStatic ? "static" : "instance")}).");
        }

        private static MethodInfo FindMethod(
            Type type,
            string name,
            BindingFlags flags,
            object[] arguments)
        {
            arguments = arguments ?? Array.Empty<object>();
            MethodInfo[] methods = type.GetMethods(flags);
            List<MethodInfo> matches = new List<MethodInfo>();

            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (!string.Equals(method.Name, name, StringComparison.Ordinal))
                    continue;

                if (ParametersMatch(method, arguments))
                    matches.Add(method);
            }

            if (matches.Count == 1)
                return matches[0];

            string argumentDescription = DescribeArguments(arguments);
            if (matches.Count == 0)
            {
                throw new AssertionException(
                    $"Method '{type.FullName}.{name}({argumentDescription})' was not found.");
            }

            List<string> signatures = new List<string>();
            for (int i = 0; i < matches.Count; i++)
                signatures.Add(DescribeMethod(matches[i]));

            throw new AssertionException(
                $"Method '{type.FullName}.{name}({argumentDescription})' is ambiguous: " +
                string.Join(" | ", signatures));
        }

        private static bool ParametersMatch(MethodInfo method, object[] arguments)
        {
            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length != arguments.Length)
                return false;

            for (int i = 0; i < parameters.Length; i++)
            {
                Type parameterType = parameters[i].ParameterType;
                object argument = arguments[i];

                if (argument == null)
                {
                    if (parameterType.IsValueType &&
                        Nullable.GetUnderlyingType(parameterType) == null)
                    {
                        return false;
                    }

                    continue;
                }

                if (!parameterType.IsInstanceOfType(argument))
                    return false;
            }

            return true;
        }

        private static string DescribeArguments(object[] arguments)
        {
            if (arguments.Length == 0)
                return string.Empty;

            string[] names = new string[arguments.Length];
            for (int i = 0; i < arguments.Length; i++)
                names[i] = arguments[i] == null ? "null" : arguments[i].GetType().Name;

            return string.Join(", ", names);
        }

        private static string DescribeMethod(MethodInfo method)
        {
            ParameterInfo[] parameters = method.GetParameters();
            string[] names = new string[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
                names[i] = parameters[i].ParameterType.Name + " " + parameters[i].Name;

            return method.Name + "(" + string.Join(", ", names) + ")";
        }

        private static T Cast<T>(object value, Type ownerType, string memberName)
        {
            if (value == null)
            {
                if (default(T) == null)
                    return default;

                throw new AssertionException(
                    $"Member '{ownerType.FullName}.{memberName}' returned null, " +
                    $"but '{typeof(T).FullName}' was expected.");
            }

            if (value is T typedValue)
                return typedValue;

            throw new AssertionException(
                $"Member '{ownerType.FullName}.{memberName}' returned " +
                $"'{value.GetType().FullName}', but '{typeof(T).FullName}' was expected.");
        }

        private static T CastResult<T>(object value, Type ownerType, string memberName)
        {
            if (typeof(T) == typeof(object))
                return (T)value;

            return Cast<T>(value, ownerType, memberName);
        }

        private static void RethrowInner(TargetInvocationException exception)
        {
            if (exception.InnerException != null)
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();

            throw exception;
        }
    }
}
