using System.Linq;

using Microsoft.EntityFrameworkCore;

using Sanctuary.Database;
using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;

namespace Sanctuary.Game.ChatCommands;

public class LeadChatCommand : IChatCommand
{
    private readonly IChatCommandManager _chatCommandManager;
    private readonly IDbContextFactory<DatabaseContext> _dbContextFactory;
    private readonly IZoneManager _zoneManager;

    public string KeyWord => "lead";

    public string Usage => "grant|revoke mod|admin <player>";

    public string Description => "Grants or revokes Moderator/Admin status for a player. Disconnects them if online so it takes effect immediately.";

    public ChatCommandRole RequiredRole => ChatCommandRole.Lead;

    public LeadChatCommand(IChatCommandManager chatCommandManager, IDbContextFactory<DatabaseContext> dbContextFactory, IZoneManager zoneManager)
    {
        _chatCommandManager = chatCommandManager;
        _dbContextFactory = dbContextFactory;
        _zoneManager = zoneManager;
    }

    private void LogAction(Player invoker, string action, string targetName, string? detail = null)
    {
        _chatCommandManager.LogAction(this, invoker, action, targetName, detail);
    }

    public bool Handle(Player invoker, string[] args)
    {
        if (args.Length < 3)
            return false;

        var action = args[0].ToLower();
        var role = args[1].ToLower();
        var targetName = string.Join(' ', args[2..]);

        if (action != "grant" && action != "revoke")
            return false;

        if (role != "mod" && role != "admin")
            return false;

        if (invoker.Name.FullName == targetName)
        {
            ChatHelper.SendSystemMessage(invoker, "You cannot change your own Mod/Admin status.");
            return true;
        }

        using DatabaseContext dbContext = _dbContextFactory.CreateDbContext();

        var target = dbContext.Characters
            .Where(character => character.FullName == targetName)
            .Select(character => new { character.UserId, character.User.IsAdmin, character.User.IsMod, character.User.IsLead })
            .SingleOrDefault();

        if (target is null)
        {
            ChatHelper.SendSystemMessage(invoker, $"No player named \"{targetName}\" was found.");
            return false;
        }

        ChatCommandRole targetRole = ChatHelper.GetRoleFromFlags(target.IsLead, target.IsAdmin, target.IsMod);
        if (!(invoker.ChatCommandRole > targetRole))
        {
            ChatHelper.SendSystemMessage(invoker, "You don't have permission to target this player.");
            return false;
        }

        bool grant = action == "grant";
        bool isMod = role == "mod";

        if (isMod)
        {
            dbContext.Users
                .Where(user => user.Id == target.UserId)
                .ExecuteUpdate(user => user
                    .SetProperty(u => u.IsMod, grant));
        }
        else
        {
            dbContext.Users
                .Where(user => user.Id == target.UserId)
                .ExecuteUpdate(user => user
                    .SetProperty(u => u.IsAdmin, grant));
        }

        bool wasOnline = _zoneManager.TryGetPlayer(targetName, out var targetPlayer);
        if (wasOnline)
            targetPlayer.Disconnect();

        LogAction(invoker, grant ? "Grant" : "Revoke", targetName, isMod ? "Mod" : "Admin");

        string roleLabel = isMod ? "Moderator" : "Admin";
        string actionLabel = grant ? "granted" : "revoked";
        string disconnectNote = wasOnline ? " and disconnected to apply it." : ".";

        ChatHelper.SendSystemMessage(invoker, $"{targetName} has had {roleLabel} status {actionLabel}{disconnectNote}");
        return true;
    }
}
