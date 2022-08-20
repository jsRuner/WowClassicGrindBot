local Load = select(2, ...)
local DataToColor = unpack(Load)
local Range = DataToColor.Libs.RangeCheck

local bit = bit
local band = bit.band
local date = date

local floor = math.floor

local tonumber = tonumber
local sub = string.sub
local gsub = string.gsub
local find = string.find
local len = string.len
local upper = string.upper
local byte = string.byte
local strsplit = strsplit

local C_Map = C_Map
local UnitExists = UnitExists
local GetUnitName = GetUnitName
local UnitReaction = UnitReaction
local GetInventorySlotInfo = GetInventorySlotInfo
local GetInventoryItemCount = GetInventoryItemCount
local CheckInteractDistance = CheckInteractDistance
local UnitGUID = UnitGUID

local GetActionInfo = GetActionInfo
local GetMacroSpell = GetMacroSpell
local GetSpellPowerCost = GetSpellPowerCost
local GetSpellBaseCooldown = GetSpellBaseCooldown
local GetInventoryItemLink = GetInventoryItemLink
local IsSpellInRange = IsSpellInRange
local GetSpellInfo = GetSpellInfo
local GetActionCooldown = GetActionCooldown
local IsUsableAction = IsUsableAction
local GetActionTexture = GetActionTexture
local IsCurrentAction = IsCurrentAction
local IsAutoRepeatAction = IsAutoRepeatAction

local IsUsableSpell = IsUsableSpell

local GetNumSkillLines = GetNumSkillLines
local GetSkillLineInfo = GetSkillLineInfo

local UnitIsGhost = UnitIsGhost
local C_DeathInfo = C_DeathInfo
local UnitAttackSpeed = UnitAttackSpeed

-- bits

local UnitAffectingCombat = UnitAffectingCombat
local GetWeaponEnchantInfo = GetWeaponEnchantInfo
local UnitIsDead = UnitIsDead
local UnitIsPlayer = UnitIsPlayer
local UnitName = UnitName
local UnitIsDeadOrGhost = UnitIsDeadOrGhost
local UnitCharacterPoints = UnitCharacterPoints
local UnitPlayerControlled = UnitPlayerControlled
local GetShapeshiftForm = GetShapeshiftForm
local GetShapeshiftFormInfo = GetShapeshiftFormInfo
local GetInventoryItemBroken = GetInventoryItemBroken
local GetInventoryItemDurability = GetInventoryItemDurability
local UnitOnTaxi = UnitOnTaxi
local IsSwimming = IsSwimming
local IsFalling = IsFalling
local IsIndoors = IsIndoors
local IsStealthed = IsStealthed
local GetMirrorTimerInfo = GetMirrorTimerInfo
local IsMounted = IsMounted
local IsInGroup = IsInGroup

local UnitIsTapDenied = UnitIsTapDenied
local IsAutoRepeatSpell = IsAutoRepeatSpell
local IsCurrentSpell = IsCurrentSpell
local UnitIsVisible = UnitIsVisible
local GetPetHappiness = GetPetHappiness

local ammoSlot = GetInventorySlotInfo("AmmoSlot")

DataToColor.unitClassification = {
    ["normal"] = 1,
    ["trivial"] = 2,
    ["minus"] = 4,
    ["rare"] = 8,
    ["elite"] = 16,
    ["rareelite"] = 32,
    ["worldboss"] = 64
}

-- Use Astrolabe function to get current player position
function DataToColor:GetPosition()
    if DataToColor.map ~= nil then
        local pos = C_Map.GetPlayerMapPosition(DataToColor.map, DataToColor.C.unitPlayer)
        if pos ~= nil then
            return pos:GetXY()
        end
    end
    return 0, 0
end

