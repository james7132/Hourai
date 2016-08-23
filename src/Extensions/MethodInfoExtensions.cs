using System;
using System.Linq;
using System.Reflection;

namespace DrumBot {
    public static class MethodInfoExtensions {
        public static bool IsMethodCompatibleWithDelegate<T>(this MethodInfo method) where T : class {
            Type delegateType = typeof(T);
            MethodInfo delegateSignature = delegateType.GetMethod("Invoke");

            bool parametersEqual = delegateSignature
                .GetParameters()
                .Select(x => x.ParameterType)
                .SequenceEqual(method.GetParameters()
                    .Select(x => x.ParameterType));

            return delegateSignature.ReturnType == method.ReturnType &&
                   parametersEqual;
        }

        public static Delegate ToDelegate<T>(this MethodInfo method) where T : class {
            Check.NotNull(method);
            if(method.IsMethodCompatibleWithDelegate<T>())
                return Delegate.CreateDelegate(typeof(T), method);
            return null;
        }
    }
}
