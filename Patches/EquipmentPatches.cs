﻿using Bloodcraft.Systems.Expertise;
using Bloodcraft.Systems.Legacies;
using Bloodcraft.Systems.Legacy;
using Bloodcraft.Systems.Professions;
using HarmonyLib;
using ProjectM;
using ProjectM.Gameplay.Systems;
using ProjectM.Network;
using ProjectM.Shared;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using static Bloodcraft.Systems.Expertise.WeaponStats.WeaponStatManager;
using static Bloodcraft.Systems.Legacies.BloodStats.BloodStatManager;

namespace Bloodcraft.Patches;

[HarmonyPatch]
internal static class EquipmentPatches
{

    [HarmonyPatch(typeof(WeaponLevelSystem_Destroy), nameof(WeaponLevelSystem_Destroy.OnUpdate))]
    [HarmonyPostfix]
    static void WeaponLevelDestroyPostfix(WeaponLevelSystem_Destroy __instance)
    {
        NativeArray<Entity> entities = __instance.__query_1111682408_0.ToEntityArray(Allocator.Temp);
        try
        {
            foreach (Entity entity in entities)
            {
                if (entity.Equals(Entity.Null)) continue;
                if (Plugin.LevelingSystem.Value && entity.Has<EntityOwner>() && entity.Read<EntityOwner>().Owner.Has<PlayerCharacter>())
                {
                    EntityOwner entityOwner = entity.Read<EntityOwner>();
                    GearOverride.SetLevel(entityOwner.Owner);
                }
            }
        }
        catch (Exception e)
        {
            Core.Log.LogError($"Exited LevelPrefix early: {e}");
        }
        finally
        {
            entities.Dispose();
        }
    }
    [HarmonyPatch(typeof(WeaponLevelSystem_Spawn), nameof(WeaponLevelSystem_Spawn.OnUpdate))]
    [HarmonyPrefix]
    static void OnUpdatePrefix(WeaponLevelSystem_Spawn __instance)
    {
        NativeArray<Entity> entities = __instance.__query_1111682356_0.ToEntityArray(Allocator.Temp);
        try
        {
            foreach (Entity entity in entities)
            {
                if (entity.Equals(Entity.Null)) continue;
                if (Plugin.ExpertiseSystem.Value && Plugin.Sanguimancy.Value && entity.Has<WeaponLevel>() && entity.Has<EntityOwner>() && entity.Read<EntityOwner>().Owner.Has<PlayerCharacter>())
                {
                    Entity character = entity.Read<EntityOwner>().Owner;
                    ulong steamId = character.Read<PlayerCharacter>().UserEntity.Read<User>().PlatformId;

                    PrefabGUID prefab = entity.Read<PrefabGUID>();
                    ExpertiseSystem.WeaponType weaponType = ExpertiseSystem.GetWeaponTypeFromPrefab(entity.Read<PrefabGUID>());
                    if (weaponType.Equals(ExpertiseSystem.WeaponType.FishingPole)) continue;
                    if (weaponType.Equals(ExpertiseSystem.WeaponType.Unarmed))
                    {
                        ModifyUnitStatBuffUtils.ApplyWeaponBonuses(character, weaponType, entity);
                        Core.ModifyUnitStatBuffSystem_Spawn.OnUpdate();
                    }
                }
                else if (!Plugin.ExpertiseSystem.Value)
                {
                    // want to safely restore weapon level sources here while accounting for upgraded weapons that have different sources compared to base prefab
                }
            }
        }
        catch (Exception e)
        {
            Core.Log.LogWarning($"Exited WeaponLevelSystem_Spawn system early: (note - if this is happening at first character spawn it just kinda does that, can't get it to stop and doesn't seem to affect anything else so moving on with my life for now) {e}");
        }
        finally
        {
            entities.Dispose();
        }
        
    }

