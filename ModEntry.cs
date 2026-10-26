using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;

namespace ArtificialCreation;

[ModInitializer(nameof(Initialize))]
public static class ModEntry
{
    public static void Initialize()
    {
        new Harmony("rrenaud.ArtificialCreation").PatchAll(Assembly.GetExecutingAssembly());
        Diagnostics.AutoCapture.Initialize();
    }
}
