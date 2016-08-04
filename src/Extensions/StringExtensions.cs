using System;
using System.Text;
using Discord;

namespace DrumBot {
    public static class StringExtensions {

        class StringBuilderWrapper : IDisposable {
            string Wrapper { get; }
            StringBuilder Builder { get; }
            public StringBuilderWrapper(StringBuilder builder, string wrapper) {
                Builder = builder;
                Wrapper = wrapper;
                Builder?.Append(wrapper);
            }

            public void Dispose() { Builder?.Append(Wrapper); }
        }

        const string CodeWrapper = "``";
        const string MultiLineCodeWrapper = "```";
        const string BoldWrapper = "**";
        const string ItalicizeWrapper = "*";
        const string UnderlineWrapper = "__";
        const string StrikethroughWrapper = "~~";
        const string QuoteWrapper = "'";
        const string DoubleQuoteWrapper = "\"";

        public static string Wrap(this string s, string w) => w + s + w; 
        public static string Code(this string s) => s.Wrap(CodeWrapper);
        public static string MultilineCode(this string s) => s.Wrap(MultiLineCodeWrapper);
        public static string Bold(this string s) => s.Wrap(BoldWrapper);
        public static string Italicize(this string s) => s.Wrap(ItalicizeWrapper);
        public static string Underline(this string s) => s.Wrap(UnderlineWrapper);
        public static string Strikethrough(this string s) => s.Wrap(StrikethroughWrapper);
        public static string Quote(this string s) => s.Wrap(QuoteWrapper);
        public static string DoubleQuote(this string s) => s.Wrap(DoubleQuoteWrapper);

        public static IDisposable Wrap(this StringBuilder builder, string wrapper) => new StringBuilderWrapper(builder, wrapper);
        public static IDisposable Code(this StringBuilder builder) => builder.Wrap(CodeWrapper);
        public static IDisposable MultilineCode(this StringBuilder builder) => builder.Wrap(MultiLineCodeWrapper);
        public static IDisposable Bold(this StringBuilder builder) => builder.Wrap(BoldWrapper);
        public static IDisposable Italicize(this StringBuilder builder) => builder.Wrap(ItalicizeWrapper);
        public static IDisposable Underline(this StringBuilder builder) => builder.Wrap(UnderlineWrapper);
        public static IDisposable Strikethrough(this StringBuilder builder) => builder.Wrap(StrikethroughWrapper);
        public static IDisposable Quote(this StringBuilder builder) => builder.Wrap(QuoteWrapper);
        public static IDisposable DoubleQuote(this StringBuilder builder) => builder.Wrap(DoubleQuoteWrapper);

        public static string ToIDString(this Channel channel) => $"#{channel.Name} ({channel.Id})";
        public static string ToIDString(this Server server) => $"{server.Name} ({server.Id})";
        public static string ToIDString(this Profile user) => $"{user.Name} ({user.Id})";
        public static string ToIDString(this User user) => $"{user.Name} ({user.Id})";

    }
}
