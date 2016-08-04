namespace DrumBot {
    public class UserBanMembersChecker : BanMembersChecker {
        protected override bool OwnerOverride { get; } = true;
        protected override bool CheckBot { get; } = false;
    }
}