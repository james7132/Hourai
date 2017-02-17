using Discord;
using Discord.Commands;
using Hourai.Model;
using System.Threading.Tasks;

namespace Hourai.Preconditions {

  public enum ModuleType : long {
    Standard = 1 << 0,
    Admin = 1 << 1,
    Feeds = 1 << 2
  }

}
