using Discord;
using Discord.Commands;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Hourai.Preconditions {

  internal class RateLimitCounter {

    public int Count { get; private set; }
    public DateTimeOffset Time { get; private set; }

    object _lock = new object();

    public  RateLimitCounter() {
      Time = DateTimeOffset.UtcNow;
    }

    public bool Check(int limit, double time) {
      lock(_lock) {
        var now = DateTimeOffset.UtcNow;
        if(now > Time + TimeSpan.FromSeconds(time)) {
          Count = 0;
          Time = now;
        }
        Count++;
        return Count > limit;
      }
    }

  }

  public abstract class RateLimitAttribute : DocumentedPreconditionAttribute {

    public int Limit { get; }
    public double Time { get; }

    readonly ConcurrentDictionary<ulong, RateLimitCounter> _counters;
    Func<ulong, RateLimitCounter> counterFactory = id => new RateLimitCounter();

    public RateLimitAttribute(int limit, double time) {
      Limit = limit;
      Time = time;
      _counters = new ConcurrentDictionary<ulong, RateLimitCounter>();
    }

    protected abstract IEntity<ulong> GetEntity(ICommandContext context);
    protected abstract string LimitType { get; }

    public override Task<PreconditionResult> CheckPermissions(
        ICommandContext context,
        CommandInfo commandInfo,
        IDependencyMap dependencies) {
      var id = GetEntity(context)?.Id;
      var result = PreconditionResult.FromSuccess();
      if(id != null) {
        var counter = _counters.GetOrAdd(id.Value, counterFactory);
        if(counter.Check(Limit, Time)) {
          result = PreconditionResult.FromError(
              $"{LimitType} rate limit exceeded: {counter.Count}/{Time}");
        }
      }
      return Task.FromResult(result);
    }

    public override string GetDocumentation() =>
      $"{LimitType} rate limit: {Limit} per {Time} seconds.";

  }

  public class UserRateLimitAttribute : RateLimitAttribute {

    public UserRateLimitAttribute(int limit, double time) : base(limit, time) {
    }

    protected override string LimitType => "User";
    protected override IEntity<ulong> GetEntity(ICommandContext context) => context.User;

  }

  public class ChannelRateLimitAttribute : RateLimitAttribute {

    public ChannelRateLimitAttribute(int limit, double time) : base(limit, time) {
    }

    protected override string LimitType => "Channel";
    protected override IEntity<ulong> GetEntity(ICommandContext context) => context.Channel;

  }

  public class GuildRateLimitAttribute : RateLimitAttribute {

    public GuildRateLimitAttribute(int limit, double time) : base(limit, time) {
    }

    protected override string LimitType => "Server";
    protected override IEntity<ulong> GetEntity(ICommandContext context) => context.Guild;

  }

}
