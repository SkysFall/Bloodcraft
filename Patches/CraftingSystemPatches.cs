using Bloodcraft.Services;
using Bloodcraft.Systems.Professions;
using Bloodcraft.Systems.Quests;
using Engine.Console.GameEngineImplementation;
using HarmonyLib;
using ProjectM;
using ProjectM.Network;
using ProjectM.Scripting;
using ProjectM.Shared;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using User = ProjectM.Network.User;

namespace Bloodcraft.Patches;

[HarmonyPatch]
internal static class CraftingSystemPatches // ForgeSystem_Update, UpdateCraftingSystem
{
    static ServerGameManager ServerGameManager => Core.ServerGameManager;
    static SystemService SystemService => Core.SystemService;
    static PrefabCollectionSystem PrefabCollectionSystem => SystemService.PrefabCollectionSystem;
    static NetworkIdSystem.Singleton NetworkIdSystem => SystemService.NetworkIdSystem;

    const float CRAFT_THRESHOLD = 0.985f;
    static readonly float CraftRateModifier = SystemService.ServerGameSettingsSystem.Settings.CraftRateModifier;

    static readonly Dictionary<ulong, Dictionary<Entity, Dictionary<PrefabGUID, int>>> playerCraftingJobs = [];
    public static readonly Dictionary<ulong, Dictionary<Entity, Dictionary<PrefabGUID, int>>> ValidatedCraftingJobs = [];

    [HarmonyPatch(typeof(ForgeSystem_Update), nameof(ForgeSystem_Update.OnUpdate))]
    [HarmonyPrefix]
    static void Prefix(ForgeSystem_Update __instance)
    {
        if (!Core.hasInitialized) return;
        else if (!ConfigService.ProfessionSystem && !ConfigService.QuestSystem) return;

        NativeArray<Entity> repairEntities = __instance.__query_1536473549_0.ToEntityArray(Allocator.Temp);
        try
        {
            foreach (Entity entity in repairEntities)
            {
                Forge_Shared forge_Shared = entity.Read<Forge_Shared>();
                if (forge_Shared.State == ForgeState.Empty) continue;

                UserOwner userOwner = entity.Read<UserOwner>();
                Entity userEntity = userOwner.Owner._Entity;
                User user = userEntity.Read<User>();
                ulong steamId = user.PlatformId;

                Entity itemEntity = forge_Shared.ItemEntity._Entity;
                PrefabGUID itemPrefab = itemEntity.Read<PrefabGUID>();

                if (itemEntity.Has<ShatteredItem>())
                {
                    itemPrefab = itemEntity.Read<ShatteredItem>().OutputItem;
                }
                else if (itemEntity.Has<UpgradeableLegendaryItem>())
                {
                    int tier = itemEntity.Read<UpgradeableLegendaryItem>().CurrentTier;
                    var buffer = itemEntity.ReadBuffer<UpgradeableLegendaryItemTiers>();
                    itemPrefab = buffer[tier].TierPrefab;
                }

                if (forge_Shared.State == ForgeState.Finished)
                {
                    if (steamId.TryGetPlayerQuests(out var quests)) QuestSystem.ProcessQuestProgress(quests, itemPrefab, 1, user);
                    else if (!ConfigService.ProfessionSystem) continue;

                    float ProfessionValue = 50f;
                    ProfessionValue *= ProfessionMappings.GetTierMultiplier(itemPrefab);
                    IProfessionHandler handler = ProfessionHandlerFactory.GetProfessionHandler(itemPrefab, "");

                    if (handler != null)
                    {
                        if (itemEntity.Has<Durability>())
                        {
                            Entity originalItem = PrefabCollectionSystem._PrefabGuidToEntityMap[itemPrefab];

                            Durability durability = itemEntity.Read<Durability>();
                            Durability originalDurability = originalItem.Read<Durability>();

                            if (durability.MaxDurability > originalDurability.MaxDurability) continue; // already handled

                            int level = handler.GetProfessionData(steamId).Key;

                            durability.MaxDurability *= (1 + (float)level / (float)ConfigService.MaxProfessionLevel);
                            durability.Value = durability.MaxDurability;
                            itemEntity.Write(durability);

                            ProfessionSystem.SetProfession(entity, user.LocalCharacter.GetEntityOnServer(), steamId, ProfessionValue, handler);
                        }
                    }
                }
            }
        }
        finally
        {
            repairEntities.Dispose();
        }
    }