-- Base 2 converter for up to 24 boolean values to a single pixel square.
function DataToColor:Bits1()
    -- 1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192, 16384

    local mainHandEnchant, _, _, _, offHandEnchant = GetWeaponEnchantInfo()

    return
        (UnitAffectingCombat(DataToColor.C.unitTarget) and 1 or 0) +
        (UnitIsDead(DataToColor.C.unitTarget) and 2 or 0) ^ 1 +
        (UnitIsDeadOrGhost(DataToColor.C.unitPlayer) and 2 or 0) ^ 2 +
        (UnitCharacterPoints(DataToColor.C.unitPlayer) > 0 and 2 or 0) ^ 3 +
        (UnitExists(DataToColor.C.unitmouseover) and 2 or 0) ^ 4 +
        ((UnitReaction(DataToColor.C.unitPlayer, DataToColor.C.unitTarget) or 0) <= 4 and 2 or 0) ^ 5 + -- isHostile
        (UnitIsVisible(DataToColor.C.unitPet) and not UnitIsDead(DataToColor.C.unitPet) and 2 or 0) ^ 6 +
        (mainHandEnchant and 2 or 0) ^ 7 +
        (offHandEnchant and 2 or 0) ^ 8 +
        DataToColor:GetInventoryBroken() ^ 9 +
        (UnitOnTaxi(DataToColor.C.unitPlayer) and 2 or 0) ^ 10 +
        (IsSwimming() and 2 or 0) ^ 11 +
        (GetPetHappiness() == 3 and 2 or 0) ^ 12 +
        (GetInventoryItemCount(DataToColor.C.unitPlayer, ammoSlot) > 0 and 2 or 0) ^ 13 +
        (UnitAffectingCombat(DataToColor.C.unitPlayer) and 2 or 0) ^ 14 +
        (DataToColor:IsUnitsTargetIsPlayerOrPet(DataToColor.C.unitTarget, DataToColor.C.unitTargetTarget) and 2 or 0) ^ 15 +
        (IsAutoRepeatSpell(DataToColor.C.Spell.AutoShotId) and 2 or 0) ^ 16 +
        (UnitExists(DataToColor.C.unitTarget) and 2 or 0) ^ 17 +
        (IsMounted() and 2 or 0) ^ 18 +
        (IsAutoRepeatSpell(DataToColor.C.Spell.ShootId) and 2 or 0) ^ 19 +
        (IsCurrentSpell(DataToColor.C.Spell.AttackId) and 2 or 0) ^ 20 +
        (UnitIsPlayer(DataToColor.C.unitTarget) and 2 or 0) ^ 21 +
        (UnitIsTapDenied(DataToColor.C.unitTarget) and 2 or 0) ^ 22 +
        (IsFalling() and 2 or 0) ^ 23
end

function DataToColor:Bits2()
    local type, _, _, scale = GetMirrorTimerInfo(2)
    return
        (type == DataToColor.C.MIRRORTIMER.BREATH and scale < 0 and 1 or 0) +
        DataToColor.corpseInRange ^ 1 +
        (IsIndoors() and 2 or 0) ^ 2 +
        (UnitExists(DataToColor.C.unitFocus) and 2 or 0) ^ 3 +
        (UnitAffectingCombat(DataToColor.C.unitFocus) and 2 or 0) ^ 4 +
        (UnitExists(DataToColor.C.unitFocusTarget) and 2 or 0) ^ 5 +
        (UnitAffectingCombat(DataToColor.C.unitFocusTarget) and 2 or 0) ^ 6 +
        ((UnitReaction(DataToColor.C.unitPlayer, DataToColor.C.unitFocusTarget) or 0) <= 4 and 2 or 0) ^ 7 + -- isHostile
        (UnitIsDead(DataToColor.C.unitmouseover) and 2 or 0) ^ 8 +
        (UnitIsDead(DataToColor.C.unitPetTarget) and 2 or 0) ^ 9 +
        (IsStealthed() and 2 or 0) ^ 10 +
        (UnitIsTrivial(DataToColor.C.unitTarget) and 2 or 0) ^ 11 +
        (UnitIsTrivial(DataToColor.C.unitmouseover) and 2 or 0) ^ 12 +
        (UnitIsTapDenied(DataToColor.C.unitmouseover) and 2 or 0) ^ 13 +
        ((UnitReaction(DataToColor.C.unitPlayer, DataToColor.C.unitmouseover) or 0) <= 4 and 2 or 0) ^ 14 + -- isHostile
        (UnitIsPlayer(DataToColor.C.unitmouseover) and 2 or 0) ^ 15 +
        (DataToColor:IsUnitsTargetIsPlayerOrPet(DataToColor.C.unitmouseover, DataToColor.C.unitmouseovertarget) and 2 or 0) ^ 16 +
        (UnitPlayerControlled(DataToColor.C.unitmouseover) and 2 or 0) ^ 17 +
        (UnitPlayerControlled(DataToColor.C.unitTarget) and 2 or 0) ^ 18
