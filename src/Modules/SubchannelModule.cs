using Discord.Modules;

namespace DrumBot {
    public class SubchannelModule : IModule {

        public void Install(ModuleManager manager) {
            //manager.ChannelDestroyed +=
            //    async delegate(object sender, ChannelEventArgs args) {
            //        var channel = args.Channel;
            //        var serverConfig = Config.GetServerConfig(args.Server);
            //        if(serverConfig.IsMainChannel(channel) && 
            //           args.Server.CurrentUser.ServerPermissions.ManageChannels) {
            //            foreach (var groupChannel in serverConfig.GetChannelGroup(channel)) {
            //                if (groupChannel == null)
            //                    continue;
            //                await groupChannel.Delete();
            //            }
            //        } 
            //        serverConfig.Cleanup(manager.Client);
            //    };
            manager.CreateCommands("soon",
                cbg => {
                    cbg.CreateCommand()
                        .Do(async e => await e.Respond("Subchannel emulation: Coming soon™"));
                });
        }
    }
}