    static readonly Dictionary<Entity, bool> CraftFinished = [];

    [HarmonyPatch(typeof(UpdateCraftingSystem), nameof(UpdateCraftingSystem.OnUpdate))]
    [HarmonyPrefix]
    static void OnUpdatePrefix(UpdateCraftingSystem __instance)
    {
        if (!Core.hasInitialized) return;
        else if (!ConfigService.ProfessionSystem && !ConfigService.QuestSystem) return;

        NativeArray<Entity> entities = __instance.__query_1831452865_0.ToEntityArray(Allocator.Temp);
        try
        {
            foreach (Entity entity in entities)
            {
                if (entity.Has<CastleWorkstation>() && ServerGameManager.TryGetBuffer<QueuedWorkstationCraftAction>(entity, out var buffer))
                {
                    if (!buffer.IsEmpty)
                    {
                        if (!CraftFinished.ContainsKey(entity))
                        {
                            CraftFinished[entity] = false;
                        }

                        QueuedWorkstationCraftAction queuedWorkstationCraftAction = buffer[0];
                        double recipeReduction = entity.Read<CastleWorkstation>().WorkstationLevel.HasFlag(WorkstationLevel.MatchingFloor) ? 0.75 : 1;

                        ProcessQueuedCraftAction(entity, queuedWorkstationCraftAction, recipeReduction);
                    }
                    else if (CraftFinished.ContainsKey(entity))
                    {
                        CraftFinished[entity] = false;
                    }

                    /*
                    for (int i = 0; i < buffer.Length; i++) // need to log this out while a station is crafting and see what it looks like
                    {
                        QueuedWorkstationCraftAction craftAction = buffer[i];

                        Entity userEntity = craftAction.InitiateUser;
                        ulong steamId = userEntity.GetSteamId();

                        PrefabGUID recipeGUID = craftAction.RecipeGuid;
                        Entity recipePrefab = PrefabCollectionSystem._PrefabGuidToEntityMap.ContainsKey(recipeGUID) ? PrefabCollectionSystem._PrefabGuidToEntityMap[recipeGUID] : Entity.Null;
                        PrefabGUID itemPrefabGUID = GetItemFromRecipePrefab(recipePrefab);

                        if (recipePrefab.TryGetComponent(out RecipeData recipeData))
                        {
                            float craftDuration = recipeData.CraftDuration;
                            double totalTime = CraftRateModifier.Equals(1f) ? craftDuration * recipeReduction : craftDuration * recipeReduction / CraftRateModifier;

                            if (craftAction.ProgressTime / totalTime >= CRAFT_THRESHOLD && playerCraftingJobs.TryGetValue(steamId, out var stationJobs) && stationJobs.TryGetValue(entity, out var craftingJobs) && craftingJobs.ContainsKey(itemPrefabGUID))
                            {
                                ValidateCraftingJob(entity, itemPrefabGUID, steamId);
                            }
                        }
                    }
                    */
                }
            }
        }
        finally
        {
            entities.Dispose();
        }
    }

