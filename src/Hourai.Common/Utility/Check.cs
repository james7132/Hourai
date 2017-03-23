using Discord;
using System;

namespace Hourai {

  public static class Check {
    public static T NotNull<T>(T obj) {
      if (obj == null)
        throw new ArgumentNullException();
      return obj;
    }

    public static ITextChannel InGuild(IMessage message) {
      if(!(message.Channel is ITextChannel))
        throw new Exception("Command must be executed in a public channel");
      return (ITextChannel) message.Channel;
    }
  }

  public static class QCheck {

    public static ITextChannel InGuild(IMessage message) {
      return message.Channel as ITextChannel;
    }

  }
}
