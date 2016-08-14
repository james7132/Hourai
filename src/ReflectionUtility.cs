using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DrumBot {
    public static class ReflectionUtility {

        public static IEnumerable<Type> AllTypes= from assembly in AppDomain.CurrentDomain.GetAssemblies()
                                                  from type in assembly.GetTypes()
                                                  select type;

        public static IEnumerable<Type> ConcreteClasses = from type in AllTypes
                                                          where !type.IsAbstract && type.IsClass
                                                          select type;

        public static IEnumerable<Type> InheritsFrom<T>(this IEnumerable<Type> types) {
            var baseType = typeof(T);
            return from type in types 
                   where baseType.IsAssignableFrom(type)
                   select type;
        }

        public static IEnumerable<KeyValuePair<Type, T>> WithAttribute<T>(this IEnumerable<Type> types, bool inherit = false) where T : Attribute {
            var attributeType = typeof(T);
            return from type in types
                   where type.IsDefined(attributeType, inherit)
                   select new KeyValuePair<Type, T>(type, type.GetCustomAttributes().OfType<T>().FirstOrDefault());
        }

        public static IEnumerable<Type> WithParameterlessConstructor(
            this IEnumerable<Type> types) {
            return from type in types
                   where type.GetConstructor(Type.EmptyTypes) != null
                   select type;
        }
    }
}
