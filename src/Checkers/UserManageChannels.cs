namespace DrumBot {
    public class UserManageChannels : ManageChannels {
        protected override bool OwnerOverride { get; } = true;
        protected override bool CheckBot { get; } = false;
    }
}