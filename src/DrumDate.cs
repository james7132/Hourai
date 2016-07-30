using System;

namespace DrumBot {
    public static class DrumDate {
        public static string Now => DateTime.Now.DrumDateString();

        public static string DrumDateString(this DateTime date) {
            return date.ToString("yyyy-MM-dd hh:mm:ss");
        }
    }
}
