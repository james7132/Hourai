namespace DrumBot {
    public class BotManageChannels : ManageChannels {
        protected override bool CheckUser { get; } = false;
    }
}