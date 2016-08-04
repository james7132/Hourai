namespace DrumBot {
    public class UserKickMembersChecker : KickMembersChecker {
        protected override bool OwnerOverride { get; } = true;
        protected override bool CheckBot { get; } = false;
    }
}