using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace AttackStamina;

[HarmonyPatch(typeof(Hud), nameof(Hud.Awake))]
static class HudAwakePatch
{
    static void Postfix(Hud __instance)
    {
        AttackStaminaPlugin.flag1 = true;
        AttackStaminaPlugin.AttackStaminaLogger.LogDebug("Instantiating AttackStamina Bar");
        HudFixedUpdatePatch._StaminaBar = UnityEngine.Object.Instantiate(AttackStaminaPlugin.StaminaUI, __instance.transform).GetComponentInChildren<Slider>();
        HudFixedUpdatePatch._StaminaBar.gameObject.SetActive(false);
        HudFixedUpdatePatch._StaminaBar.GetComponent<RectTransform>().transform.Rotate(new Vector3(0.0f, 0.0f, -90f));
    }
}

[HarmonyPatch(typeof(Character), nameof(Character.Jump))]
static class CharacterJumpPatch
{
    static void Prefix(Character __instance)
    {
        if (AttackStaminaPlugin.useAttackWhenDrained.Value == AttackStaminaPlugin.Toggle.Off || __instance.HaveStamina(__instance.m_jumpStaminaUsage))
            return;
        if (AttackStaminaPlugin.attackStamina - (double)__instance.m_jumpStaminaUsage >= 0.0 && __instance.IsOnGround())
        {
            __instance.m_jumpForceTiredFactor = 1f;
            HumanoidStartAttackPatch.useAttackStaminaMod(__instance.m_jumpStaminaUsage);
        }
        else if (AttackStaminaPlugin.attackStamina - (double)__instance.m_jumpStaminaUsage < __instance.m_jumpStaminaUsage)
        {
            __instance.m_jumpForceTiredFactor = AttackStaminaPlugin.noStaminaJumpForce.Value;
            Hud.instance.StaminaBarEmptyFlash();
        }
        else
            Hud.instance.StaminaBarEmptyFlash();
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
            HumanoidStartAttackPatch.useAttackStaminaMod(AttackStaminaPlugin.noStaminaSprintDrain.Value);
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

    static bool Prefix(Humanoid __instance)
    {
        float attackStamina = __instance.GetCurrentWeapon().m_shared.m_attack.m_attackStamina;
        if (!__instance.IsPlayer() || AttackStaminaPlugin.attackStamina - (double)attackStamina >= 0.0 && DoAttack)
            return true;
        player1 = (Player)__instance;
        if (AttackStaminaPlugin.attackStamina >= (double)attackStamina)
        {
            DoAttack = true;
            return true;
        }

        if (player1.GetStamina() - (double)attackStamina >= 0.0 && AttackStaminaPlugin.attackStamina - (double)attackStamina < 0.0)
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

    public static void useAttackStaminaMod(float v)
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
            if ((double)AttackStaminaPlugin.attackStamina - (double)__instance.m_attackStamina >= 0.0 && __instance.m_character.IsPlayer())
            {
                HumanoidStartAttackPatch.DoAttack = true;
                HumanoidStartAttackPatch.useAttackStaminaMod(__instance.m_attackStamina);
            }
            else if ((double)AttackStaminaPlugin.attackStamina <= 0.0 && (double)((Player)__instance.m_character).GetStamina() > 0.0 && (double)((Player)__instance.m_character).GetStamina() - (double)__instance.m_attackStamina >= 0.0)
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
        HumanoidStartAttackPatch.useAttackStaminaMod(drawStaminaDrain * Time.fixedDeltaTime);
        __instance.UseStamina(-num * drawStaminaDrain * Time.fixedDeltaTime);
    }
}

[HarmonyPatch(typeof(Hud), nameof(Hud.OnDestroy))]
static class HudOnDestroyPatch
{
    static void Prefix(Hud __instance) => AttackStaminaPlugin.flag1 = false;
}

[HarmonyPatch(typeof(Hud), nameof(Hud.FixedUpdate))]
static class HudFixedUpdatePatch
{
    internal static Slider _StaminaBar;

    static void Prefix(Hud __instance)
    {
        if (AttackStaminaPlugin.flag)
            ++AttackStaminaPlugin.counter;
        if (AttackStaminaPlugin.hasUsedRecently && AttackStaminaPlugin.attackStamina >= (double)AttackStaminaPlugin.MaxAttackStamina.Value)
            ++AttackStaminaPlugin.displayCounter;
        if (AttackStaminaPlugin.attackStamina <= (double)AttackStaminaPlugin.MaxAttackStamina.Value && IsRegening(AttackStaminaPlugin.counter))
            AttackStaminaPlugin.attackStamina += 0.5f * AttackStaminaPlugin.AttackStaminaRecharge.Value;
        if (AttackStaminaPlugin.attackStamina > (double)AttackStaminaPlugin.MaxAttackStamina.Value)
        {
            AttackStaminaPlugin.attackStamina = AttackStaminaPlugin.MaxAttackStamina.Value;
            AttackStaminaPlugin.flag = false;
        }

        if (!AttackStaminaPlugin.flag1)
            return;
        Slider componentInChildren = _StaminaBar.GetComponentInChildren<Slider>();
        componentInChildren.maxValue = AttackStaminaPlugin.MaxAttackStamina.Value;
        componentInChildren.normalizedValue = AttackStaminaPlugin.attackStamina / 100f;
        componentInChildren.value = AttackStaminaPlugin.attackStamina;
        _StaminaBar.gameObject.SetActive(isShowing());
        //AttackStaminaPlugin.AttackStaminaLogger.LogError("Is Showing: " + isShowing());
        Transform? parent = _StaminaBar.transform.parent.parent;
        RectTransform component1 = parent.GetComponent<RectTransform>();
        RectTransform component2 = parent.parent.GetChild(0).GetChild(2).GetComponent<RectTransform>();
        component1.anchorMin = new Vector2(0.5f, 0.0f);
        component1.anchorMax = new Vector2(0.5f, 0.0f);
        _StaminaBar.GetComponent<RectTransform>().sizeDelta = new Vector2(component2.sizeDelta.x - 12f, component2.sizeDelta.y - 15f);
        component1.anchoredPosition = new Vector2(0.0f, component2.anchoredPosition.y + 30f);
    }

    public static bool IsRegening(int counter)
    {
        if (counter <= AttackStaminaPlugin.timeTillCharging.Value)
            return false;
        counter = 0;
        AttackStaminaPlugin.flag = false;
        return true;
    }

    public static bool isShowing()
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