using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using LocalTweaks;
using RoR2;
using UnityEngine.Networking;

namespace MoreMonsterCredits
{
    [BepInPlugin(Guid, Name, "1.0.0")]
    [BepInDependency("com.rune580.riskofoptions")]
    [BepInDependency("Wolfo.LittleGameplayTweaks", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.youssef.MoreMonsterCredits";
        public const string Name = "More Monster Credits";
        private static readonly ConfigEntry<int>[] bonuses = new ConfigEntry<int>[8];
        private readonly Harmony harmony = new Harmony(Guid);

        private void Awake()
        {
            for (int stage = 3; stage <= 10; stage++)
                bonuses[stage - 3] = TweakSettings.Slider(Config, "Monster credits", $"Stage {stage} bonus percent", 0, 0, 200,
                    "Extra monster budget: 0 leaves it alone, 100 doubles it, 200 triples it. Ongoing income changes immediately; starting spawns change on the next stage entry. Hidden realms do not count.", Guid, Name);
            try
            {
                harmony.Patch(AccessTools.Method(typeof(CombatDirector), "FixedUpdate"),
                    transpiler: new HarmonyMethod(typeof(Plugin), nameof(ChangeIncome)));
                harmony.Patch(AccessTools.Method(typeof(SceneDirector), "PopulateScene"),
                    prefix: new HarmonyMethod(typeof(Plugin), nameof(ChangeStartingCredits)));
                Logger.LogInfo("Monster credit tweaks loaded.");
            }
            catch (Exception exception)
            {
                harmony.UnpatchSelf();
                Logger.LogError($"Monster credit tweaks were not applied.\n{exception}");
            }
        }

        private void OnDestroy() => harmony.UnpatchSelf();

        private static float Multiplier()
        {
            var run = Run.instance;
            var scene = SceneCatalog.mostRecentSceneDef;
            if (!NetworkServer.active || !run || !scene || scene.sceneType != SceneType.Stage ||
                scene.isFinalStage || !scene.validForRandomSelection)
                return 1f;
            int index = run.stageClearCount - 2;
            return index >= 0 && index < bonuses.Length ? 1f + bonuses[index].Value / 100f : 1f;
        }

        private static void ChangeStartingCredits(ref int ___monsterCredit)
        {
            ___monsterCredit = (int)Math.Min(int.MaxValue, Math.Round(___monsterCredit * (double)Multiplier()));
        }

        private static float ScaleIncome(float credits, CombatDirector director) =>
            director.teamIndex == TeamIndex.Monster ? credits * Multiplier() : credits;

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
