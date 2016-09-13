using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Discord;

namespace DrumBot {

public static class StringExtensions {

  static readonly Regex CamelCaseRegex;
  static readonly Regex WhitespaceRegex;

  static StringExtensions() {
    CamelCaseRegex = new Regex("([a-z](?=[A-Z])|[A-Z](?=[A-Z][a-z]))", RegexOptions.Compiled);
    WhitespaceRegex = new Regex(@"\s+", RegexOptions.Compiled);
  }

  class StringBuilderWrapper : IDisposable {
    string Wrapper { get; }
    StringBuilder Builder { get; }
    bool Diff { get; }
    
    public StringBuilderWrapper(StringBuilder builder, string wrapper, bool diff = false) {
      Builder = builder;
      Wrapper = wrapper;
      Diff = diff;
      if (Diff)
        Builder?.Append(Wrapper[0]);
      else
        Builder?.Append(wrapper);
    }

    public void Dispose() {
    if (Diff)
      Builder?.Append(Wrapper[1]);
    else
      Builder?.Append(Wrapper);
    }
  }

  public static string Wrap(this string s, string w) => w + s + w;
  public static string Wrap(this string s, char l, char r) => l + s + r;
  public static string Code(this string s) => $"`{s}`";
  public static string MultilineCode(this string s) => $"```{s}```";
  public static string Bold(this string s) => $"**{s}**";
  public static string Italicize(this string s) => $"*{s}*";
  public static string Underline(this string s) => $"__{s}__";
  public static string Strikethrough(this string s) => $"~~{s}~~";
  public static string Quote(this string s) => $"'{s}'";
  public static string DoubleQuote(this string s) => $"\"{s}\"";
  public static string SquareBracket(this string s) => $"[{s}]";
  public static string AngleBracket(this string s) => $"<{s}>";
  public static string CurlyBrace(this string s) => $"{{{s}}}";
  public static string Parentheses(this string s) => $"({s}))";

  public static IDisposable Wrap(this StringBuilder builder, string wrapper) => new StringBuilderWrapper(builder, wrapper);
  public static IDisposable Code(this StringBuilder builder) => builder.Wrap("``");
  public static IDisposable MultilineCode(this StringBuilder builder) => builder.Wrap("```");
  public static IDisposable Bold(this StringBuilder builder) => builder.Wrap("**");
  public static IDisposable Italicize(this StringBuilder builder) => builder.Wrap("*");
  public static IDisposable Underline(this StringBuilder builder) => builder.Wrap("__");
  public static IDisposable Strikethrough(this StringBuilder builder) => builder.Wrap("~~");
  public static IDisposable Quote(this StringBuilder builder) => builder.Wrap("'");
  public static IDisposable DoubleQuote(this StringBuilder builder) => builder.Wrap("\"");

  public static string ToTitleCase(this string str) {
      var tokens = str.Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries);
      var builder = new StringBuilder();
      for (var i = 0; i < tokens.Length; i++) {
          var token = tokens[i];
          tokens[i] = token.Substring(0, 1).ToUpper() + token.Substring(1);
      }
      return string.Join(" ", tokens);
  }

  public static bool IsNullOrEmpty(this string str) => string.IsNullOrEmpty(str);
  public static string NullIfEmpty(this string str) => str.IsNullOrEmpty() ? null : str;

  public static string Remove(this string str, string substring) => str.Replace(substring, string.Empty);

  public static string[] SplitWhitespace(this string str)
      => WhitespaceRegex.Replace(str.Trim(), " ").Split(' ');

  public static string Join(this IEnumerable<string> strings,
                            string delimiter = null) {
    if (delimiter.IsNullOrEmpty())
      return string.Join(string.Empty, strings);
    else
      return string.Join(delimiter, strings);
  }

  public static string SplitCamelCase(this string str, string delimiter = " ")
    => CamelCaseRegex.Replace(str, $"$1{delimiter}");

  public static string ToIDString(this IGuildChannel channel) 
    => $"#{channel.Name} ({channel.Id})";
  public static string ToIDString(this IGuild server) 
    => $"{server.Name} ({server.Id})";
  public static string ToIDString(this IUser user) 
    => $"{user.Username} ({user.Id})";
}

}
