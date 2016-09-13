using System;

namespace DrumBot {

public interface IFactroy<out T> {

  T Create();

}

public class ActivatorFactory<T> : IFactroy<T> {

  public T Create() { return Activator.CreateInstance<T>(); }

}

public class GenericFactory<T> : IFactroy<T> {

  Func<T> Func { get; }
  public GenericFactory(Func<T> func) { Func = Check.NotNull(func); }
  public T Create() { return Func(); }

}

}
