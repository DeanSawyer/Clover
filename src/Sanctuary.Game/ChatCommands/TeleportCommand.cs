using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;

using Sanctuary.Game;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Game.ChatCommands;

public class TeleportCommand : IChatCommand
{
    public string KeyWord => "teleport";
    public string Usage => "<x> <y> <z>";
    public string Description => "Teleports you to the specified coordinates.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Mod;

    public bool Handle(Player invoker, string[] args)
    {
        if (args.Length != 3)
        {
            ChatHelper.SendSystemMessage(invoker, "Invalid usage. Use: !teleport <x> <y> <z>");
            return false;
        }

        if (!float.TryParse(args[0], out float x) || !float.TryParse(args[1], out float y) || !float.TryParse(args[2], out float z))
        {
            ChatHelper.SendSystemMessage(invoker, "Invalid coordinates. Please enter valid numbers.");
            return false;
        }

        var position = new System.Numerics.Vector4(x, y, z, invoker.Position.W);

        // Bug fix: this previously re-applied the player's OLD position instead
        // of the new x/y/z, so the server's own tracking never actually moved -
        // only the client's displayed position did. Beyond being wrong on its
        // own, that left the server thinking the next real movement packet from
        // this player started at their pre-teleport spot, which the position
        // anti-cheat (Player.ValidateClientPosition) would have flagged as an
        // impossible jump the moment they took a single step after !teleport.
        invoker.UpdatePosition(position, System.Numerics.Quaternion.Identity, false);
        invoker.ResetMovementValidation();

        var clientUpdatePacketUpdateLocation = new ClientUpdatePacketUpdateLocation
        {
            Position = position,
            Rotation = System.Numerics.Quaternion.Identity,
            Teleport = true
        };
        invoker.SendTunneled(clientUpdatePacketUpdateLocation);
        ChatHelper.SendSystemMessage(invoker, $"Teleported to ({x:F2}, {y:F2}, {z:F2})");
        return true;
    }
}
