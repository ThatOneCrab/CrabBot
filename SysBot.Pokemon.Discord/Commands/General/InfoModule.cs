using Discord;
using Discord.Commands;
using PKHeX.Core;
using SysBot.Base;
using SysBot.Pokemon.Helpers;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord;

// src: https://github.com/foxbot/patek/blob/master/src/Patek/Modules/InfoModule.cs
// ISC License (ISC)
// Copyright 2017, Christopher F. <foxbot@protonmail.com>
public class InfoModule<T> : ModuleBase<SocketCommandContext> where T : PKM, new()
{
    private readonly PokeTradeHub<T> Hub = SysCord<T>.Runner.Hub;
    private const string Version = CrabBot.Version;
    private const string dev = "https://discord.gg/FnAGV4pb2k";
    private const string detail = "I am an open-source Discord bot powered by PKHeX.Core and other open-source software, modified by Crab.";

    [Command("Info")]
    [Alias("about", "whoami", "owner")]
    public async Task InfoAsync()
    {
        var app = await Context.Client.GetApplicationInfoAsync().ConfigureAwait(false);
        var game = typeof(T) switch
        {
            Type type when type == typeof(PA9) => "Pokémon Legends: ZA",
            Type type when type == typeof(PK9) => "Scarlet & Violet",
            Type type when type == typeof(PK8) => "Sword & Shield",
            Type type when type == typeof(PA8) => "Pokémon Legends: Arceus",
            Type type when type == typeof(PB8) => "Brilliant Diamond & Shining Pearl",
            _ => "Let's Go Pikachu & Eevee"
        };

        var builder = new EmbedBuilder
        {
            Title = "Here's a bit about me!",
            Description = detail,
            Color = Color.Green,
            ThumbnailUrl = "https://raw.githubusercontent.com/ThatOneCrab/sprites/refs/heads/main/Cyberklawf.png",
            // To be added later
            //ImageUrl = ""
        };

        builder.AddField("Info",
            $"- {Format.Bold("Owner")}: {app.Owner.Username} ({app.Owner.Id})\n" +
            $"- {Format.Bold("Current Botmode")}: {game}\n" +
            $"- {Format.Bold("CrabBot Version")}: {Version}\n" +
            $"- {Format.Bold("PKHeX.Core Version")}: {GetVersionInfo("PKHeX.Core")}\n" +
            $"- {Format.Bold("AutoLegality Version")}: {GetVersionInfo("PKHeX.Core.AutoMod")}\n" +
            $"- {Format.Bold("Buildtime")}: {GetVersionInfo("SysBot.Base", false)}\n" +
            $"- {Format.Bold("Built on kwsch [SysBot.NET](https://github.com/kwsch/SysBot.NET)")}\n" +
            $"- {Format.Bold($"Dev Server: [Crab's Dev Server]({dev})")}"
            );
        builder.AddField("Stats",
            $"- {Format.Bold("Uptime")}: {GetUptime()}\n" +
            $"- {Format.Bold("Runtime")}: {RuntimeInformation.FrameworkDescription} {RuntimeInformation.ProcessArchitecture} " +
            $"({RuntimeInformation.OSDescription} {RuntimeInformation.OSArchitecture})\n" +
            $"- {Format.Bold("Heap Size")}: {GetHeapSize()}MiB\n" +
            $"- {Format.Bold("Guilds")}: {Context.Client.Guilds.Count}\n" +
            $"- {Format.Bold("Channels")}: {Context.Client.Guilds.Sum(g => g.Channels.Count)}\n" +
            $"- {Format.Bold("Users")}: {Context.Client.Guilds.Sum(g => g.MemberCount)}\n"
        );

        await ReplyAsync(embed: builder.Build()).ConfigureAwait(false);
    }
    private static string GetHeapSize() => Math.Round(GC.GetTotalMemory(true) / (1024.0 * 1024.0), 2).ToString(CultureInfo.CurrentCulture);

    private static string GetUptime() => (DateTime.Now - Process.GetCurrentProcess().StartTime).ToString(@"dd\.hh\:mm\:ss");

    private static string GetVersionInfo(string assemblyName, bool inclVersion = true)
    {
        const string _default = "Unknown";
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        var assembly = Array.Find(assemblies, x => x.GetName().Name == assemblyName);

        var attribute = assembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        if (attribute is null)
            return _default;

        var info = attribute.InformationalVersion;
        var split = info.Split('+');
        if (split.Length >= 2)
        {
            var version = split[0];
            var revision = split[1];
            if (DateTime.TryParseExact(revision, "yyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var buildTime))
                return (inclVersion ? $"{version} " : "") + $@"{buildTime:yy-MM-dd\.hh\:mm}";
            return inclVersion ? version : _default;
        }
        return _default;
    }
}