    [HarmonyPatch(typeof(WeaponLevelSystem_Spawn), nameof(WeaponLevelSystem_Spawn.OnUpdate))]
    [HarmonyPostfix]
    static void OnUpdatePostfix(WeaponLevelSystem_Spawn __instance)
    {
        NativeArray<Entity> entities = __instance.__query_1111682356_0.ToEntityArray(Allocator.Temp);
        try
        {
            foreach (Entity entity in entities)
            {
                if (entity.Equals(Entity.Null)) continue;
                if (Plugin.ExpertiseSystem.Value && Plugin.LevelingSystem.Value && entity.Has<EntityOwner>() && entity.Read<EntityOwner>().Owner.Has<PlayerCharacter>())
                {
                    Entity character = entity.Read<EntityOwner>().Owner;
                    ulong steamId = character.Read<PlayerCharacter>().UserEntity.Read<User>().PlatformId;

                    PrefabGUID prefab = entity.Read<PrefabGUID>();

                    ExpertiseSystem.WeaponType weaponType = ExpertiseSystem.GetWeaponTypeFromPrefab(entity.Read<PrefabGUID>());
                    if (!weaponType.Equals(ExpertiseSystem.WeaponType.Unarmed) && !weaponType.Equals(ExpertiseSystem.WeaponType.FishingPole)) GearOverride.SetWeaponItemLevel(character.Read<Equipment>(), ExpertiseHandlerFactory.GetExpertiseHandler(weaponType).GetExpertiseData(steamId).Key, Core.EntityManager);

                    Entity player = entity.Read<EntityOwner>().Owner;
                    GearOverride.SetLevel(player);
                }
                else if (Plugin.LevelingSystem.Value && !Plugin.ExpertiseSystem.Value && entity.Has<EntityOwner>() && entity.Read<EntityOwner>().Owner.Has<PlayerCharacter>())
                {
                    Entity character = entity.Read<EntityOwner>().Owner;
                    GearOverride.SetLevel(character);
                }
            }
        }
        catch (Exception e)
        {
            Core.Log.LogError($"Exited LevelPrefix early: {e}");
        }
        finally
        {
            entities.Dispose();
        }
    }

    [HarmonyPatch(typeof(ArmorLevelSystem_Spawn), nameof(ArmorLevelSystem_Spawn.OnUpdate))]
    [HarmonyPrefix]
    static void ArmorLevelSpawnPrefix(ArmorLevelSystem_Spawn __instance)
    {
        NativeArray<Entity> entities = __instance.__query_663986227_0.ToEntityArray(Allocator.Temp);
        try
        {
            foreach (Entity entity in entities)
            {
                if (Plugin.LevelingSystem.Value && entity.Has<EntityOwner>() && entity.Read<EntityOwner>().Owner.Has<PlayerCharacter>())
                {
                    if (entity.Has<ArmorLevel>()) entity.Write(new ArmorLevel { Level = 0f });
                }
            }
        }
        catch (Exception e)
        {
            Core.Log.LogError($"Exited LevelPrefix early: {e}");
        }
        finally
        {
            entities.Dispose();
        }
    }
    [HarmonyPatch(typeof(ArmorLevelSystem_Spawn), nameof(ArmorLevelSystem_Spawn.OnUpdate))]
    [HarmonyPostfix]
    static void ArmorLevelSpawnPostfix(ArmorLevelSystem_Spawn __instance)
    {
        NativeArray<Entity> entities = __instance.__query_663986227_0.ToEntityArray(Allocator.Temp);
        try
        {
            foreach (Entity entity in entities)
            {
                if (Plugin.LevelingSystem.Value && entity.Has<EntityOwner>() && entity.Read<EntityOwner>().Owner.Has<PlayerCharacter>())
                {
                    EntityOwner entityOwner = entity.Read<EntityOwner>();
                    GearOverride.SetLevel(entityOwner.Owner);
                }
            }
        }
        catch (Exception e)
        {
            Core.Log.LogError($"Exited LevelPrefix early: {e}");
        }
        finally
        {
            entities.Dispose();
        }
    }

    [HarmonyPatch(typeof(ArmorLevelSystem_Destroy), nameof(ArmorLevelSystem_Destroy.OnUpdate))]
    [HarmonyPostfix]
    static void ArmorLevelDestroyPostfix(ArmorLevelSystem_Destroy __instance)
    {
        NativeArray<Entity> entities = __instance.__query_663986292_0.ToEntityArray(Allocator.Temp);
        try
        {
            foreach (Entity entity in entities)
            {
                if (Plugin.LevelingSystem.Value && entity.Has<EntityOwner>() && entity.Read<EntityOwner>().Owner.Has<PlayerCharacter>())
                {
                    EntityOwner entityOwner = entity.Read<EntityOwner>();
                    GearOverride.SetLevel(entityOwner.Owner);
                }
            }
        }
        catch (Exception e)
        {
            Core.Log.LogError($"Exited LevelPrefix early: {e}");
        }
        finally
        {
            entities.Dispose();
        }
    }
    

