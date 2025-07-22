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
            .WithDescription($"Please try again. If the issue persists, restart your switch and check your internet connection.\n\n**Reason**: {reason}")
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
            thumbnailUrl = "https://media.discordapp.net/attachments/1234791557396172874/1389388345708122163/Mystery_Egg.png?ex=6864703b&is=68631ebb&hm=befeec4cff4e25c1c16ebd5ed26e5fc875e2b0a06f6b140138551419b6225978&=";
        }
        else if (isMysteryMon)
        {
            thumbnailUrl = "https://media.discordapp.net/attachments/1234791557396172874/1389388345708122163/Mystery_Egg.png?ex=6864703b&is=68631ebb&hm=befeec4cff4e25c1c16ebd5ed26e5fc875e2b0a06f6b140138551419b6225978&=";
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
