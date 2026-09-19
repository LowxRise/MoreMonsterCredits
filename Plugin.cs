using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using LocalTweaks;
using RiskOfOptions;
using RoR2;
using UnityEngine.Networking;

namespace MoreMonsterCredits
{
    [BepInPlugin(Guid, Name, "1.1.0")]
    [BepInDependency("com.rune580.riskofoptions")]
    [BepInDependency("Wolfo.LittleGameplayTweaks", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.youssef.MoreMonsterCredits";
        public const string Name = "More Monster Credits";
        private static readonly ConfigEntry<int>[] bonuses = new ConfigEntry<int>[8];
        private static readonly ConfigEntry<float>[] monsterCounts = new ConfigEntry<float>[8];
        private static FieldInfo singleBossSpawn;
        private readonly Harmony harmony = new Harmony(Guid);

        private void Awake()
        {
            ModSettingsManager.SetModDescription("Set separate monster budgets and monster-count multipliers for stages 3 through 10.", Guid, Name);
            var icon = SettingsIcon.Load("MoreMonsterCredits.SettingsIcon.png");
            if (icon)
                ModSettingsManager.SetModIcon(icon, Guid, Name);
            for (int stage = 3; stage <= 10; stage++)
            {
                bonuses[stage - 3] = TweakSettings.Slider(Config, "Monster credits", $"Stage {stage} bonus percent", 0, 0, 200,
                    "Extra monster budget: 0 leaves it alone, 100 doubles it, 200 triples it. Ongoing income changes immediately; starting spawns change on the next stage entry. Hidden realms do not count.", Guid, Name);
                monsterCounts[stage - 3] = TweakSettings.StepSlider(Config, "Monster count", $"Stage {stage} multiplier", 1f, 1f, 3f, 0.5f,
                    "Makes the director spend less credit per successful monster spawn, producing roughly this many monsters from the same budget. Does not affect chests or other interactables.", Guid, Name);
            }
            try
            {
                harmony.Patch(AccessTools.Method(typeof(CombatDirector), "FixedUpdate"),
                    transpiler: new HarmonyMethod(typeof(Plugin), nameof(ChangeIncome)));
                harmony.Patch(AccessTools.Method(typeof(SceneDirector), "PopulateScene"),
                    prefix: new HarmonyMethod(typeof(Plugin), nameof(ChangeStartingCredits)));
                var attemptSpawn = AccessTools.Method(typeof(CombatDirector), "AttemptSpawnOnTarget");
                singleBossSpawn = AccessTools.Field(typeof(CombatDirector), "_bossOverrideSpawnSingleBoss");
                if (attemptSpawn == null || singleBossSpawn == null)
                    throw new MissingMemberException("CombatDirector's spawn accounting no longer matches.");
                harmony.Patch(attemptSpawn,
                    prefix: new HarmonyMethod(typeof(Plugin), nameof(RememberCredits)),
                    postfix: new HarmonyMethod(typeof(Plugin), nameof(RefundSpawnCost)));
                Logger.LogInfo("Monster credit and count tweaks loaded.");
            }
            catch (Exception exception)
            {
                harmony.UnpatchSelf();
                Logger.LogError($"Monster credit tweaks were not applied.\n{exception}");
            }
        }

        private void OnDestroy() => harmony.UnpatchSelf();

        private static int StageIndex()
        {
            var run = Run.instance;
            var scene = SceneCatalog.mostRecentSceneDef;
            if (!NetworkServer.active || !run || !scene || scene.sceneType != SceneType.Stage ||
                scene.isFinalStage || !scene.validForRandomSelection)
                return -1;
            int index = run.stageClearCount - 2;
            return index >= 0 && index < bonuses.Length ? index : -1;
        }

        private static float CreditMultiplier()
        {
            int index = StageIndex();
            return index >= 0 ? 1f + bonuses[index].Value / 100f : 1f;
        }

        private static float MonsterMultiplier()
        {
            int index = StageIndex();
            return index >= 0 ? monsterCounts[index].Value : 1f;
        }

        private static void ChangeStartingCredits(ref int ___monsterCredit)
        {
            ___monsterCredit = (int)Math.Min(int.MaxValue, Math.Round(___monsterCredit * (double)CreditMultiplier()));
        }

        private static float ScaleIncome(float credits, CombatDirector director) =>
            director.teamIndex == TeamIndex.Monster ? credits * CreditMultiplier() : credits;

        private static void RememberCredits(CombatDirector __instance, out float __state) =>
            __state = __instance.monsterCredit;

        private static void RefundSpawnCost(CombatDirector __instance, bool __result, float __state)
        {
            float multiplier = MonsterMultiplier();
            if (!__result || multiplier <= 1f || __instance.teamIndex == TeamIndex.Player ||
                (bool)singleBossSpawn.GetValue(__instance))
                return;
            float spent = __state - __instance.monsterCredit;
            if (spent > 0f)
                __instance.monsterCredit += spent * (1f - 1f / multiplier);
        }

        private static IEnumerable<CodeInstruction> ChangeIncome(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            int count = 0;
            for (int i = 0; i < code.Count; i++)
            {
                if (!(code[i].operand is MethodInfo method) || method.Name != "Update" ||
                    method.DeclaringType?.Name != "DirectorMoneyWave" || method.ReturnType != typeof(float))
                    continue;
                code.InsertRange(i + 1, new[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Plugin), nameof(ScaleIncome)))
                });
                i += 2;
                count++;
            }
            if (count != 1)
                throw new InvalidOperationException($"Expected one director income update, found {count}.");
            return code;
        }
    }
}
