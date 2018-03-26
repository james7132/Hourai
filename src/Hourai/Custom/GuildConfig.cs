using Discord;
using Discord.WebSocket;
using Hourai.Extensions;
using Hourai.Model;
using Hourai.Custom.Converters;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Hourai.Custom {

public abstract class DiscordContextConfig {
  [YamlMember(Alias="on_message")]
  public CustomEvent OnMessage { get; set; }
  [YamlMember(Alias="on_edit")]
  public CustomEvent OnEdit { get; set; }
  [YamlMember(Alias="on_join")]
  public CustomEvent OnJoin { get; set; }
  [YamlMember(Alias="on_leave")]
  public CustomEvent OnLeave { get; set; }
  [YamlMember(Alias="on_ban")]
  public CustomEvent OnBan { get; set; }
}

public class ChannelConfig : DiscordContextConfig {
}

public class GuildConfig : DiscordContextConfig {

  static readonly Serializer Serializer;
  static readonly Deserializer Deserializer;

  static GuildConfig() {
    Serializer = new SerializerBuilder()
      .WithTypeConverter(new ExecuteCommandActionConverter())
      .Build();
    Deserializer = new DeserializerBuilder()
      .WithTypeConverter(new ExecuteCommandActionConverter())
      .Build();
  }

  [YamlMember(Alias="aliases")]
  public Dictionary<string, string> Aliases { get; set; }
  [YamlMember(Alias="channels")]
  public Dictionary<string, ChannelConfig> Channels { get; set; }

  public static GuildConfig FromString(string config) =>
    Deserializer.Deserialize<GuildConfig>(config);

  public override string ToString() => Serializer.Serialize(this);

}

public abstract class ProcessableEvent {

  public static readonly Dictionary<Type, PropertyInfo[]> _properties;

  static ProcessableEvent() {
    _properties = new Dictionary<Type, PropertyInfo[]>();
  }

  protected ILogger Logger;

  async Task PropogateEvent(HouraiContext context, ILogger logger) {
    var eventType = typeof(ProcessableEvent);
    PropertyInfo[] properties;
    if (!_properties.TryGetValue(GetType(), out properties)) {
      properties = (from property in GetType().GetTypeInfo().GetProperties()
                   where eventType.IsAssignableFrom(property.PropertyType)
                   select property).ToArray();
      _properties[GetType()] = properties;
    }
    var events = properties.Select(p => p.GetValue(this)).OfType<ProcessableEvent>();
    logger.LogInformation(events.Count().ToString());
    foreach (var evt in events) {
      logger.LogInformation(evt.GetType().ToString());
    }
    await Task.WhenAll(events.Select(e => e.ProcessEvent(context, logger)));
  }

  public virtual Task Process(HouraiContext context) => Task.CompletedTask;

  public virtual Task<bool> IsValid(HouraiContext context) => (true).ToTask();

  public async Task ProcessEvent(HouraiContext context, ILogger logger) {
    Logger = logger;
    try {
      if (!(await IsValid(context))) return;
      await Process(context);
      await PropogateEvent(context, logger);
    } catch (Exception e) {
      logger.LogError(0, e, "Error in running custom event.");
    }
  }
}

public abstract class ActionableEvent : ProcessableEvent {

  [YamlMember(Alias="execute")]
  public ExecuteCommandAction ExecuteAction { get; set; }

}

public class CustomEvent : ActionableEvent {

  [YamlMember(Alias="content")]
  public MessageContentFilter ContentFilter { get; set; }

  [YamlMember(Alias="username")]
  public UsernameFilter UsernameFilter { get; set; }

}

public class MessageContentFilter : ActionableEvent {

  [YamlMember(Alias="match")]
  public string Match { get; set; }

  public override Task<bool> IsValid(HouraiContext context) =>
    Regex.IsMatch(context.Content, Match).ToTask();

}

public class UsernameFilter : ActionableEvent {

  [YamlMember(Alias="match")]
  public string Match { get; set; }

  public override Task<bool> IsValid(HouraiContext context) {
    return context.Users.Any(u =>
           Regex.IsMatch(u.Username, Match)).ToTask();
  }

}

public class ExecuteCommandAction : ProcessableEvent {

  [YamlMember(Alias="commands")]
  public List<string> Commands { get; set; }

  public override async Task Process(HouraiContext context) {
    foreach (var command in Commands) {
      var commandContext = new HouraiContext(
          context.Client,
          context.Process(command),
          (SocketUser) context.Guild?.CurrentUser ??
            (SocketUser) context.Client.CurrentUser,
          context.Channel,
          context.Db);
      await context.Commands.ExecuteCommand(commandContext);
    }
  }

}

}