end

function DataToColor:CustomTrigger(t)
    local v = t[0]
    for i = 1, 23 do
        v = v + (t[i] ^ i)
    end
    return v
end

function DataToColor:Set(trigger, input)
    if input == true then input = 1 end
    local v = tonumber(input) or 0
    if v > 0 then v = 1 end
    if trigger >= 0 and trigger <= 23 then
        DataToColor.customTrigger1[trigger] = v
    end
end

function DataToColor:getAuraMaskForClass(func, unitId, table)
    local mask = 0
    for k, v in pairs(table) do
        for i = 1, 24 do
            local name, texture = func(unitId, i)
            if name == nil then
                break
            end
            if v[texture] or find(name, v[1]) then
                mask = mask + (2 ^ k)
                break
            end
        end
    end
    return mask
end

function DataToColor:populateAuraTimer(func, unitId, queue)
    local count = 0
    for i = 1, 40 do
        local name, texture, _, _, duration, expirationTime = func(unitId, i)
        if name == nil then
            break
        end
        count = i

        if queue ~= nil then
            if duration == 0 then
                expirationTime = GetTime() + 14400 -- 4 hours - anything above considered unlimited duration
            end

            if not queue:exists(texture) then
                queue:set(texture, expirationTime)
                --DataToColor:Print(texture, " aura added ", expirationTime)
            elseif not queue:isDirty(texture) and queue:value(texture) < expirationTime then
                queue:set(texture, expirationTime)
                --DataToColor:Print(texture, " aura updated ", expirationTime)
            end
        end
    end
    return count
end

-- Pass in a string to get the upper case ASCII values. Converts any special character with ASCII values below 100
local function StringToASCIIHex(str)
    -- Converts string to upper case so only 2 digit ASCII values
    -- All lowercase letters have a decimal ASCII value >100, so we only uppercase numbers which are a mere 2 digits long.
    str = sub(upper(str), 0, 6)
    -- Sets string to an empty string
    local ASCII = ''
    -- Loops through all of string passed to it and converts to upper case ASCII values
    for i = 1, len(str) do
        -- Assigns the specific value to a character to then assign to the ASCII string/number
        local c = sub(str, i, i)
        -- Concatenation of old string and new character
        ASCII = ASCII .. byte(c)
    end
    return tonumber(ASCII)
end

-- Grabs current targets name
function DataToColor:GetTargetName(partition)
    if UnitExists(DataToColor.C.unitTarget) then
        local target = GetUnitName(DataToColor.C.unitTarget)
        target = StringToASCIIHex(target)
        if partition < 3 then
            return tonumber(sub(target, 0, 6))
        else if target > 999999 then
                return tonumber(sub(target, 7, 12))
            end
        end
    end
    return 0
end

function DataToColor:CastingInfoSpellId(unitId)
    local _, _, _, startTime, endTime, _, _, _, spellID = DataToColor.UnitCastingInfo(unitId)

    if spellID ~= nil then
        if unitId == DataToColor.C.unitPlayer and startTime ~= DataToColor.lastCastStartTime then
            DataToColor.lastCastStartTime = startTime
            DataToColor.lastCastEndTime = endTime
            DataToColor.CastNum = DataToColor.CastNum + 1
        end
        return spellID
    end

    local _, _, _, startTime, endTime, _, _, spellID = DataToColor.UnitChannelInfo(unitId)
    if spellID ~= nil then
        if unitId == DataToColor.C.unitPlayer and startTime ~= DataToColor.lastCastStartTime then
            DataToColor.lastCastStartTime = startTime
            DataToColor.lastCastEndTime = endTime
            DataToColor.CastNum = DataToColor.CastNum + 1
        end
        return spellID
    end

    if unitId == DataToColor.C.unitPlayer then
        DataToColor.lastCastEndTime = 0
    end

    return 0
end

--

function DataToColor:getRange()
    local min, max = Range:GetRange(DataToColor.C.unitTarget)
    return (max or 0) * 1000 + (min or 0)
end

function DataToColor:NpcId(unit)
    local guid = UnitGUID(unit) or ""
    local id = tonumber(guid:match("-(%d+)-%x+$"), 10)
    if id and guid:match("%a+") ~= "Player" then
        return id
    end
    return 0
end

