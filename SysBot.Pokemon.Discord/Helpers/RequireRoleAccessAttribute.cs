using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord;

/// <summary>
/// Requires an assigned role in order to accept commands. Can be used by sudo users if satisfied.
/// </summary>
public sealed class RequireRoleAccessAttribute(string RoleName) : PreconditionAttribute
{
    // Create a field to store the specified name

    // Create a constructor so the name can be specified

    public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
    {
        var mgr = SysCordSettings.Manager;
        if (mgr.Config.AllowGlobalSudo && mgr.CanUseSudo(context.User.Id))
            return PreconditionResult.FromSuccess();

        // Attempt to obtain the guild user via context, cache, or REST fallback
        SocketGuildUser? gUser = context.User as SocketGuildUser;
        if (gUser == null && context.Guild is SocketGuild guild)
            gUser = guild.GetUser(context.User.Id);

        if (gUser == null && context.Guild is SocketGuild restGuild && context.Client is DiscordSocketClient socketClient)
        {
            try
            {
                var restUser = await socketClient.Rest.GetGuildUserAsync(restGuild.Id, context.User.Id).ConfigureAwait(false);
                if (restUser != null)
                {
                    var roleNames = restGuild.Roles.Where(r => restUser.RoleIds.Contains(r.Id)).Select(r => r.Name);
                    if (mgr.CanUseSudo(roleNames))
                        return PreconditionResult.FromSuccess();
                    if (!mgr.GetHasRoleAccess(RoleName, roleNames))
                        return PreconditionResult.FromError("You do not have the required role to run this command.");
                    return PreconditionResult.FromSuccess();
                }
            }
            catch
            {
                // ignore and fall through
            }
        }

        if (gUser == null)
            return PreconditionResult.FromError("You must be sending the message from a guild to run this command or member information is unavailable.");

        var roles = gUser.Roles;
        if (mgr.CanUseSudo(roles.Select(z => z.Name)))
            return PreconditionResult.FromSuccess();

        if (!mgr.GetHasRoleAccess(RoleName, roles.Select(z => z.Name)))
            return PreconditionResult.FromError("You do not have the required role to run this command.");

        return PreconditionResult.FromSuccess();
    }
}
