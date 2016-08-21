using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Commands.Permissions.Visibility;
using Discord.Modules;

namespace DrumBot {
    public class ChannelModule : IModule {

        const string Permission = "Manage Channels";

        public void Install(ModuleManager manager) {
            manager.CreateCommands("channel", cbg => {
                cbg.Category("Admin")
                    .PublicOnly()
                    .AddCheck(Check.ManageChannels());
                CreateCommand(cbg, "create", "Creates a public channel with a specified name.")
                    .Parameter("Name")
                    .Do(CreateChannel);
                CreateCommand(cbg, "delete", "Deletes all mentioned channels")
                    .Parameter("Channel(s)", ParameterType.Multiple)
                    .Do(DeleteChannel);
            });
        }

        async Task DeleteChannel(CommandEventArgs e) {
            await Command.ForEvery(e, e.Message.MentionedChannels, Command.Action(e.Channel, async c => await c.Delete()));
        }

        async Task CreateChannel(CommandEventArgs e) {
            var channel = await e.Server.CreateChannel(e.GetArg("Name"), ChannelType.Text);
            await e.Respond($"{Config.SuccessResponse}: {channel.Mention} created.");
        }

        CommandBuilder CreateCommand(CommandGroupBuilder builder,
                                      string name,
                                      string description) {
            return builder.CreateCommand(name)
                          .Description(description + Utility.Requires(Permission));
        }
    }
}
