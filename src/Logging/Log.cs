using System;
using System.Diagnostics;

namespace DrumBot {

public static class Log {

  public enum Level {
    Debug = 0,
    Info = 1,
    Warning = 2,
    Error = 3
  }

#if DEBUG
  public static Level LogLevel = Level.Debug;
#else
  public static Level LogLevel = Level.Info;
#endif

  public static void Info(object value) {
      LogValue(Level.Info, value);
  }

  public static void Debug(object value) {
      LogValue(Level.Debug, value);
  }

  public static void Warning(object value) {
      LogValue(Level.Warning, value);
  }

  public static void Error(object value) {
      LogValue(Level.Error, value);
  }

  static void LogValue(Level level, object value) {
      if (level < LogLevel)
          return;
      Trace.WriteLine($"[{level}] {Utility.DateString(DateTime.Now)}: {value}");
  }

}

}
