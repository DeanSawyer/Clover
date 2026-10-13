using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.IO;
using Sanctuary.Packet;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class BaseAbilityPacketHandler
{
    private static ILogger _logger = null!;
    private static global::Sanctuary.Game.IResourceManager _resourceManager = null!;

    // CloverClientMessages: AbilityPacketRequestAbilityDefinition, body is one int ability id.
    private const short RequestAbilityDefinitionOpCode = 12;

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(BaseAbilityPacketHandler));

        _resourceManager = serviceProvider.GetRequiredService<global::Sanctuary.Game.IResourceManager>();
    }

    public static bool HandlePacket(GatewayConnection connection, PacketReader reader)
    {
        if (!reader.TryRead(out short opCode))
        {
            _logger.LogError("Failed to read opcode from packet. ( Data: {data} )", Convert.ToHexString(reader.Span));
            return false;
        }

        return opCode switch
        {
            AbilityPacketClientRequestStartAbility.OpCode => AbilityPacketClientRequestStartAbilityHandler.HandlePacket(connection, reader.Span),
            RequestAbilityDefinitionOpCode => HandleRequestAbilityDefinition(connection, reader),
            _ => false
        };
    }

    // CloverClientMessages: answer with the name, description and icon of the item that
    // activates this ability (food, transforms, boomboxes...).
    private static bool HandleRequestAbilityDefinition(GatewayConnection connection, PacketReader reader)
    {
        if (!reader.TryRead(out int abilityId))
            return false;

        foreach (var itemDefinition in _resourceManager.ClientItemDefinitions.Values)
        {
            if (itemDefinition.ActivatableAbilityId != abilityId)
                continue;

            connection.SendTunneled(new AbilityPacketAbilityDefinition
            {
                AbilityId = abilityId,
                NameId = itemDefinition.NameId,
                DescriptionId = itemDefinition.DescriptionId,
                IconId = itemDefinition.Icon?.Id ?? 0
            });

            return true;
        }

        global::Sanctuary.Game.Eye.Log("ability-def-unknown", connection.Player, new { abilityId });

        return true;
    }
}