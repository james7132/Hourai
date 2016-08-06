using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;

namespace DrumBot.src {

    public interface IAction {
        Task Do(Server server);
        Task Undo(Server server);
    }

    public abstract class UserAction : IAction {
        public ulong UserId { get; set; }

        protected UserAction(ulong userID) { UserId = userID; }
        protected UserAction(User user) : this(Check.NotNull(user).Id) {}

        protected User GetUser(Server server) {
            var user = Check.NotNull(server).GetUser(UserId);
            if(user == null)
                throw new InvalidOperationException($"No user with ID { UserId } exists on server { server.ToIDString() }");
            return user;
        }

        public abstract Task Do(Server server);
        public abstract Task Undo(Server server);
    }

    public class Ban : UserAction {
        public Ban(ulong userID) : base(userID) {
        }

        public Ban(User user) : base(user) {
        }

        public override async Task Do(Server server) {
            await server.Ban(GetUser(server));
        }

        public override async Task Undo(Server server) { await server.Unban(UserId); }
    }

    public class Kick : UserAction {
        public Kick(ulong userID) : base(userID) {
        }

        public Kick(User user) : base(user) {
        }

        public override async Task Do(Server server) {
            await GetUser(server).Kick();
        }

        public override Task Undo(Server server) { throw new InvalidOperationException("Cannot undo a kick operation.");}
    }

    public class RoleAction : UserAction {

        public ulong[] RoleIds { get; set; }

        public RoleAction(User user, params Role[] roles) : base(user) {
            RoleIds =
                Check.NotNull(roles)
                    .Where(r => r != null)
                    .Select(r => r.Id)
                    .ToArray();
        }

        protected IEnumerable<Role> GetRoles(Server server) {
            Check.NotNull(server);
            foreach (ulong roleId in RoleIds) {
                var role = Check.NotNull(server).GetRole(roleId);
                if (role == null) {
                    Log.Error($"No role with ID { roleId} exists on server { server.ToIDString() }");
                    continue;
                }
                yield return role;
            }
        }

        public override async Task Do(Server server) {
            await GetUser(server).AddRoles(GetRoles(server).ToArray());
        }

        public override async Task Undo(Server server) {
            await GetUser(server).RemoveRoles(GetRoles(server).ToArray());
        }
    }

    public static class ActionExtensions {

        public static UserAction Reverse(this UserAction action) {
            return new ReverseUserAction(action);
        }

        public static async Task Do(this IEnumerable<IAction> actions, Server server) {
            await Task.WhenAll(actions.Select(a => a.Do(server)));
        }

        public static async Task Undo(this IEnumerable<IAction> actions, Server server) {
            await Task.WhenAll(actions.Select(a => a.Undo(server)));
        }

    }

    public class ReverseUserAction : UserAction {
        public UserAction Action { get; set; }

        public ReverseUserAction(UserAction action)
            : base(Check.NotNull(action).UserId) {
        }

        public override Task Do(Server server) {
            return Action.Undo(server);
        }

        public override Task Undo(Server server) {
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

        public Task Do(Server server) { return Action.Do(server); }
        public Task Undo(Server server) { return Action.Undo(server); }
    }
}
