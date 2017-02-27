using Discord;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace Hourai.Model {

public class BotDbContext : DbContext {

  // Discord Data
  public DbSet<Guild> Guilds { get; set; }
  public DbSet<Channel> Channels { get; set; }
  public DbSet<User> Users { get; set; }
  public DbSet<Username> Usernames { get; set; }
  public DbSet<GuildUser> GuildUsers { get; set; }
  public DbSet<CustomCommand> Commands { get; set; }

  // Temporary Action Data
  public DbSet<AbstractTempAction> TempActions { get; set; }
  public DbSet<TempBan> TempBans { get; set; }
  public DbSet<TempRole> TempRole { get; set; }

  //// Analytics Data
  //public DbSet<Counter> Counters { get; set; }

  // Service Data
  public DbSet<Subreddit> Subreddits { get; set; }
  public DbSet<SubredditChannel> SubredditChannels { get; set; }

  public bool AllowSave { get; set; } = true;

  protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) {
    if (!Config.IsLoaded) {
      Config.Load();
      Console.WriteLine($"Database File: {Config.DbFilename}");
    }
    optionsBuilder.UseSqlite($"Filename={Config.DbFilename}");
  }

  protected override void OnModelCreating(ModelBuilder builder) {
    builder.Entity<Guild>(b => {
        b.Property(g => g.Prefix).HasDefaultValue("~");
      });
    builder.Entity<Channel>().HasKey(c => new { c.Id, c.GuildId });
    builder.Entity<CustomCommand>(b =>{
        b.HasKey(c => new { c.GuildId, c.Name });
        b.HasIndex(c => c.GuildId);
      });
    builder.Entity<GuildUser>(b => {
        b.HasKey(u => new { u.Id, u.GuildId });
      });
    builder.Entity<Username>(b => {
        b.HasKey(c => new { c.UserId, c.Date });
        b.HasIndex(c => c.UserId);
      });
    builder.Entity<Role>(b => {
        b.HasKey(r => new { r.Id, r.GuildId });
        b.HasOne(r => r.Guild).WithMany(g => g.Roles);
      });
    builder.Entity<UserRole>(b => {
        b.HasKey(r => new { r.UserId, r.GuildId, r.RoleId });
        b.HasOne(r => r.User).WithMany(u => u.Roles).HasForeignKey(r => new { r.UserId, r.GuildId });
        b.HasOne(u => u.Role).WithMany(r => r.Users).HasForeignKey(r => new { r.RoleId, r.GuildId });
      });
  //builder.Entity<CounterEvent>(b => {
        //b.HasKey(c => new { c.CounterId, c.Timestamp });
        //b.HasOne(c => c.Counter)
         //.WithMany(c => c.Events)
         //.HasForeignKey(c => c.CounterId);
        //b.HasOne(c => c.Channel)
         //.WithMany(c => c.Events)
         //.HasForeignKey(c => new { c.ChannelId, c.GuildId });
        //b.HasOne(c => c.GuildUser)
         //.WithMany(u => u.Events)
         //.HasForeignKey(c => new { c.GuildId, c.UserId });
     //});
    builder.Entity<SubredditChannel>(b => {
        b.HasKey(s => new { s.Name, s.ChannelId });
        b.HasOne(c => c.Subreddit)
          .WithMany(s => s.Channels)
          .HasForeignKey(s => s.Name);
        b.HasOne(c => c.Channel)
          .WithMany(s => s.Subreddits)
          .HasForeignKey(c => new { c.ChannelId, c.GuildId });
      });
    builder.Entity<AbstractTempAction>()
        .HasIndex(t => t.Expiration)
        .HasName("IX_temp_actions_Expiration");
  }

  public async Task Save() {
    if(!AllowSave)
      return;
    var changes = await SaveChangesAsync();
    if(changes > 0)
      Log.Info($"Saved {changes} changes to the database.");
  }

  public Guild GetGuild(IGuild iguild) {
    var guild = Guilds.Find(iguild.Id);
    if(guild == null) {
      guild = new Guild(iguild);
      Guilds.Add(guild);
    }
    if (guild.Commands == null)
      Entry(guild).Collection(g => g.Commands).Load();
    if (guild.Channels == null)
      Entry(guild).Collection(g => g.Channels).Load();
    if (guild.Roles == null)
      Entry(guild).Collection(g => g.Roles).Load();
    return guild;
  }

  public bool RemoveGuild(IGuild iguild) {
    var guild = Guilds.Find(iguild.Id);
    if(guild == null)
      return false;
    Guilds.Remove(guild);
    return true;
  }

  public User GetUser(IUser iuser) {
    var user = Users.Find(iuser.Id);
    if(user == null)
      user = Users.Add(new User(iuser)).Entity;
    if (user.Usernames == null)
      Entry(user).Collection(s => s.Usernames).Load();
    return user;
  }

  public bool RemoveUser(IUser iuser) {
    var user = Users.Find(iuser.Id);
    if(user == null)
      return false;
    Users.Remove(user);
    return true;
  }

  public GuildUser GetGuildUser(IGuildUser iuser) {
    var user = GuildUsers.Find(iuser.Id, iuser.Guild.Id);
    if(user == null) {
      user = GuildUsers.Add(new GuildUser(iuser) {
          User = GetUser(iuser)
        }).Entity;
    }
    if (user.Roles == null)
      Entry(user).Collection(s => s.Roles).Load();
    return user;
  }

  public bool RemoveGuildUser(IGuildUser iuser) {
    var user = GuildUsers.Find(iuser.Id, iuser.Guild.Id);
    if (user == null)
      return false;
    GuildUsers.Remove(user);
    return true;
  }

  public Channel GetChannel(IGuildChannel ichannel) {
    var channel = Channels.Find(ichannel.Id, ichannel.Guild.Id);
    if (channel == null)
      channel = Channels.Add(new Channel(ichannel)).Entity;
    if (channel.Subreddits == null)
      Entry(channel).Collection(s => s.Subreddits).Load();
    return channel;
  }

  public bool RemoveChannel(IGuildChannel ichannel) {
    var channel = Channels.Find(ichannel.Id);
    if(channel == null)
      return false;
    Channels.Remove(channel);
    return true;
  }

  public Subreddit GetSubreddit(string name) {
    name = Subreddit.SanitizeName(name);
    var subreddit = Subreddits.Find(name);
    if(subreddit == null) {
      subreddit = Subreddits.Add(new Subreddit {
          Name = name,
          LastPost = DateTimeOffset.Now
        }).Entity;
    }
    if (subreddit.Channels != null)
      Entry(subreddit).Collection(s => s.Channels).Load();
    return subreddit;
  }

}


}
