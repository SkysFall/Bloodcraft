using Cobalt.Core;
using Cobalt.Systems.Bloodline;
using Cobalt.Systems.Experience;
using Cobalt.Systems.Expertise;
using HarmonyLib;
using ProjectM;
using Unity.Collections;
using ProfessionSystem = Cobalt.Systems.ProfessionSystem;

namespace Cobalt.Hooks;

[HarmonyPatch]
public class DeathEventListenerSystem_Patch
{
    [HarmonyPatch(typeof(DeathEventListenerSystem), "OnUpdate")]
    [HarmonyPostfix]
    public static void Postfix(DeathEventListenerSystem __instance)
    {
        NativeArray<DeathEvent> deathEvents = __instance._DeathEventQuery.ToComponentDataArray<DeathEvent>(Allocator.Temp);
        try
        {
            foreach (DeathEvent ev in deathEvents)
            {
                if (__instance.EntityManager.HasComponent<PlayerCharacter>(ev.Killer) && __instance.EntityManager.HasComponent<Movement>(ev.Died))
                {
                    ExperienceSystem.EXPMonitor(ev.Killer, ev.Died);
                    WeaponMasterySystem.UpdateCombatMastery(__instance.EntityManager, ev.Killer, ev.Died);
                    BloodMasterySystem.UpdateBloodMastery(ev.Killer, ev.Died);
                }
                else if (__instance.EntityManager.HasComponent<PlayerCharacter>(ev.Killer))
                {
                    //ev.Died.LogComponentTypes();
                    ProfessionSystem.UpdateProfessions(ev.Killer, ev.Died);
                }
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"Exited DeathEventListenerSystem hook early: {e}");
        }
        finally
        {
            deathEvents.Dispose();
        }
    }
}