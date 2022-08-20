﻿using System;

namespace Core
{
    public interface IAddonReader
    {
        PlayerReader PlayerReader { get; }

        BagReader BagReader { get; }

        EquipmentReader EquipmentReader { get; }

        ActionBarCostReader ActionBarCostReader { get; }

        ActionBarCooldownReader ActionBarCooldownReader { get; }

        AuraTimeReader PlayerBuffTimeReader { get; }

        AuraTimeReader TargetDebuffTimeReader { get; }

        SpellBookReader SpellBookReader { get; }

        TalentReader TalentReader { get; }

        LevelTracker LevelTracker { get; }

        double AvgUpdateLatency { get; }

        int DamageTakenCount();

        string TargetName { get; }

        event Action? AddonDataChanged;
        event Action? PlayerDeath;

        void FetchData();
        void FullReset();

        void Update();
        void UpdateUI();
        void SessionReset();

        int GetInt(int index);
    }
}