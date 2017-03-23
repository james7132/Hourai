using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace Hourai {

public interface ICounter { void IncrementBy(ulong value); }

public static class CounterExtensions {

  public static void Increment(this ICounter counter) => counter?.IncrementBy(1);

}

public interface IReadableCounter {
  ulong? Value { get; }
}

public interface IMaxValueTracker {
  bool Test(ulong value);
}

public class SimpleCounter : ICounter, IReadableCounter {
  ulong _count;

  protected ulong Count { get; set; }

  public virtual void IncrementBy(ulong value) { _count += value; }
  public ulong? Value => _count;
}

public class SaveableCounter : SimpleCounter {

  ulong _oldCount;

  public SaveableCounter(ulong value, DateTimeOffset? lastChanged = null) {
    Count = value;
    _oldCount = value;
  }

  public bool IsDirty => _oldCount != Count;
  public DateTimeOffset LastChanged  { get; private set; }

  public override void IncrementBy(ulong value) {
    base.IncrementBy(value);
    LastChanged = DateTimeOffset.UtcNow;
  }

  public void Save() => _oldCount = Count;

}

public class AggregatedCounter : ICounter, IEnumerable<ICounter> {

  readonly List<ICounter> _counters;

  public AggregatedCounter(IEnumerable<ICounter> counters) {
    _counters = new List<ICounter>(Check.NotNull(counters));
  }

  public AggregatedCounter(params ICounter[] counters) :
      this((IEnumerable<ICounter>)counters) {}

  public IEnumerator<ICounter> GetEnumerator() { return _counters.GetEnumerator(); }

  IEnumerator IEnumerable.GetEnumerator() { return ((IEnumerable) _counters).GetEnumerator(); }

  public void Increment() {
    foreach (ICounter counter in _counters)
      counter.Increment();
  }

  public void IncrementBy(ulong value) {
    foreach (ICounter counter in _counters) {
      counter.IncrementBy(value);
    }
  }
}

public class CounterSet : CounterSet<SimpleCounter> {

  public CounterSet(IFactroy<SimpleCounter> factory) : base(factory) {}

  public CounterSet(Func<SimpleCounter> factoryFunc) : base(factoryFunc) {}

}

public class CounterSet<T> : CounterSet<string, T> where T : ICounter {

  public CounterSet(IFactroy<T> factory) : base(factory) {}

  public CounterSet(Func<T> factoryFunc) : base(factoryFunc) {}

}

public class CounterSet<TKey, T> : IEnumerable<KeyValuePair<TKey, T>> where T : ICounter {

  readonly ConcurrentDictionary<TKey, T> _counters;
  readonly IFactroy<T> _counterFactory;

  public CounterSet(IFactroy<T> factory) {
    _counters = new ConcurrentDictionary<TKey, T>();
    _counterFactory = Check.NotNull(factory);
  }

  public CounterSet(Func<T> factoryFunc) : this(new GenericFactory<T>(factoryFunc)){}

  public T Get(TKey key) {
    if (!_counters.ContainsKey(key))
      _counters[key] = _counterFactory.Create();
    return _counters[key];
  }

  public IEnumerator<KeyValuePair<TKey, T>> GetEnumerator() {
    return _counters.GetEnumerator();
  }

  IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }

}
}
