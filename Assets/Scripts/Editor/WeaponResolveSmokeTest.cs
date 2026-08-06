#if UNITY_EDITOR
using Combat;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor smoke test for the Phase 6 weapon resolve chain (decision 0016). Runs the same
/// WeaponInstance.Resolve the game uses and logs the resolved profile, so the attachment fold
/// (Q10b stacking + behavior + effects) can be verified without entering play mode.
/// Menu: Tools/Horde/Weapon Resolve Smoke Test
/// </summary>
public static class WeaponResolveSmokeTest
{
    [MenuItem("Tools/Horde/Weapon Resolve Smoke Test")]
    public static void Run()
    {
        var weapons = AssetDatabase.LoadAssetAtPath<WeaponCatalog>("Assets/Data/Weapons/WeaponCatalog.asset");
        var mods = AssetDatabase.LoadAssetAtPath<AttachmentCatalog>("Assets/Data/Weapons/AttachmentCatalog.asset");
        if (weapons == null || mods == null)
        {
            Debug.LogError("[WeaponTest] catalogs missing");
            return;
        }

        // 1) plain rifle
        WeaponSlot plain = WeaponSlot.Of(0, 30);
        Log("rifle (plain)", WeaponInstance.Resolve(weapons.Get(0), plain, mods, null));

        // 2) rifle + shotgun choke (id 0): +5 pellets, spread 6, -40% damage
        WeaponSlot shotgun = WeaponSlot.Of(0, 30);
        shotgun.mod0 = 0;
        Log("rifle + ShotgunChoke", WeaponInstance.Resolve(weapons.Get(0), shotgun, mods, null));

        // 3) rifle + explosive (id 1) + vampire (id 2): effects + stat fold
        WeaponSlot mixed = WeaponSlot.Of(0, 30);
        mixed.mod0 = 1;
        mixed.mod1 = 2;
        Log("rifle + Explosive + Vampire", WeaponInstance.Resolve(weapons.Get(0), mixed, mods, null));

        // 4) launcher (projectile path, W2)
        WeaponSlot launcher = WeaponSlot.Of(1, 10);
        Log("launcher", WeaponInstance.Resolve(weapons.Get(1), launcher, mods, null));
    }

    private static void Log(string label, WeaponInstance inst)
    {
        if (inst == null)
        {
            Debug.LogError($"[WeaponTest] {label}: NULL");
            return;
        }

        WeaponProfile p = inst.profile;
        Debug.Log($"[WeaponTest] {label}: dmg={p.damage:0.##} cd={p.cooldown:0.###} mag={p.magazineSize} " +
                  $"auto={p.fullAuto} pellets={p.pelletCount} spread={p.spread} pierce={p.pierceCount} " +
                  $"projectile={p.isProjectile} projPrefab={(p.projectilePrefab != null ? p.projectilePrefab.name : "none")} " +
                  $"effects={p.effects.Length}");
    }
}
#endif
