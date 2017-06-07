using System;
using System.Collections.Concurrent;

namespace Hourai {

  public static class FuncExtensions {

    public static Func<T> Memoize<T>(this Func<T> func) {
      bool computed = false;
      T cachedValue = default(T);
      return () => {
        if (!computed) {
          cachedValue = func();
          computed = true;
        }
        return cachedValue;
      };
    }

    public static Func<T, TResult> Memoize<T, TResult>(this Func<T, TResult> func) {
      var  valueMap = new ConcurrentDictionary<T, TResult>();
      return param => {
        TResult value;
        if (!valueMap.TryGetValue(param, out value)) {
          value = func(param);
          valueMap[param] = value;
        }
        return value;
      };
    }


  }

}
