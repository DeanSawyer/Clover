using System;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Database;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class PlayerUpdatePacketUpdatePositionHandler
{
    private static ILogger _logger = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(PlayerUpdatePacketUpdatePositionHandler));

        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
    }

    public static bool HandlePacket(GatewayConnection connection, Span<byte> data)
    {
        if (!PlayerUpdatePacketUpdatePosition.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(PlayerUpdatePacketUpdatePosition));
            return false;
        }

        // _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(PlayerUpdatePacketUpdatePosition), packet);

        packet.Guid = connection.Player.Guid;

        if (!connection.Player.ValidateClientPosition(packet.Position))
        {
            _logger.LogWarning("{connection} sent repeated implausible position updates ({packet}); disconnecting.", connection, nameof(PlayerUpdatePacketUpdatePosition));
            ReportAntiCheatKick(connection, nameof(PlayerUpdatePacketUpdatePosition));
            connection.Disconnect();
            return false;
        }

        connection.Player.UpdatePosition(packet.Position, packet.Rotation);

        connection.Player.SendTunneledToVisible(packet);

        return true;
    }

    private static void ReportAntiCheatKick(GatewayConnection connection, string reason)
    {
        global::Sanctuary.Game.Eye.Log("anticheat-kick", connection.Player, new { reason });

        try
        {
            using var dbContext = _dbContextFactory.CreateDbContext();

            var characterName = $"{connection.Player.Name.FirstName} {connection.Player.Name.LastName}".Trim();

            dbContext.Database.ExecuteSqlInterpolated($@"
                INSERT INTO AntiCheatKicks (UserId, CharacterName, Reason, OccurredAt, Processed)
                VALUES ({connection.Player.UserId}, {characterName}, {reason}, UTC_TIMESTAMP(), 0)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record anti-cheat kick for {connection}.", connection);
        }
    }
}
