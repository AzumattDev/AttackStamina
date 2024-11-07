using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace AttackStamina;

[HarmonyPatch(typeof(Hud), nameof(Hud.Awake))]
static class HudAwakePatch
{
    internal static RectTransform _StaminaBarRect = null!;
    internal static GameObject _StaminaBarUI = null!;
    internal static RectTransform _StaminaBarUIRect = null!;
    internal static Slider _StaminaBar = null!;
    internal static RectTransform _StaminaPanelRoot = null!;

    [HarmonyPriority(Priority.Last)]
    static void Postfix(Hud __instance)
    {
        RectTransform? root;
        if (AttackStaminaPlugin.betterUIInstalled && __instance.m_rootObject.transform.Find("BetterUI_StaminaBar") != null)
            root = __instance.m_rootObject.transform.Find("BetterUI_StaminaBar").GetComponent<RectTransform>();
        else
            root = __instance.m_staminaBar2Root;
        if (root == null) return;
        AttackStaminaPlugin.flag1 = true;
        AttackStaminaPlugin.AttackStaminaLogger.LogDebug("Instantiating AttackStamina Bar");
        _StaminaBarUI = Object.Instantiate(AttackStaminaPlugin.StaminaUI, root);
        _StaminaBarUIRect = _StaminaBarUI.GetComponent<RectTransform>();
        _StaminaBar = _StaminaBarUI.GetComponentInChildren<Slider>();
        _StaminaBar.gameObject.SetActive(false);
        _StaminaBarRect = _StaminaBar.GetComponent<RectTransform>();
        _StaminaBarRect.transform.Rotate(new Vector3(0.0f, 0.0f, -90f));

        _StaminaPanelRoot = root;
    }
}

[HarmonyPatch(typeof(Character), nameof(Character.Jump))]
static class CharacterJumpPatch
{
    static void Prefix(Character __instance)
    {
        if (AttackStaminaPlugin.useAttackWhenDrained.Value == AttackStaminaPlugin.Toggle.Off || __instance.HaveStamina(__instance.m_jumpStaminaUsage))
            return;

        if (AttackStaminaPlugin.attackStamina >= __instance.m_jumpStaminaUsage && __instance.IsOnGround())
        {
            __instance.m_jumpForceTiredFactor = 1f;
            HumanoidStartAttackPatch.UseAttackStaminaMod(__instance.m_jumpStaminaUsage);
        }
        else
        {
            __instance.m_jumpForceTiredFactor = AttackStaminaPlugin.noStaminaJumpForce.Value;
            Hud.instance.StaminaBarEmptyFlash();
        }
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.CheckRun))]
static class PlayerCheckRunPatch
{
    static void Prefix(Player __instance) => AttackStaminaPlugin.UseAttackInstead(__instance);
}

[HarmonyPatch(typeof(Character), nameof(Character.OnSwimming))]
static class CharacterOnSwimmingPatch
{
    static void Prefix(Character __instance)
    {
        if (AttackStaminaPlugin.useAttackWhenDrained.Value == AttackStaminaPlugin.Toggle.Off || __instance.HaveStamina())
            return;

        if (AttackStaminaPlugin.attackStamina > 0.0)
        {
            __instance.AddStamina(1f);
            HumanoidStartAttackPatch.UseAttackStaminaMod(AttackStaminaPlugin.noStaminaSprintDrain.Value);
        }
        else
            Hud.instance.StaminaBarEmptyFlash();
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.UpdateCrouch))]
static class PlayerUpdateCrouchPatch
{
    static void Prefix(Player __instance) => AttackStaminaPlugin.UseAttackInstead(__instance, true);
}

[HarmonyPatch(typeof(Humanoid), nameof(Humanoid.StartAttack))]
static class HumanoidStartAttackPatch
{
    internal static Player player1;
    internal static bool DoAttack = true;
    private static float lastAttackTime = 0f;

    static bool Prefix(Humanoid __instance, bool secondaryAttack)
    {
        float attackStamina = __instance.GetCurrentWeapon().m_shared.m_attack.m_attackStamina;
        float secondattackStamina = __instance.GetCurrentWeapon().m_shared.m_secondaryAttack.m_attackStamina;

        if (!__instance.IsPlayer() || __instance != Player.m_localPlayer || (AttackStaminaPlugin.attackStamina - (double)attackStamina >= 0.0 && DoAttack && !secondaryAttack) || (AttackStaminaPlugin.attackStamina - (double)secondattackStamina >= 0.0 && DoAttack && secondaryAttack))
            return true;

        // Debounce to prevent rapid stamina depletion
        if (Time.time - lastAttackTime < 0.1f)
            return false;

        lastAttackTime = Time.time;

        if (AttackStaminaPlugin.attackStamina >= attackStamina && DoAttack)
            return true;

        player1 = (Player)__instance;

        if (AttackStaminaPlugin.attackStamina >= attackStamina)
        {
            DoAttack = true;
            return true;
        }

        if (player1.GetStamina() >= attackStamina && AttackStaminaPlugin.attackStamina < attackStamina)
        {
            if (AttackStaminaPlugin.useNormWhenDrained.Value == AttackStaminaPlugin.Toggle.Off)
                return false;

            AttackStaminaPlugin.attackStamina = 0.0f;
            return true;
        }

        Hud.instance.StaminaBarEmptyFlash();
        DoAttack = false;
        return false;
    }

