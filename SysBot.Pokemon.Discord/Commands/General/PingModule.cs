using Discord;
using Discord.Commands;
using System.Diagnostics;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord;

public class PingModule : ModuleBase<SocketCommandContext>
{
    [Command("ping")]
    [Summary("Makes the bot respond, indicating that it is running.")]
    public async Task PingAsync()
    {
        var stopwatch = Stopwatch.StartNew();

        var initialEmbed = new EmbedBuilder()
            .WithTitle("Ping Response")
            .WithDescription("Pong!")
            .WithImageUrl("https://lparchive.org/Pokemon-Yellow/Update%2011/41-Trade_To.gif")
            .WithColor(Color.Green)
            .Build();

        var sentMessage = await ReplyAsync(embed: initialEmbed).ConfigureAwait(false);

        stopwatch.Stop();
        var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;

        var finalEmbed = new EmbedBuilder()
            .WithTitle("Ping Response")
            .WithDescription("Pong!")
            .WithImageUrl("https://lparchive.org/Pokemon-Yellow/Update%2011/41-Trade_To.gif")
            .WithColor(Color.Green)
            .WithFooter($"Response time: {elapsedMs:F0} ms")
            .Build();

        await sentMessage.ModifyAsync(msg => msg.Embed = finalEmbed).ConfigureAwait(false);
    }
}
