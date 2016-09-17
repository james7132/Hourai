using System;
using System.Collections;
using System.Collections.Generic;

namespace Hourai {

public interface ICounter {
  void Increment();
  void IncrementBy(ulong value);
}

public interface IReadableCounter {
  ulong? Value { get; }
}

public interface IMaxValueTracker {
  bool Test(ulong value);
}

public class SimpleCounter : ICounter, IReadableCounter {
  ulong _count;

  public virtual void Increment() { _count++; }

  public virtual void IncrementBy(ulong value) { _count += value; }
  public ulong? Value => _count;
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

public class CounterSet : CounterSet<ICounter> {

  public CounterSet(IFactroy<ICounter> factory) : base(factory) {}

  public CounterSet(Func<ICounter> factoryFunc) : base(factoryFunc) {}

}

public class CounterSet<T> : CounterSet<string, T> where T : ICounter {

  public CounterSet(IFactroy<T> factory) : base(factory) {}

  public CounterSet(Func<T> factoryFunc) : base(factoryFunc) {}

}

public class CounterSet<TKey, T> : IEnumerable<KeyValuePair<TKey, T>> where T : ICounter {

  readonly Dictionary<TKey, T> _counters;
  readonly IFactroy<T> _counterFactory;

  public CounterSet(IFactroy<T> factory) {
    _counters = new Dictionary<TKey, T>();
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
