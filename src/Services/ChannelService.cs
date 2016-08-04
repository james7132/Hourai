using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace DrumBot {
    public class ChannelService : IService {

        const string Requires = "Requires ``Manage Channels`` permission for both bot and user.";

        public void Install(DiscordClient client) {
            var commandService = client.GetService<CommandService>();

            commandService.CreateGroup("channel",
                cbg => {

                    CreateCommand(cbg, "create", "Creates a channel with a specified name.", false)
                        .Parameter("Name")
                        .Do(CreateChannel);

                    CreateCommand(cbg, "delete", "Deletes all mentioned channels")
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
                                      string description,
                                      bool multiple = true) {
            var command = builder.CreateCommand(name)
                                 .Description(description + Requires)
                                 .AddCheck(new ProdChecker())
                                 .AddCheck(Check.ManageChannels());
            if (multiple)
                command = command.Parameter("Channels", ParameterType.Multiple);
            return command;
        }
    }
}
