﻿using Microsoft.Extensions.Logging;

using System;

using SharedLib.Extensions;

namespace Core;

public sealed partial class MouseOverBlacklist : IBlacklist
{
    private readonly string[] blacklist;

    private readonly ILogger<MouseOverBlacklist> logger;

    private readonly AddonReader addonReader;
    private readonly PlayerReader playerReader;
    private readonly AddonBits bits;
    private readonly CombatLog combatLog;

    private readonly int above;
    private readonly int below;
    private readonly bool checkMouseOverGivesExp;
    private readonly UnitClassification mask;

    private readonly bool allowPvP;

    private int lastGuid;

    public MouseOverBlacklist(ILogger<MouseOverBlacklist> logger,
        AddonReader addonReader, PlayerReader playerReader,
        AddonBits bits, ClassConfiguration classConfig, CombatLog combatLog)
    {
        this.logger = logger;

        this.addonReader = addonReader;
        this.playerReader = playerReader;
        this.bits = bits;
        this.combatLog = combatLog;

        this.above = classConfig.NPCMaxLevels_Above;
        this.below = classConfig.NPCMaxLevels_Below;

        this.checkMouseOverGivesExp = classConfig.CheckTargetGivesExp;
        this.mask = classConfig.TargetMask;

        this.blacklist = classConfig.Blacklist;

        this.allowPvP = classConfig.AllowPvP;

        logger.LogInformation($"{nameof(classConfig.TargetMask)}: {string.Join(", ", mask.GetIndividualFlags())}");

        if (blacklist.Length > 0)
            logger.LogInformation($"Name: {string.Join(", ", blacklist)}");
    }

    public bool Is()
    {
        if (!bits.MouseOver())
        {
            lastGuid = 0;
            return false;
        }
        else if (combatLog.DamageTaken.Contains(playerReader.MouseOverGuid))
        {
            return false;
        }

        if (playerReader.PetTarget() && playerReader.MouseOverGuid == playerReader.PetGuid)
        {
            return true;
        }

        // it is trying to kill me
        if (bits.MouseOverTarget_PlayerOrPet())
        {
            return false;
        }

        if (!mask.HasFlagF(playerReader.MouseOverClassification))
        {
            if (lastGuid != playerReader.MouseOverGuid)
            {
                LogClassification(logger, playerReader.MouseOverId, playerReader.MouseOverGuid, addonReader.MouseOverName, playerReader.MouseOverClassification.ToStringF());
                lastGuid = playerReader.MouseOverGuid;
            }

            return true; // ignore non white listed unit classification
        }

        if (!allowPvP && (bits.MouseOver_Player() || bits.MouseOver_PlayerControlled()))
        {
            if (lastGuid != playerReader.MouseOverGuid)
            {
                LogPlayerOrPet(logger, playerReader.MouseOverId, playerReader.MouseOverGuid, addonReader.MouseOverName);
                lastGuid = playerReader.MouseOverGuid;
            }

            return true; // ignore players and pets
        }

        if (!bits.MouseOver_Dead() && bits.MouseOver_Tagged())
        {
            if (lastGuid != playerReader.MouseOverGuid)
            {
                LogTagged(logger, playerReader.MouseOverId, playerReader.MouseOverGuid, addonReader.MouseOverName);
                lastGuid = playerReader.MouseOverGuid;
            }

            return true; // ignore tagged mobs
        }


        if (bits.MouseOver_Hostile() && playerReader.MouseOverLevel > playerReader.Level.Value + above)
        {
            if (lastGuid != playerReader.MouseOverGuid)
            {
                LogLevelHigh(logger, playerReader.MouseOverId, playerReader.MouseOverGuid, addonReader.MouseOverName);
                lastGuid = playerReader.MouseOverGuid;
            }

            return true; // ignore if current level + 2
        }

        if (checkMouseOverGivesExp)
        {
            if (bits.MouseOver_Trivial())
            {
                if (lastGuid != playerReader.MouseOverGuid)
                {
                    LogNoExperienceGain(logger, playerReader.MouseOverId, playerReader.MouseOverGuid, addonReader.MouseOverName);
                    lastGuid = playerReader.MouseOverGuid;
                }
                return true;
            }
        }
        else if (bits.MouseOver_Hostile() && playerReader.MouseOverLevel < playerReader.Level.Value - below)
        {
            if (lastGuid != playerReader.MouseOverGuid)
            {
                LogLevelLow(logger, playerReader.MouseOverId, playerReader.MouseOverGuid, addonReader.MouseOverName);
                lastGuid = playerReader.MouseOverGuid;
            }
            return true; // ignore if current level - 7
        }

        if (blacklist.Length > 0 && Contains())
        {
            if (lastGuid != playerReader.MouseOverGuid)
            {
                LogNameMatch(logger, playerReader.MouseOverId, playerReader.MouseOverGuid, addonReader.MouseOverName);
                lastGuid = playerReader.MouseOverGuid;
            }
            return true;
        }

        return false;
    }

    private bool Contains()
    {
        for (int i = 0; i < blacklist.Length; i++)
        {
            if (addonReader.MouseOverName.Contains(blacklist[i], StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    #region logging

    [LoggerMessage(
        EventId = 0060,
        Level = LogLevel.Warning,
        Message = "({id},{guid},{name}) is player!")]
    static partial void LogPlayerOrPet(ILogger logger, int id, int guid, string name);

    [LoggerMessage(
        EventId = 0061,
        Level = LogLevel.Warning,
        Message = "({id},{guid},{name}) is tagged!")]
    static partial void LogTagged(ILogger logger, int id, int guid, string name);

    [LoggerMessage(
        EventId = 0062,
        Level = LogLevel.Warning,
        Message = "({id},{guid},{name}) too high level!")]
    static partial void LogLevelHigh(ILogger logger, int id, int guid, string name);

    [LoggerMessage(
        EventId = 0063,
        Level = LogLevel.Warning,
        Message = "({id},{guid},{name}) too low level!")]
    static partial void LogLevelLow(ILogger logger, int id, int guid, string name);

    [LoggerMessage(
        EventId = 0064,
        Level = LogLevel.Warning,
        Message = "({id},{guid},{name}) not yield experience!")]
    static partial void LogNoExperienceGain(ILogger logger, int id, int guid, string name);

    [LoggerMessage(
        EventId = 0065,
        Level = LogLevel.Warning,
        Message = "({id},{guid},{name}) name match!")]
    static partial void LogNameMatch(ILogger logger, int id, int guid, string name);

    [LoggerMessage(
        EventId = 0066,
        Level = LogLevel.Warning,
        Message = "({id},{guid},{name},{classification}) not defined in the TargetMask!")]
    static partial void LogClassification(ILogger logger, int id, int guid, string name, string classification);

    #endregion
}