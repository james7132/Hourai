using Discord;

namespace DrumBot {
    public class SubchannelService : IService {

        public Config Config { get; }

        public SubchannelService(Config config) {
            Config = Check.NotNull(config);
        }

        public void Install(DiscordClient client) {
            client.ChannelDestroyed +=
                async delegate(object sender, ChannelEventArgs args) {
                    var channel = args.Channel;
                    var serverConfig = Config.GetServerConfig(args.Server);
                    if(serverConfig.IsMainChannel(channel) && 
                       args.Server.CurrentUser.ServerPermissions.ManageChannels) {
                        foreach (var groupChannel in serverConfig.GetChannelGroup(channel)) {
                            if (groupChannel == null)
                                continue;
                            await groupChannel.Delete();
                        }
                    } 
                    await serverConfig.Cleanup(client);
                };
        }
    }
}
