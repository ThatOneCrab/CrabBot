using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord;

public sealed class RequireRoleAttribute(string RoleName) : PreconditionAttribute
{
    // Create a field to store the specified name

    // Create a constructor so the name can be specified

    // Override the CheckPermissions method
    public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
    {
        // Attempt to obtain the guild user via context or guild cache
        SocketGuildUser? gUser = context.User as SocketGuildUser;
        if (gUser == null && context.Guild is SocketGuild guild)
            gUser = guild.GetUser(context.User.Id);

        if (gUser != null)
        {
            // If this command was executed by a user with the appropriate role, return a success
            if (gUser.Roles.Any(r => r.Name == RoleName))
                return PreconditionResult.FromSuccess();

            // Since it wasn't, fail
            return PreconditionResult.FromError($"You must have a role named {RoleName} to run this command.");
        }

        // Fallback to REST in case the member is not cached
        if (context.Guild is SocketGuild restGuild && context.Client is DiscordSocketClient socketClient)
        {
            try
            {
                var restUser = await socketClient.Rest.GetGuildUserAsync(restGuild.Id, context.User.Id).ConfigureAwait(false);
                if (restUser != null)
                {
                    if (restGuild.Roles.Any(r => restUser.RoleIds.Contains(r.Id) && r.Name == RoleName))
                        return PreconditionResult.FromSuccess();

                    return PreconditionResult.FromError($"You must have a role named {RoleName} to run this command.");
                }
            }
            catch
            {
                // Ignore REST failures and fall through to a graceful error
            }
        }

        return PreconditionResult.FromError("You must be in a guild to run this command or member information is unavailable.");
    }
}
