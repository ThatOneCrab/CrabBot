using PKHeX.Core;
using SysBot.Pokemon.Discord;
using SysBot.Pokemon.WinForms;
using System.Threading;
using System.Threading.Tasks;

namespace SysBot.Pokemon;

/// <summary>
/// Bot Environment implementation with Integrations added.
/// </summary>
public class PokeBotRunnerImpl<T> : PokeBotRunner<T> where T : PKM, new()
{
    private readonly ProgramConfig _config;

    public PokeBotRunnerImpl(PokeTradeHub<T> hub, BotFactory<T> fac, ProgramConfig config) : base(hub, fac)
    {
        _config = config;
    }

    public PokeBotRunnerImpl(PokeTradeHubConfig config, BotFactory<T> fac, ProgramConfig programConfig) : base(config, fac)
    {
        _config = programConfig;
    }

    protected override void AddIntegrations()
    {
        AddDiscordBot(Hub.Config.Discord.Token);
    }

    private void AddDiscordBot(string apiToken)
    {
        if (string.IsNullOrWhiteSpace(apiToken))
            return;
        var bot = new SysCord<T>(this, _config);
        Task.Run(() => bot.MainAsync(apiToken, CancellationToken.None));
    }

   
    }