    [HarmonyPatch(typeof(StartCraftingSystem), nameof(StartCraftingSystem.OnUpdate))]
    [HarmonyPrefix]
    public static void Prefix(StartCraftingSystem __instance)
    {
        if (!Core.hasInitialized) return;
        else if (!ConfigService.ProfessionSystem && !ConfigService.QuestSystem) return;

        NativeArray<Entity> entities = __instance._StartCraftItemEventQuery.ToEntityArray(Allocator.Temp);
        try
        {
            foreach (Entity entity in entities)
            {
                if (entity.TryGetComponent(out StartCraftItemEvent startCraftEvent) && entity.TryGetComponent(out FromCharacter fromCharacter))
                {
                    Entity craftingStation = NetworkIdSystem._NetworkIdLookupMap.TryGetValue(startCraftEvent.Workstation, out Entity station) ? station : Entity.Null;

                    PrefabGUID recipeGUID = startCraftEvent.RecipeId;
                    Entity recipePrefab = PrefabCollectionSystem._PrefabGuidToEntityMap.ContainsKey(recipeGUID) ? PrefabCollectionSystem._PrefabGuidToEntityMap[recipeGUID] : Entity.Null;
                    PrefabGUID itemPrefabGUID = GetItemFromRecipePrefab(recipePrefab);

                    ulong steamId = fromCharacter.User.GetSteamId();

                    if (!playerCraftingJobs.ContainsKey(steamId))
                    {
                        playerCraftingJobs.Add(steamId, []);
                    }

                    if (!playerCraftingJobs[steamId].ContainsKey(craftingStation))
                    {
                        playerCraftingJobs[steamId].Add(craftingStation, []);
                    }

                    Dictionary<PrefabGUID, int> RecipesCrafting = playerCraftingJobs[steamId][craftingStation];

                    if (!RecipesCrafting.ContainsKey(itemPrefabGUID))
                    {
                        //Core.Log.LogInfo($"Crafting job added for {itemPrefabGUID.LookupName()}| 1");
                        RecipesCrafting[itemPrefabGUID] = 1;
                    }
                    else
                    {
                        //Core.Log.LogInfo($"Crafting job added for {itemPrefabGUID.LookupName()}| {RecipesCrafting[itemPrefabGUID] + 1}");
                        RecipesCrafting[itemPrefabGUID] = ++RecipesCrafting[itemPrefabGUID];
                    }

                    playerCraftingJobs[steamId][craftingStation] = RecipesCrafting;
                }
            }
        }
        finally
        {
            entities.Dispose();
        }
    }

    [HarmonyPatch(typeof(StopCraftingSystem), nameof(StopCraftingSystem.OnUpdate))]
    [HarmonyPrefix]
    public static void Prefix(StopCraftingSystem __instance)
    {
        if (!Core.hasInitialized) return;
        else if (!ConfigService.ProfessionSystem && !ConfigService.QuestSystem) return;

        NativeArray<Entity> entities = __instance._EventQuery.ToEntityArray(Allocator.Temp);
        try
        {
            foreach (Entity entity in entities)
            {
                if (entity.TryGetComponent(out StopCraftItemEvent stopCraftEvent) && entity.TryGetComponent(out FromCharacter fromCharacter))
                {
                    Entity craftingStation = NetworkIdSystem._NetworkIdLookupMap.TryGetValue(stopCraftEvent.Workstation, out Entity station) ? station : Entity.Null;

                    PrefabGUID recipeGUID = stopCraftEvent.RecipeGuid;
                    Entity recipePrefab = PrefabCollectionSystem._PrefabGuidToEntityMap.ContainsKey(recipeGUID) ? PrefabCollectionSystem._PrefabGuidToEntityMap[recipeGUID] : Entity.Null;
                    PrefabGUID itemPrefabGUID = GetItemFromRecipePrefab(recipePrefab);

                    ulong steamId = fromCharacter.User.GetSteamId();

                    if (playerCraftingJobs.TryGetValue(steamId, out var stationJobs) && stationJobs.TryGetValue(craftingStation, out var craftingJobs))
                    {
                        if (craftingJobs.ContainsKey(itemPrefabGUID))
                        {
                            int jobs = craftingJobs[itemPrefabGUID];
                            craftingJobs[itemPrefabGUID] = --jobs;

                            //Core.Log.LogInfo($"Crafting job removed via StopCraftEvent for {itemPrefabGUID.LookupName()}| {craftingJobs[itemPrefabGUID]}");
                            if (craftingJobs[itemPrefabGUID] <= 0) craftingJobs.Remove(itemPrefabGUID);
                        }
                    }
                }
            }
        }
        finally
        {
            entities.Dispose();
        }
    }

