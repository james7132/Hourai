using Discord;
using Discord.Commands;

namespace Hourai.Preconditions {

  public abstract class DocumentedPreconditionAttribute : PreconditionAttribute {

    public abstract string GetDocumentation();

  }

}