function DataToColor:getGuidFromUnit(unit)
    -- Player-4731-02AAD4FF
    -- Creature-0-4488-530-222-19350-000005C0D70
    -- Pet-0-4448-530-222-22123-15004E200E
    if UnitExists(unit) then
        return DataToColor:uniqueGuid(select(-2, strsplit('-', UnitGUID(unit))))
    end
    return 0
end

function DataToColor:getGuidFromUUID(uuid)
    return DataToColor:uniqueGuid(select(-2, strsplit('-', uuid)))
end

function DataToColor:uniqueGuid(npcId, spawn)
    local spawnEpochOffset = band(tonumber(sub(spawn, 5), 16), 0x7fffff)
    local spawnIndex = band(tonumber(sub(spawn, 1, 5), 16), 0xffff8)

    local dd = date("*t", spawnEpochOffset)
    return (
        dd.day +
        dd.hour +
        dd.min +
        dd.sec +
        npcId +
        spawnIndex
    ) % 0x1000000
end

local offsetEnumPowerType = 2
function DataToColor:populateActionbarCost(slot)
    local actionType, id = GetActionInfo(slot)
    if actionType == DataToColor.C.ActionType.Macro then
        id = GetMacroSpell(id)
    end

    if id and actionType == DataToColor.C.ActionType.Spell or actionType == DataToColor.C.ActionType.Macro then
        local costTable = GetSpellPowerCost(id)
        if costTable ~= nil then
            for order, costInfo in ipairs(costTable) do
                -- cost negative means it produces that type of powertype...
                if costInfo.cost > 0 then
                    local meta = 100000 * slot + 10000 * order + costInfo.type + offsetEnumPowerType
                    --print(slot, actionType, order, costInfo.type, costInfo.cost, GetSpellLink(id), meta)
                    DataToColor.actionBarCostQueue:set(meta, costInfo.cost)
                end
            end
        end
    end
    -- default value mana with zero cost
    DataToColor.actionBarCostQueue:set(100000 * slot + 10000 + offsetEnumPowerType, 0)
end

function DataToColor:equipSlotItemId(slot)
    if slot == nil then
        return 0
    end
    local equip
    if GetInventoryItemLink(DataToColor.C.unitPlayer, slot) == nil then
        equip = 0
    else _, _, equip = find(GetInventoryItemLink(DataToColor.C.unitPlayer, slot), DataToColor.C.ItemPattern)
        equip = gsub(equip, 'm:', '')
    end
    return tonumber(equip or 0)
end

-- -- Function to tell if a spell is on cooldown and if the specified slot has a spell assigned to it
-- -- Slot ID information can be found on WoW Wiki. Slots we are using: 1-12 (main action bar), Bottom Right Action Bar maybe(49-60), and  Bottom Left (61-72)

function DataToColor:areSpellsInRange()
    local inRange = 0
    local targetCount = #DataToColor.S.spellInRangeTarget
    for i = 1, targetCount do
        if IsSpellInRange(GetSpellInfo(DataToColor.S.spellInRangeTarget[i]), DataToColor.C.unitTarget) == 1 then
            inRange = inRange + (2 ^ (i - 1))
        end
    end

    for i = 1, #DataToColor.S.spellInRangeUnit do
        local data = DataToColor.S.spellInRangeUnit[i]
        if IsSpellInRange(GetSpellInfo(data[1]), data[2]) == 1 then
            inRange = inRange + (2 ^ (targetCount + i - 1))
        end
    end

    local c = #DataToColor.S.interactInRangeUnit
    for i = 1, c do
        local data = DataToColor.S.interactInRangeUnit[i]
        if CheckInteractDistance(data[1], data[2]) then
            inRange = inRange + (2 ^ (24 - c + i - 1))
        end
    end

    return inRange
end

function DataToColor:isActionUseable(min, max)
    local isUsableBits = 0
    for i = min, max do
        local start, duration, enabled = GetActionCooldown(i)
        local isUsable, notEnough = IsUsableAction(i)
        local texture = GetActionTexture(i)
        local spellName = DataToColor.S.playerSpellBookName[texture]

        if start == 0 and (isUsable == true and notEnough == false or IsUsableSpell(spellName)) and texture ~= 134400 then -- red question mark texture
            isUsableBits = isUsableBits + (2 ^ (i - min))
        end

        local _, spellId = GetActionInfo(i)
        local gcd = 0
        if DataToColor.S.playerSpellBookId[spellId] then
            gcd = select(2, GetSpellBaseCooldown(spellId))
        end

        if enabled == 1 and start ~= 0 and (duration * 1000) > gcd and not DataToColor.actionBarCooldownQueue:exists(i) then
            local expireTime = start + duration
            DataToColor.actionBarCooldownQueue:set(i, expireTime)
        end
    end
    return isUsableBits