    static void ProcessQueuedCraftAction(Entity entity, QueuedWorkstationCraftAction craftAction, double recipeReduction)
    {
        Entity userEntity = craftAction.InitiateUser;
        ulong steamId = userEntity.GetSteamId();
        bool craftFinished = CraftFinished[entity];

        PrefabGUID recipeGUID = craftAction.RecipeGuid;
        Entity recipePrefab = PrefabCollectionSystem._PrefabGuidToEntityMap.ContainsKey(recipeGUID) ? PrefabCollectionSystem._PrefabGuidToEntityMap[recipeGUID] : Entity.Null;
        PrefabGUID itemPrefabGUID = GetItemFromRecipePrefab(recipePrefab);

        if (recipePrefab.TryGetComponent(out RecipeData recipeData))
        {
            float craftDuration = recipeData.CraftDuration;
            double totalTime = CraftRateModifier.Equals(1f) ? craftDuration * recipeReduction : craftDuration * recipeReduction / CraftRateModifier;

            if (!craftFinished && craftAction.ProgressTime / totalTime >= CRAFT_THRESHOLD)
            {
                if (playerCraftingJobs.TryGetValue(steamId, out var stationJobs) 
                    && stationJobs.TryGetValue(entity, out var craftingJobs) 
                    && craftingJobs.ContainsKey(itemPrefabGUID))
                {
                    ValidateCraftingJob(entity, itemPrefabGUID, steamId);
                    CraftFinished[entity] = true;
                }
            }
            else if (craftFinished && craftAction.ProgressTime / totalTime < CRAFT_THRESHOLD)
            {
                CraftFinished[entity] = false;
            }
        }
    }
    static PrefabGUID GetItemFromRecipePrefab(Entity recipePrefab)
    {
        if (recipePrefab.Exists() && recipePrefab.Has<RecipeData>())
        {
            var outputBuffer = recipePrefab.ReadBuffer<RecipeOutputBuffer>();
            return outputBuffer[0].Guid;
        }

        return PrefabGUID.Empty;
    }
    static void ValidateCraftingJob(Entity craftingStation, PrefabGUID itemPrefabGUID, ulong steamId)
    {
        if (playerCraftingJobs.TryGetValue(steamId, out var stationJobs) && stationJobs.TryGetValue(craftingStation, out var craftingJobs) && craftingJobs.ContainsKey(itemPrefabGUID))
        {
            if (craftingJobs[itemPrefabGUID] > 0) 
            {
                if (!ValidatedCraftingJobs.ContainsKey(steamId))
                {
                    ValidatedCraftingJobs[steamId] = [];
                }

                if (!ValidatedCraftingJobs[steamId].ContainsKey(craftingStation))
                {
                    ValidatedCraftingJobs[steamId].Add(craftingStation, []);
                }

                Dictionary<PrefabGUID, int> validatedCraftingJobs = ValidatedCraftingJobs[steamId][craftingStation];

                if (!validatedCraftingJobs.ContainsKey(itemPrefabGUID))
                {
                    validatedCraftingJobs[itemPrefabGUID] = 1;
                }
                else
                {
                    validatedCraftingJobs[itemPrefabGUID]++;
                }

                ValidatedCraftingJobs[steamId][craftingStation] = validatedCraftingJobs;

                int jobs = craftingJobs[itemPrefabGUID];
                craftingJobs[itemPrefabGUID] = --jobs;

                //Core.Log.LogInfo($"Crafting job removed via CraftValidation for {itemPrefabGUID.LookupName()}| {craftingJobs[itemPrefabGUID]}");

                if (craftingJobs[itemPrefabGUID] <= 0) craftingJobs.Remove(itemPrefabGUID);
            }
            else if (craftingJobs[itemPrefabGUID] > 0) // if no change then just a click event with no materials used
            {
                int jobs = craftingJobs[itemPrefabGUID];
                craftingJobs[itemPrefabGUID] = --jobs;

                if (craftingJobs[itemPrefabGUID] <= 0) craftingJobs.Remove(itemPrefabGUID);
            }
        }
    }
}