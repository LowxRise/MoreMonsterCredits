using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace LocalTweaks
{
    internal static class PatchTools
    {
        internal static MethodInfo Method(string type, string name)
        {
            var targetType = AccessTools.TypeByName(type);
            return targetType == null
                ? throw new TypeLoadException(type)
                : AccessTools.DeclaredMethod(targetType, name) ?? throw new MissingMethodException(type, name);
        }

        internal static IEnumerable<CodeInstruction> ReplaceFloat(IEnumerable<CodeInstruction> source,
            float value, MethodInfo replacement)
        {
            var code = source.ToList();
            var matches = code.Where(x => x.opcode == OpCodes.Ldc_R4 &&
                x.operand is float number && Math.Abs(number - value) < 0.000001f).ToList();
            if (matches.Count != 1)
                throw new InvalidOperationException($"Expected one {value} constant, found {matches.Count}.");
            matches[0].opcode = OpCodes.Call;
            matches[0].operand = replacement;
            return code;
        }
    }
}
