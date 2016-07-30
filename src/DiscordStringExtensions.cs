using Discord;

namespace DrumBot {
    public static class DiscordStringExtensions {

        public static string ToIDString(this Channel channel) {
            return $"#{channel.Name} ({channel.Id})";
        }

        public static string ToIDString(this Server server) {
            return $"\"{server.Name}\"({server.Id})";
        }

        public static string ToIDString(this Profile user) {
            return $"{user.Name} ({user.Id})";
        }

        public static string ToIDString(this User user) {
            return $"{user.Name} ({user.Id})";
        }

    }
}
