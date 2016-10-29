using System;
using System.Linq;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System.Threading.Tasks;

namespace Hourai {

[RequireContext(ContextType.Guild)]
[ModuleCheck(ModuleType.Feeds)]
public partial class Feeds : HouraiModule {
}

}
