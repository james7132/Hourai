using Discord;

namespace DrumBot {
    public static class ServerExtensions {

        /// <summary>
        /// Shortcut for getting the Server config given a Server reference.
        /// </summary>
        public static ServerConfig GetConfig(this Server server) => Bot.Config.GetServerConfig(server);
    }
}