    [HarmonyPatch(typeof(ModifyUnitStatBuffSystem_Spawn), nameof(ModifyUnitStatBuffSystem_Spawn.OnUpdate))]
    [HarmonyPrefix]
    static void OnUpdatePrefix(ModifyUnitStatBuffSystem_Spawn __instance)
    {
        NativeArray<Entity> entities = __instance.__query_1735840491_0.ToEntityArray(Allocator.TempJob);
        try
        {
            foreach (var entity in entities)
            {
                
                if (Plugin.ExpertiseSystem.Value && entity.Has<WeaponLevel>() && entity.Has<EntityOwner>() && entity.Read<EntityOwner>().Owner.Has<PlayerCharacter>())
                {
                    Entity character = entity.Read<EntityOwner>().Owner;
                    ulong steamId = character.Read<PlayerCharacter>().UserEntity.Read<User>().PlatformId;
                    ExpertiseSystem.WeaponType weaponType = ExpertiseSystem.GetWeaponTypeFromPrefab(entity.Read<PrefabGUID>());
                    if (weaponType.Equals(ExpertiseSystem.WeaponType.Unarmed) || weaponType.Equals(ExpertiseSystem.WeaponType.FishingPole)) continue;

                    ModifyUnitStatBuffUtils.ApplyWeaponBonuses(character, weaponType, entity);
                }
            }
        }
        catch (Exception ex)
        {
            Core.Log.LogError(ex);
        }
        finally
        {
            entities.Dispose();
        }
    }
    
    [HarmonyPatch(typeof(ModifyUnitStatBuffSystem_Spawn), nameof(ModifyUnitStatBuffSystem_Spawn.OnUpdate))]
    [HarmonyPostfix]
    static void OnUpdatePostix(ModifyUnitStatBuffSystem_Spawn __instance)
    {
        NativeArray<Entity> entities = __instance.__query_1735840491_0.ToEntityArray(Allocator.TempJob);
        try
        {
            foreach (var entity in entities)
            {
                if (Plugin.LevelingSystem.Value && entity.Has<EntityOwner>() && entity.Read<EntityOwner>().Owner.Has<PlayerCharacter>())
                {
                    EntityOwner entityOwner = entity.Read<EntityOwner>();
                    GearOverride.SetLevel(entityOwner.Owner);
                }
            }
        }
        catch (Exception ex)
        {
            Core.Log.LogError(ex);
        }
        finally
        {
            entities.Dispose();
        }
    }

