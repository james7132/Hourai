using System;

namespace DrumBot {
    public enum LogLevel {
        Debug = 0,
        Info = 1,
        Warning = 2,
        Error = 3
    }

    public static class Log {

#if DEBUG
        public static LogLevel Level = LogLevel.Debug;
#else
        public static LogLevel Level = LogLevel.Info;
#endif

        public static void Info(object value) {
            LogValue(LogLevel.Info, value);
        }

        public static void Debug(object value) {
            LogValue(LogLevel.Debug, value);
        }

        public static void Warning(object value) {
            LogValue(LogLevel.Warning, value);
        }

        public static void Error(object value) {
            LogValue(LogLevel.Error, value);
        }

        static void LogValue(LogLevel level, object value) {
            if (level < Level)
                return;
            Console.WriteLine(
                $"[{level}] {DrumDate.Now}: {value}");
        }
    }
}
