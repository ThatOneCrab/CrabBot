using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord;

/// <summary>
/// Same as <see cref="RequireRoleAccessAttribute"/> with extra consideration for bots accepting Queue requests.
/// </summary>
public sealed class RequireQueueRoleAttribute(string RoleName) : PreconditionAttribute
{
    // Create a field to store the specified name

    // Create a constructor so the name can be specified

    public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
    {
        var mgr = SysCordSettings.Manager;
        if (mgr.Config.AllowGlobalSudo && mgr.CanUseSudo(context.User.Id))
            return PreconditionResult.FromSuccess();

        // Attempt to obtain the guild user via context, guild cache, or REST fallback
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

                    bool canQueueByRest = SysCordSettings.HubConfig.Queues.CanQueue;
                    if (!canQueueByRest)
                        return PreconditionResult.FromError("Sorry, I am not currently accepting queue requests!");

                    if (!mgr.GetHasRoleAccess(RoleName, roleNames))
                        return PreconditionResult.FromError("You do not have the required role to run this command.");

                    return PreconditionResult.FromSuccess();
                }
            }
            catch
            {
                // Ignore REST lookup failures and fall through to the standard missing-member handling.
            }
        }

        if (gUser == null)
            return PreconditionResult.FromError("You must be sending the message from a guild to run this command or member information is unavailable.");

        var roles = gUser.Roles;
        if (mgr.CanUseSudo(roles.Select(z => z.Name)))
            return PreconditionResult.FromSuccess();

        bool canQueue = SysCordSettings.HubConfig.Queues.CanQueue;
        if (!canQueue)
            return PreconditionResult.FromError("Sorry, I am not currently accepting queue requests!");

        if (!mgr.GetHasRoleAccess(RoleName, roles.Select(z => z.Name)))
            return PreconditionResult.FromError("You do not have the required role to run this command.");

        return PreconditionResult.FromSuccess();
    }
}