    [HarmonyPatch(typeof(DealDamageSystem), nameof(DealDamageSystem.OnUpdate))]
    [HarmonyPrefix]
    static void OnUpdatePrefix(DealDamageSystem __instance)
    {
        NativeArray<Entity> entities = __instance._Query.ToEntityArray(Allocator.TempJob);
        try
        {
            foreach (var entity in entities)
            {
                if (Plugin.ExpertiseSystem.Value)
                {
                    DealDamageEvent dealDamageEvent = entity.Read<DealDamageEvent>();
                    Entity entityOwner = dealDamageEvent.SpellSource.Read<EntityOwner>().Owner;
                    EntityCategory entityCategory = dealDamageEvent.Target.Read<EntityCategory>();
                    if (!entityCategory.MainCategory.Equals(MainEntityCategory.Resource) || !entityOwner.Has<Equipment>()) continue;
                    Entity weapon = entityOwner.Read<Equipment>().WeaponSlot.SlotEntity._Entity;
                    if (weapon.Equals(Entity.Null) || !weapon.Has<WeaponLevelSource>()) continue;
                    //if (weapon.Read<WeaponLevelSource>().Level >= entityCategory.ResourceLevel._Value) continue;
                    float originalSource = Core.PrefabCollectionSystem._PrefabGuidToEntityMap[weapon.Read<PrefabGUID>()].Read<WeaponLevelSource>().Level;
                    int originalResource = Core.PrefabCollectionSystem._PrefabGuidToEntityMap[dealDamageEvent.Target.Read<PrefabGUID>()].Read<EntityCategory>().ResourceLevel._Value;
                    if (originalSource >= originalResource)
                    {
                        entityCategory.ResourceLevel._Value = 0;
                        dealDamageEvent.Target.Write(entityCategory);
                    }
                    else if (originalSource < originalResource)
                    {
                        entityCategory.ResourceLevel._Value = originalResource;
                        dealDamageEvent.Target.Write(entityCategory);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Core.Log.LogError(ex);
        }
        finally
        {
            entities.Dispose();
        }
    }

    [HarmonyPatch(typeof(ReactToInventoryChangedSystem), nameof(ReactToInventoryChangedSystem.OnUpdate))]
    [HarmonyPrefix]
    static void OnUpdatePrefix(ReactToInventoryChangedSystem __instance)
    {
        NativeArray<Entity> entities = __instance.__query_2096870024_0.ToEntityArray(Allocator.TempJob);
        try
        {
            foreach (var entity in entities)
            {
                InventoryChangedEvent inventoryChangedEvent = entity.Read<InventoryChangedEvent>();
                Entity inventory = inventoryChangedEvent.InventoryEntity;
                if (inventoryChangedEvent.ChangeType.Equals(InventoryChangedEventType.Obtained) && inventory.Has<InventoryConnection>() && inventory.Read<InventoryConnection>().InventoryOwner.Has<PlayerCharacter>())
                {
                    if (Plugin.ExpertiseSystem.Value && inventoryChangedEvent.ItemEntity.Has<WeaponLevelSource>()) // set weapon visual level to expertise of player in that weapon
                    {
                        ulong steamId = inventory.Read<InventoryConnection>().InventoryOwner.Read<PlayerCharacter>().UserEntity.Read<User>().PlatformId;
                        ExpertiseSystem.WeaponType weaponType = ExpertiseSystem.GetWeaponTypeFromPrefab(inventoryChangedEvent.ItemEntity.Read<PrefabGUID>());
                        if (weaponType.Equals(ExpertiseSystem.WeaponType.Unarmed) || weaponType.Equals(ExpertiseSystem.WeaponType.FishingPole)) continue;

                        IExpertiseHandler handler = ExpertiseHandlerFactory.GetExpertiseHandler(weaponType);
                        if (handler != null)
                        {
                            WeaponLevelSource weaponLevelSource = new()
                            {
                                Level = handler.GetExpertiseData(steamId).Key
                            };
                            inventoryChangedEvent.ItemEntity.Write(weaponLevelSource);
                        }
                    }
                }
                else if (inventoryChangedEvent.ChangeType.Equals(InventoryChangedEventType.Removed) && inventory.Has<InventoryConnection>() && inventory.Read<InventoryConnection>().InventoryOwner.Has<PlayerCharacter>())
                {
                    if (Plugin.ExpertiseSystem.Value && inventoryChangedEvent.ItemEntity.Has<WeaponLevelSource>() ) // restore weapon level source for servants
                    {
                        inventoryChangedEvent.ItemEntity.Write(new WeaponLevelSource { Level = Core.PrefabCollectionSystem._PrefabGuidToEntityMap[inventoryChangedEvent.Item].Read<WeaponLevelSource>().Level });
                    }
                }
                else if (Plugin.ProfessionSystem.Value && inventoryChangedEvent.ChangeType.Equals(InventoryChangedEventType.Obtained) && inventory.Has<InventoryConnection>() && inventory.Read<InventoryConnection>().InventoryOwner.Has<CastleWorkstation>())
                {
                    Entity userEntity = inventory.Read<InventoryConnection>().InventoryOwner.Read<UserOwner>().Owner._Entity;
                    ulong steamId = userEntity.Read<User>().PlatformId;
                    IProfessionHandler handler = ProfessionHandlerFactory.GetProfessionHandler(inventoryChangedEvent.Item, "");
                    if (Core.DataStructures.PlayerCraftingJobs.TryGetValue(steamId, out var playerJobs))
                    {
                        bool jobExists = false;
                        for (int i = 0; i < playerJobs.Count; i++)
                        {
                            if (playerJobs[i].Item1 == inventoryChangedEvent.Item && playerJobs[i].Item2 > 0)
                            {
                                playerJobs[i] = (playerJobs[i].Item1, playerJobs[i].Item2 - 1);
                                if (playerJobs[i].Item2 == 0) playerJobs.RemoveAt(i);
                                jobExists = true;
                                break;
                            }
                        }
                        if (!jobExists) continue;
                        float ProfessionValue = 50f;
                        ProfessionValue *= ProfessionUtilities.GetTierMultiplier(inventoryChangedEvent.Item);
                        if (handler != null)
                        {
                            ProfessionSystem.SetProfession(userEntity.Read<User>(), steamId, ProfessionValue, handler);
                            Entity itemEntity = inventoryChangedEvent.ItemEntity;
                            switch (handler)
                            {
                                case BlacksmithingHandler:
                                    if (itemEntity.Has<Durability>())
                                    {
                                        Durability durability = itemEntity.Read<Durability>();
                                        int level = handler.GetExperienceData(steamId).Key;
                                        durability.MaxDurability *= (1 + level / Plugin.MaxProfessionLevel.Value);
                                        durability.Value = durability.MaxDurability;
                                        itemEntity.Write(durability);
                                    }
                                    break;
                                case AlchemyHandler:
                                    break;
                                case EnchantingHandler:
                                    if (itemEntity.Has<Durability>())
                                    {
                                        Durability durability = itemEntity.Read<Durability>();
                                        int level = handler.GetExperienceData(steamId).Key;
                                        durability.MaxDurability *= (1 + level / Plugin.MaxProfessionLevel.Value);
                                        durability.Value = durability.MaxDurability;
                                        itemEntity.Write(durability);
                                    }
                                    break;
                                case TailoringHandler:
                                    if (itemEntity.Has<Durability>())
                                    {
                                        Durability durability = itemEntity.Read<Durability>();
                                        int level = handler.GetExperienceData(steamId).Key;
                                        durability.MaxDurability *= (1 + level / Plugin.MaxProfessionLevel.Value);
                                        durability.Value = durability.MaxDurability;
                                        itemEntity.Write(durability);
                                    }
                                    break;
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Core.Log.LogError(ex);
        }
        finally
        {
            entities.Dispose();
        }
    }
}

public static class ModifyUnitStatBuffUtils // need to move this out of equipment patches
{
    public static void ApplyWeaponBonuses(Entity character, ExpertiseSystem.WeaponType weaponType, Entity weaponEntity)
    {
        ulong steamId = character.Read<PlayerCharacter>().UserEntity.Read<User>().PlatformId;
        IExpertiseHandler handler = ExpertiseHandlerFactory.GetExpertiseHandler(weaponType);

        //if (!weaponType.Equals(ExpertiseSystem.WeaponType.Unarmed)) GearOverride.SetWeaponItemLevel(equipment, handler.GetExpertiseData(steamId).Key, Core.EntityManager);

        if (Core.DataStructures.PlayerWeaponStats.TryGetValue(steamId, out var weaponStats) && weaponStats.TryGetValue(weaponType, out var bonuses))
        {
            if (!weaponEntity.Has<ModifyUnitStatBuff_DOTS>())
            {
                Core.EntityManager.AddBuffer<ModifyUnitStatBuff_DOTS>(weaponEntity);
            }
            var buffer = weaponEntity.ReadBuffer<ModifyUnitStatBuff_DOTS>();
            foreach (var weaponStatType in bonuses)
            {
                float scaledBonus = CalculateScaledWeaponBonus(handler, steamId, weaponStatType);
                bool found = false;
                for (int i = 0; i < buffer.Length; i++)
                {
                    ModifyUnitStatBuff_DOTS statBuff = buffer[i];
                    if (statBuff.StatType.Equals(WeaponStatMap[weaponStatType])) // Assuming WeaponStatType can be cast to UnitStatType
                    {
                        statBuff.Value += scaledBonus; // Modify the value accordingly
                        buffer[i] = statBuff; // Assign the modified struct back to the buffer
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    // If not found, create a new stat modifier
                    UnitStatType statType = WeaponStatMap[weaponStatType];
                    ModifyUnitStatBuff_DOTS newStatBuff = new()
                    {
                        StatType = statType,
                        ModificationType = ModificationType.AddToBase,
                        Value = scaledBonus,
                        Modifier = 1,
                        IncreaseByStacks = false,
                        ValueByStacks = 0,
                        Priority = 0,
                        Id = ModificationId.Empty
                    };
                    buffer.Add(newStatBuff);
                }
            }
            
        }
    }
    public static void ApplyBloodBonuses(Entity character, BloodSystem.BloodType bloodType, Entity bloodBuff)
    {
        ulong steamId = character.Read<PlayerCharacter>().UserEntity.Read<User>().PlatformId;
        IBloodHandler handler = BloodHandlerFactory.GetBloodHandler(bloodType);

        if (Core.DataStructures.PlayerBloodStats.TryGetValue(steamId, out var bloodStats) && bloodStats.TryGetValue(bloodType, out var bonuses))
        {
            if (!bloodBuff.Has<ModifyUnitStatBuff_DOTS>())
            {
                Core.EntityManager.AddBuffer<ModifyUnitStatBuff_DOTS>(bloodBuff);
            }
            var buffer = bloodBuff.ReadBuffer<ModifyUnitStatBuff_DOTS>();
            foreach (var bloodStatType in bonuses)
            {
                float scaledBonus = CalculateScaledBloodBonus(handler, steamId, bloodStatType);
                bool found = false;
                for (int i = 0; i < buffer.Length; i++)
                {
                    ModifyUnitStatBuff_DOTS statBuff = buffer[i];
                    if (statBuff.StatType.Equals(BloodStatMap[bloodStatType])) // Assuming WeaponStatType can be cast to UnitStatType
                    {
                        statBuff.Value += scaledBonus; // Modify the value accordingly
                        buffer[i] = statBuff; // Assign the modified struct back to the buffer
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    // If not found, create a new stat modifier
                    UnitStatType statType = BloodStatMap[bloodStatType];
                    ModifyUnitStatBuff_DOTS newStatBuff = new()
                    {
                        StatType = statType,
                        ModificationType = ModificationType.AddToBase,
                        Value = scaledBonus,
                        Modifier = 1,
                        IncreaseByStacks = false,
                        ValueByStacks = 0,
                        Priority = 0,
                        Id = ModificationId.Empty
                    };
                    buffer.Add(newStatBuff);
                }
            }

        }
    }
    public static float CalculateScaledWeaponBonus(IExpertiseHandler handler, ulong steamId, WeaponStats.WeaponStatManager.WeaponStatType statType)
    {
        if (handler != null)
        {
            var xpData = handler.GetExpertiseData(steamId);
            int currentLevel = ExpertiseSystem.ConvertXpToLevel(xpData.Value);
            float maxBonus = WeaponStats.WeaponStatManager.BaseCaps[statType];
            float scaledBonus = maxBonus * (currentLevel / Plugin.MaxExpertiseLevel.Value); // Scale bonus up to 99%
            return scaledBonus;
        }
        return 0; // Return 0 if no handler is found or other error
    }
    public static float CalculateScaledBloodBonus(IBloodHandler handler, ulong steamId, BloodStats.BloodStatManager.BloodStatType statType)
    {
        if (handler != null)
        {
            var xpData = handler.GetLegacyData(steamId);
            int currentLevel = BloodSystem.ConvertXpToLevel(xpData.Value);
            float maxBonus = BloodStats.BloodStatManager.BaseCaps[statType];
            float scaledBonus = maxBonus * (currentLevel / Plugin.MaxBloodLevel.Value); // Scale bonus up to 99%
            return scaledBonus;
        }
        return 0; // Return 0 if no handler is found or other error
    }
    public static ExpertiseSystem.WeaponType GetCurrentWeaponType(Entity character)
    {
        Entity weapon = character.Read<Equipment>().WeaponSlot.SlotEntity._Entity;
        if (weapon.Equals(Entity.Null)) return ExpertiseSystem.WeaponType.Unarmed;
        return ExpertiseSystem.GetWeaponTypeFromPrefab(weapon.Read<PrefabGUID>());
    }
    public static BloodSystem.BloodType GetCurrentBloodType(Entity character)
    {
        Blood blood = character.Read<Blood>();

        return BloodSystem.GetBloodTypeFromPrefab(blood.BloodType);
    }
}

public static class GearOverride // also this
{
    public static void SetLevel(Entity player)
    {
        ulong steamId = player.Read<PlayerCharacter>().UserEntity.Read<User>().PlatformId;
        if (Core.DataStructures.PlayerExperience.TryGetValue(steamId, out var xpData))
        {
            int playerLevel = xpData.Key;
            Equipment equipment = player.Read<Equipment>();

            if (Plugin.LevelingSystem.Value)
            {
                equipment.ArmorLevel._Value = 0f;
                equipment.SpellLevel._Value = 0f;
                equipment.WeaponLevel._Value = playerLevel;
                player.Write(equipment);
            }
        }
    }
    public static void SetWeaponItemLevel(Equipment equipment, int level, EntityManager entityManager)
    {
        Entity weaponEntity = equipment.WeaponSlot.SlotEntity._Entity;
        if (!weaponEntity.Equals(Entity.Null) && entityManager.HasComponent<WeaponLevelSource>(weaponEntity))
        {
            weaponEntity.Write(new WeaponLevelSource { Level = level });
        }
    }
}