end

function DataToColor:isCurrentAction(min, max)
    local isUsableBits = 0
    for i = min, max do
        if IsCurrentAction(i) or IsAutoRepeatAction(i) then
            isUsableBits = isUsableBits + (2 ^ (i - min))
        end
    end
    return isUsableBits
end

-- Finds passed in string to return profession level
function DataToColor:GetProfessionLevel(skill)
    local numskills = GetNumSkillLines()
    for c = 1, numskills do
        local skillname, _, _, skillrank = GetSkillLineInfo(c)
        if (skillname == skill) then
            return tonumber(skillrank)
        end
    end
    return 0
end

function DataToColor:GetCorpsePosition()
    if UnitIsGhost(DataToColor.C.unitPlayer) then
        local corpseMap = C_DeathInfo.GetCorpseMapPosition(DataToColor.map)
        if corpseMap ~= nil then
            return corpseMap:GetXY()
        end
    end
    return 0, 0
end

function DataToColor:getMeleeAttackSpeed(unit)
    local main, off = UnitAttackSpeed(unit)
    return 10000 * floor((off or 0) * 100) + floor((main or 0) * 100)
end

function DataToColor:getAvgEquipmentDurability()
    local c = 0
    local m = 0
    for i = 1, 18 do
        local cc, mm = GetInventoryItemDurability(i)
        c = c + (cc or 0)
        m = m + (mm or 0)
    end
    return max(0, floor((c + 1)* 100 / (m + 1)) - 1) -- 0-99
end

-----------------------------------------------------------------
-- Boolean functions --------------------------------------------
-- Only put functions here that are part of a boolean sequence --
-- Sew BELOW for examples ---------------------------------------
-----------------------------------------------------------------

function DataToColor:shapeshiftForm()
    local index = GetShapeshiftForm(false)
    if index == nil or index == 0 then
        return 0
    end

    local _, _, _, spellId = GetShapeshiftFormInfo(index)
    local form = DataToColor.S.playerAuraMap[spellId]
    if form ~= nil then
        return form
    end
    return index
end

function DataToColor:GetInventoryBroken()
    for i = 1, 18 do
        if GetInventoryItemBroken(DataToColor.C.unitPlayer, i) then
            return 2
        end
    end
    return 0
end

function DataToColor:UnitsTargetAsNumber(unit, unittarget)
    if not (UnitName(unittarget)) then return 2 end -- target has no target
    if DataToColor.C.CHARACTER_NAME == UnitName(unit) then return 0 end -- targeting self
    if UnitName(DataToColor.C.unitPet) == UnitName(unittarget) then return 4 end -- targetting my pet
    if DataToColor.playerPetSummons[UnitGUID(unittarget)] then return 4 end
    if DataToColor.C.CHARACTER_NAME == UnitName(unittarget) then return 1 end -- targetting me
    if UnitName(DataToColor.C.unitPet) == UnitName(unit) and
        UnitName(unittarget) ~= nil then return 5 end
    if IsInGroup() and DataToColor:UnitTargetsPartyOrPet(unittarget) then return 6 end
    return 3
end

function DataToColor:UnitTargetsPartyOrPet(unittarget)
    for i = 1, 4 do
        local unit = DataToColor.C.unitParty .. i
        if UnitExists(unit) == false then
            return false
        end
        local name = UnitName(unit)
        if name == UnitName(unittarget) then return true end

        unit = DataToColor.C.unitParty .. i .. DataToColor.C.unitPet
        if UnitExists(unit) == false then
            return false
        end
        name = UnitName(unit)
        if name == UnitName(unittarget) then return true end
    end
    return false
end

-- Returns true if target of our target is us
function DataToColor:IsUnitsTargetIsPlayerOrPet(unit, unittarget)
    local x = DataToColor:UnitsTargetAsNumber(unit, unittarget)
    return x == 1 or x == 4
end