    public static void UseAttackStaminaMod(float v)
    {
        AttackStaminaPlugin.attackStamina -= v;
        AttackStaminaPlugin.counter = 0;
        AttackStaminaPlugin.displayCounter = 0;
        AttackStaminaPlugin.flag = true;
        AttackStaminaPlugin.hasUsedRecently = true;
    }
}

[HarmonyPatch(typeof(Attack), nameof(Attack.GetAttackStamina))]
static class AttackGetAttackStaminaPatch
{
    static void Prefix(Attack __instance)
    {
        if (__instance.m_character.IsPlayer())
        {
            if (AttackStaminaPlugin.attackStamina >= __instance.m_attackStamina && !__instance.m_character.InAttack())
            {
                HumanoidStartAttackPatch.DoAttack = true;
                HumanoidStartAttackPatch.UseAttackStaminaMod(__instance.m_attackStamina);
            }
            else if (AttackStaminaPlugin.attackStamina <= 0.0 && ((Player)__instance.m_character).GetStamina() > 0.0 && ((Player)__instance.m_character).GetStamina() >= __instance.m_attackStamina)
            {
                HumanoidStartAttackPatch.DoAttack = true;
                ((Player)__instance.m_character).UseStamina(__instance.m_attackStamina);
            }

            __instance.m_attackStamina = 0.0f;
        }
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.UpdateAttackBowDraw))]
static class PlayerUpdateAttackBowDrawPatch
{
    static void Prefix(Player __instance)
    {
        ItemDrop.ItemData currentWeapon = __instance.GetCurrentWeapon();

        if (!__instance.IsDrawingBow())
            return;

        float num = AttackStaminaPlugin.attackStamina > 0.0 ? 1f : 0.0f;
        float drawStaminaDrain = currentWeapon.GetDrawStaminaDrain();

        if (__instance.GetAttackDrawPercentage() >= 1.0)
            drawStaminaDrain *= 0.5f;

        HumanoidStartAttackPatch.UseAttackStaminaMod(drawStaminaDrain * Time.fixedDeltaTime);
        __instance.UseStamina(-num * drawStaminaDrain * Time.fixedDeltaTime);
    }
}

[HarmonyPatch(typeof(Hud), nameof(Hud.OnDestroy))]
static class HudOnDestroyPatch
{
    static void Prefix(Hud __instance) => AttackStaminaPlugin.flag1 = false;
}

[HarmonyPatch(typeof(Hud), nameof(Hud.Update))]
static class HudFixedUpdatePatch
{
    static void Prefix(Hud __instance)
    {
        if (AttackStaminaPlugin.flag)
            ++AttackStaminaPlugin.counter;

        if (AttackStaminaPlugin.hasUsedRecently && AttackStaminaPlugin.attackStamina >= AttackStaminaPlugin.MaxAttackStamina.Value)
            ++AttackStaminaPlugin.displayCounter;

        if (AttackStaminaPlugin.attackStamina <= AttackStaminaPlugin.MaxAttackStamina.Value && IsRegening(AttackStaminaPlugin.counter))
            AttackStaminaPlugin.attackStamina += 0.5f * AttackStaminaPlugin.AttackStaminaRecharge.Value;

        if (AttackStaminaPlugin.attackStamina > AttackStaminaPlugin.MaxAttackStamina.Value)
        {
            AttackStaminaPlugin.attackStamina = AttackStaminaPlugin.MaxAttackStamina.Value;
            AttackStaminaPlugin.flag = false;
        }

        if (!AttackStaminaPlugin.flag1)
            return;

        HudAwakePatch._StaminaBar.maxValue = AttackStaminaPlugin.MaxAttackStamina.Value;
        HudAwakePatch._StaminaBar.normalizedValue = AttackStaminaPlugin.attackStamina / 100f;
        HudAwakePatch._StaminaBar.value = AttackStaminaPlugin.attackStamina;
        HudAwakePatch._StaminaBar.gameObject.SetActive(IsShowing());
        //AttackStaminaPlugin.AttackStaminaLogger.LogError("Is Showing: " + isShowing());
        RectTransform component2 = HudAwakePatch._StaminaPanelRoot;
        HudAwakePatch._StaminaBarUIRect.anchorMin = AttackStaminaPlugin.uiAnchorMin.Value;
        HudAwakePatch._StaminaBarUIRect.anchorMax = AttackStaminaPlugin.uiAnchorMax.Value;
        HudAwakePatch._StaminaBar.GetComponent<RectTransform>().sizeDelta = new Vector2(component2.sizeDelta.x - (AttackStaminaPlugin.uiDeltaOffset.Value.x), component2.sizeDelta.y - (AttackStaminaPlugin.uiDeltaOffset.Value.y));
        HudAwakePatch._StaminaBarUIRect.anchoredPosition = new Vector2(0.0f + +AttackStaminaPlugin.uiAnchoredPosition.Value.x, component2.anchoredPosition.y + AttackStaminaPlugin.uiAnchoredPosition.Value.y);
    }

    public static bool IsRegening(int counter)
    {
        if (counter <= AttackStaminaPlugin.timeTillCharging.Value)
            return false;

        // AttackStaminaPlugin.counter = 0;
        AttackStaminaPlugin.flag = false;
        return true;
    }

    public static bool IsShowing()
    {
        if (!AttackStaminaPlugin.hasUsedRecently)
            return false;

        if (AttackStaminaPlugin.displayCounter <= AttackStaminaPlugin.displayTime.Value)
            return true;

        AttackStaminaPlugin.hasUsedRecently = false;
        AttackStaminaPlugin.displayCounter = 0;
        return false;
    }
}