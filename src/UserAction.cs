using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;

namespace DrumBot {

    public interface IAction {
        Task Do(IGuild server);
        Task Undo(IGuild server);
    }

    public abstract class UserAction : IAction {
        public ulong UserId { get; set; }

        protected UserAction(ulong userID) { UserId = userID; }
        protected UserAction(IGuildUser user) : this(Check.NotNull(user).Id) {}

        protected async Task<IGuildUser> GetUser(IGuild server) {
            var user = await Check.NotNull(server).GetUserAsync(UserId);
            if(user == null)
                throw new InvalidOperationException($"No user with ID { UserId } exists on server { server.ToIDString() }");
            return user;
        }

        public abstract Task Do(IGuild server);
        public abstract Task Undo(IGuild server);
    }

    public class Ban : UserAction {
        public Ban(ulong userID) : base(userID) {
        }

        public Ban(IGuildUser user) : base(user) {
        }

        public override async Task Do(IGuild server) {
            await server.AddBanAsync(await GetUser(server));
        }

        public override async Task Undo(IGuild server) {
            await server.RemoveBanAsync(UserId);
        }
    }

    public class Kick : UserAction {
        public Kick(ulong userID) : base(userID) {
        }

        public Kick(IGuildUser user) : base(user) {
        }

        public override async Task Do(IGuild server) {
            var user = await GetUser(server);
            await user.KickAsync();
        }

        public override Task Undo(IGuild server) { throw new InvalidOperationException("Cannot undo a kick operation.");}
    }

    public class RoleAction : UserAction {

        public ulong[] RoleIds { get; set; }

        public RoleAction(IGuildUser user, params IRole[] roles) : base(user) {
            RoleIds = Check.NotNull(roles)
                        .Where(r => r != null)
                        .Select(r => r.Id)
                        .ToArray();
        }

        protected IEnumerable<IRole> GetRoles(IGuild server) {
            Check.NotNull(server);
            return RoleIds.Select(server.GetRole);
        }

        public override async Task Do(IGuild server) {
            var user = await GetUser(server);
            await user.AddRolesAsync(GetRoles(server));
        }

        public override async Task Undo(IGuild server) {
            var user = await GetUser(server);
            await user.RemoveRolesAsync(GetRoles(server));
        }
    }

    public static class ActionExtensions {

        public static UserAction Reverse(this UserAction action) {
            return new ReverseUserAction(action);
        }

        public static async Task Do(this IEnumerable<IAction> actions, IGuild server) {
            await Task.WhenAll(actions.Select(a => a.Do(server)));
        }

        public static async Task Undo(this IEnumerable<IAction> actions, IGuild server) {
            await Task.WhenAll(actions.Select(a => a.Undo(server)));
        }

    }

    public class ReverseUserAction : UserAction {
        public UserAction Action { get; set; }

        public ReverseUserAction(UserAction action)
            : base(Check.NotNull(action).UserId) {
        }

        public override Task Do(IGuild server) {
            return Action.Undo(server);
        }

        public override Task Undo(IGuild server) {
            return Action.Do(server);
        }
    }

    public class TempUserAction : IAction {

        public IAction Action { get; set; }
        public DateTime Expiration { get; set; }

        public TempUserAction(IAction action, DateTime expiration) {
            if(expiration <= DateTime.Now)
                throw new ArgumentException();
            Action = Check.NotNull(action);
            Expiration = expiration;
        }

        public Task Do(IGuild server) { return Action.Do(server); }
        public Task Undo(IGuild server) { return Action.Undo(server); }
    }
}
