using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Logging;

using Sanctuary.Core.IO;
using Sanctuary.Game.Entities;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game;

/// <summary>
/// The "all seeing eye": a continuous audit trail of what every player does.
/// Every call becomes one JSON line in Logs/Eye/ (NLog logger name "Eye").
/// Auditing must never break gameplay, so every entry point swallows its own errors.
/// </summary>
public static class Eye
{
    private static ILogger? _logger;

    // Per-player count of every packet type sent this session. Routine packets are
    // only counted (one summary line at logout keeps the log small); unhandled or
    // unreadable packets, which is what modified clients tend to send, are logged at once.
    private static readonly ConcurrentDictionary<ulong, ConcurrentDictionary<string, long>> _packetCounts = new();

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static void Initialize(ILoggerFactory loggerFactory)
    {
        _logger ??= loggerFactory.CreateLogger("Eye");
    }

    public static void Log(string evt, Player? player, object? data = null)
    {
        var logger = _logger;

        if (logger is null)
            return;

        try
        {
            var entry = new Dictionary<string, object?>
            {
                ["evt"] = evt
            };

            if (player is not null)
            {
                entry["guid"] = player.Guid;
                entry["name"] = player.Name?.FullName;
                entry["user"] = player.UserId;
                entry["zone"] = player.Zone?.Name;
                entry["pos"] = new[]
                {
                    MathF.Round(player.Position.X, 1),
                    MathF.Round(player.Position.Y, 1),
                    MathF.Round(player.Position.Z, 1)
                };
                entry["job"] = player.ActiveProfileId;
                entry["coins"] = player.Coins;
            }

            if (data is not null)
                entry["data"] = data;

            logger.LogInformation("{EyeJson}", JsonSerializer.Serialize(entry, _jsonOptions));
        }
        catch (Exception)
        {
            // Never let auditing interfere with the game.
        }
    }

    public static void Packet(Player? player, byte[] payload, bool handled, string channel, bool highFrequency)
    {
        if (_logger is null || player is null)
            return;

        try
        {
            string name;

            if (highFrequency)
            {
                name = "Movement/Camera/TimeSync";
            }
            else
            {
                try
                {
                    var reader = new PacketReader(payload);
                    name = reader.ReadTunneledPacketName();
                }
                catch (Exception)
                {
                    name = "Unreadable";
                }
            }

            var counts = _packetCounts.GetOrAdd(player.Guid, _ => new ConcurrentDictionary<string, long>());
            counts.AddOrUpdate(name, 1, (_, count) => count + 1);

            if (!handled || name == "Unreadable" || name.StartsWith("Unknown", StringComparison.Ordinal))
                Log("packet-unhandled", player, new { name, handled, channel, size = payload.Length });
        }
        catch (Exception)
        {
            // Never let auditing interfere with the game.
        }
    }

    public static IReadOnlyDictionary<string, long> TakePacketCounts(ulong guid)
    {
        if (!_packetCounts.TryRemove(guid, out var counts))
            return new Dictionary<string, long>();

        return new SortedDictionary<string, long>(counts);
    }
}
