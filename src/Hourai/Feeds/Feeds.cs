using System;
using System.Linq;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System.Threading.Tasks;
using Hourai.Preconditions;

namespace Hourai.Feeds {

[Log]
[ChannelRateLimit(1, 1)]
[RequireContext(ContextType.Guild)]
[RequirePermission(GuildPermission.ManageGuild, Require.User | Require.BotOwnerOverride)]
public partial class Feeds : HouraiModule {
}

}
