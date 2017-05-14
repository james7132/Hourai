/*using Discord;*/
//using Discord.Commands;
//using Discord.WebSocket;
//using Hourai.Model;
//using System;
//using System.Linq;
//using System.Threading.Tasks;

//namespace Hourai.Standard {

//public partial class Standard : HouraiModule {

  //[Group("quote")]
  //public class Quotes : HouraiModule {

    //[Command]
    //public async Task GetRandom() {
      //await Context.Db.Entry(Context.DbGuild).Collection(g => g.Quotes).LoadAsync();
      //var quotes = Context.DbGuild.Quotes
          //.Where(q => !q.Removed)
          //.OrderBy(q => q.Created).ToArray();
      //if (quotes.Length <= 0) {
        //await RespondAsync("No quotes are available.");
        //return;
      //}
      //var index = RandomUtil.Int(quotes.Length);
      //await RespondAsync(QuoteToResponse(quotes[index]));
    //}

    //[Command("remove")]
    //public async Task Remove(int quoteId) {
      //await Context.Db.Entry(Context.DbGuild).Collection(g => g.Quotes).LoadAsync();
      //if (quoteId < 0 || quoteId >= Context.DbGuild.Quotes.Count) {
        //await RespondAsync("No such quote exists");
        //return;
      //}
      //var quotes = Context.DbGuild.Quotes.OrderBy(q => q.Created).ToArray();
      //quotes[quoteId].Removed = true;
      //await Context.Db.Save();
      //await RespondAsync($"Quote {quoteId} has been removed.");
    //}

    //[Command("add")]
    //[Remarks("Adds a message as a quote via message ID.")]
    //public async Task Add(ulong messageId) {
      //var message = await Context.Channel.GetMessageAsync(messageId);
      //if (string.IsNullOrWhiteSpace(message.Content)) {
        //await RespondAsync("Cannot create an empty quote.");
        //return;
      //}
      //var dbQuote = new Quote {
        //Guild = Context.DbGuild,
        //Author = Context.Message.Author.Username,
        //AuthorId = Context.Message.Author.Id,
        //Content = message.Content,
        //Created = DateTimeOffset.UtcNow,
        //Removed = false
      //};
      //await Db.AddAsync(dbQuote);
      //await Db.Save();
      //await Context.Db.Entry(Context.DbGuild).Collection(g => g.Quotes).LoadAsync();
      //await RespondAsync($"A new quote with ID {Context.DbGuild.Quotes.Count} has been added.");
    //}


    //[Command("add")]
    //public async Task Add([Remainder] string quote = "") {
      //if (string.IsNullOrWhiteSpace(quote)) {
        //await RespondAsync("Cannot create an empty quote.");
        //return;
      //}
      //var dbQuote = new Quote {
        //Guild = Context.DbGuild,
        //Author = Context.Message.Author.Username,
        //AuthorId = Context.Message.Author.Id,
        //Content = quote,
        //Created = DateTimeOffset.UtcNow,
        //Removed = false
      //};
      //await Db.AddAsync(dbQuote);
      //await Db.Save();
      //await Context.Db.Entry(Context.DbGuild).Collection(g => g.Quotes).LoadAsync();
      //await RespondAsync($"A new quote with ID {Context.DbGuild.Quotes.Count} has been added.");
    //}

    //string QuoteToResponse(Quote quote) {
      //var user = Context.Guild.GetUser(quote.AuthorId);
      //string author = user?.Username ?? quote.Author;
      //return quote.Content.MultilineCode() + $"\n   - {author}, {quote.Created}";
    //}

  //}

//}

/*}*/
