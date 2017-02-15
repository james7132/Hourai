using System;
using System.Linq;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System.Threading.Tasks;
using Hourai.Preconditions;

namespace Hourai.Feeds {

[RequireContext(ContextType.Guild)]
[RequireModule(ModuleType.Feeds)]
public partial class Feeds : HouraiModule {
}

}
