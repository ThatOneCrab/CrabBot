using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord;

public sealed class RequireSudoAttribute : PreconditionAttribute
{
    // Override the CheckPermissions method
    public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
    {
        var mgr = SysCordSettings.Manager;
        if (mgr.Config.AllowGlobalSudo && mgr.CanUseSudo(context.User.Id))
            return PreconditionResult.FromSuccess();

        // Attempt to obtain the guild user. If not available, try to locate via the guild's cache or REST as a fallback.
        SocketGuildUser? gUser = context.User as SocketGuildUser;
        SocketRole[] roles = Array.Empty<SocketRole>();

        if (gUser == null && context.Guild is SocketGuild guild)
            gUser = guild.GetUser(context.User.Id);

        if (gUser != null)
        {
            roles = gUser.Roles.ToArray();
        }
        else if (context.Guild is SocketGuild restGuild && context.Client is DiscordSocketClient socketClient)
        {
            try
            {
                var restUser = await socketClient.Rest.GetGuildUserAsync(restGuild.Id, context.User.Id).ConfigureAwait(false);
                if (restUser != null)
                {
                    // Map role IDs to role names using the guild's roles collection
                    var roleNames = restGuild.Roles.Where(r => restUser.RoleIds.Contains(r.Id)).Select(r => r.Name);
                    if (mgr.CanUseSudo(roleNames))
                        return PreconditionResult.FromSuccess();
                }
            }
            catch
            {
                // Ignore REST errors and fall through to error result
            }
        }

        if (roles.Length > 0)
        {
            if (mgr.CanUseSudo(roles.Select(z => z.Name)))
                return PreconditionResult.FromSuccess();
        }

        // Since it wasn't, fail
        return PreconditionResult.FromError("You are not permitted to run this command or member information is unavailable.");
    }
}
