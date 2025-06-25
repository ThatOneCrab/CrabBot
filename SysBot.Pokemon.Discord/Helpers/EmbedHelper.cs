using Discord;
using PKHeX.Core;
using SysBot.Pokemon.Helpers;
using System;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord;

public static class EmbedHelper
{
    public static async Task SendNotificationEmbedAsync(IUser user, string message)
    {
        var embed = new EmbedBuilder()
            .WithTitle("Notice")
            .WithDescription(message)
            .WithTimestamp(DateTimeOffset.Now)
            .WithThumbnailUrl("")
            .WithColor(Color.Red)
            .Build();

        await user.SendMessageAsync(embed: embed).ConfigureAwait(false);
    }

    public static async Task SendTradeCanceledEmbedAsync(IUser user, string reason)
    {
        var embed = new EmbedBuilder()
            .WithTitle("Your Trade was Canceled...")
            .WithDescription($"Your trade was canceled.\nPlease try again. If the issue persists, restart your switch and check your internet connection.\n\n**Reason**: {reason}")
            .WithTimestamp(DateTimeOffset.Now)
            .WithThumbnailUrl("")
            .WithColor(Color.Red)
            .Build();

        await user.SendMessageAsync(embed: embed).ConfigureAwait(false);
    }

    public static async Task SendTradeCodeEmbedAsync(IUser user, int code)
    {
        var embed = new EmbedBuilder()
            .WithTitle("Here's your trade code!")
            .WithDescription($"# {code:0000 0000}")
            .WithTimestamp(DateTimeOffset.Now)
            .WithThumbnailUrl("")
            .WithColor(Color.Blue)
            .Build();

        await user.SendMessageAsync(embed: embed).ConfigureAwait(false);
    }

    public static async Task SendTradeFinishedEmbedAsync<T>(IUser user, string message, T pk, bool isMysteryMon, bool isMysteryEgg)
        where T : PKM, new()
    {
        string thumbnailUrl;

        if (isMysteryEgg)
        {
            thumbnailUrl = "https://media.discordapp.net/attachments/1234791557396172874/1387499892158627972/pm0000_00_00_00_big.png?ex=685d9178&is=685c3ff8&hm=4b7308b5a2b1a8c1c06c8ab4f6a84cd9f865b96325e4163e7283bcb5ad0a8877&=&format=webp&quality=lossless";
        }
        else if (isMysteryMon)
        {
            thumbnailUrl = "https://media.discordapp.net/attachments/1234791557396172874/1387499974044029019/pm1091_00_00_00_big.png?ex=685d918c&is=685c400c&hm=9f6f6ba3a6d5b79253b2991f1e3a2dd8dfc465d37b8ed7bbb51edac5b776f665&=&format=webp&quality=lossless";
        }
        else
        {
            thumbnailUrl = TradeExtensions<T>.PokeImg(pk, false, true, null);
        }

        var embed = new EmbedBuilder()
            .WithTitle("Trade Completed!")
            .WithDescription(message)
            .WithTimestamp(DateTimeOffset.Now)
            .WithThumbnailUrl(thumbnailUrl)
            .WithColor(Color.Teal)
            .Build();

        await user.SendMessageAsync(embed: embed).ConfigureAwait(false);
    }

    public static async Task SendTradeInitializingEmbedAsync(IUser user, string speciesName, int code, bool isMysteryMon, bool isMysteryEgg, string? message = null)
    {
        if (isMysteryEgg)
        {
            speciesName = "**Mystery Egg**";
        }
        else if (isMysteryMon)
        {
            speciesName = "**Mystery Pokémon**";
        }

        var embed = new EmbedBuilder()
            .WithTitle("Loading the Trade Portal...")
            .WithDescription($"**Pokemon**: {speciesName}\n**Trade Code**: {code:0000 0000}")
            .WithTimestamp(DateTimeOffset.Now)
            .WithThumbnailUrl("")
            .WithColor(Color.Orange);

        if (!string.IsNullOrEmpty(message))
        {
            embed.WithDescription($"{embed.Description}\n\n{message}");
        }

        var builtEmbed = embed.Build();
        await user.SendMessageAsync(embed: builtEmbed).ConfigureAwait(false);
    }

    public static async Task SendTradeSearchingEmbedAsync(IUser user, string trainerName, string inGameName, string? message = null)
    {
        var embed = new EmbedBuilder()
            .WithTitle($"Now Searching for You, {trainerName}...")
            .WithDescription($"**Waiting for**: {trainerName}\n**My IGN**: {inGameName}")
            .WithTimestamp(DateTimeOffset.Now)
            .WithThumbnailUrl("")
            .WithColor(Color.Green);

        if (!string.IsNullOrEmpty(message))
        {
            embed.WithDescription($"{embed.Description}\n\n{message}");
        }

        var builtEmbed = embed.Build();
        await user.SendMessageAsync(embed: builtEmbed).ConfigureAwait(false);
    }
}
