using Discord;

namespace DrumBot {
    public class ManageChannels : PermissionChecker {
        protected override string PermissionName => "Manage Channels";
        protected override bool Check(User user) => user.ServerPermissions.ManageChannels; 
    }
}