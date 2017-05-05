using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Hourai {

  [AttributeUsage(AttributeTargets.Class)]
  public class ServiceAttribute : Attribute {
  }

  public static class ServiceDiscovery {

    public static IEnumerable<Type> FindServices(Assembly assembly) {
      return from type in assembly.GetTypes()
             where type.GetTypeInfo().GetCustomAttribute<ServiceAttribute>() != null
             select type;
                     //where serviceType.IsAssignableFrom(type) && !type.GetTypeInfo().IsAbstract
                     //select type;
      //foreach(var service in servicesSubtypes) {
        //AddService(service, services);
      //}
    }

    //static object AddService(Type type, IServiceCollection services) {
      //object obj;
      //if (services.TryGet(type, out obj))
        //return obj;

      //var typeInfo = type.GetTypeInfo();
      //Log.Info($"Loading Service {type.Name}...");
      //var constructor = typeInfo.DeclaredConstructors.Where(x => !x.IsStatic).First();
      //var parameters = constructor.GetParameters();
      //var properties = typeInfo.DeclaredProperties.Where(p => p.CanWrite);

      //object[] args = new object[parameters.Length];

      //for (int i = 0; i < parameters.Length; i++) {
        //var paramType = parameters[i].ParameterType;
        //Log.Info($"Found {type.Name} dependency => {paramType.Name}...");
        //object arg = null;
        //if (services == null || !services.TryGet(paramType, out arg)) {
          //if (paramType == typeof(IServiceProvider))
            //arg = services;
          //else
            //arg = AddService(paramType, services);
        //}
        //args[i] = arg;
      //}

      //try {
        //obj = constructor.Invoke(args);
        //services.AddSingleton(obj);
      //} catch (Exception ex) {
        //throw new Exception($"Failed to create \"{type.FullName}\"", ex);
      //}

      //foreach(var property in properties) {
        //var propType = property.PropertyType;
        //Log.Info($"Found {type.Name} dependency => {propType.Name}...");
        //object arg = null;
        //if (services == null || !services.TryGet(propType, out arg)) {
          //if (propType  == typeof(IServiceProvider))
            //arg = services;
          //else if (!property.IsDefined(typeof(NotServiceAttribute)))
            //arg = AddService(propType, services);
        //}
        //if (arg != null)
          //property.SetValue(obj, arg, null);
      //}
      //return obj;
    //}

  }

}
