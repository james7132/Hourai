using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Hourai.Extensions {

public static class AsyncEnumerable {

  public static Task ForEachAwait<T>(
      this IAsyncEnumerable<T> enumerable,
      Func<T, Task> action) {
    return ForEachAwait(enumerable, action, CancellationToken.None);
  }

  public static async Task ForEachAwait<T>(
      this IAsyncEnumerable<T> enumerable,
      Func<T, Task> action,
      CancellationToken cancellationToken) {
    using (var enumerator = enumerable.GetEnumerator()) {
      if (await enumerator.MoveNext(cancellationToken).ConfigureAwait(continueOnCapturedContext: false)) {
        Task<bool> moveNextTask;
        do {
          var current = enumerator.Current;
          moveNextTask = enumerator.MoveNext(cancellationToken);
          await action(current); //now with await
        } while (await moveNextTask.ConfigureAwait(continueOnCapturedContext: false));
      }
    }
  }

}

}

