using AmongUs.GameOptions;
using BetterAmongUs.Helpers;
using BetterAmongUs.Modules;
using BetterAmongUs.Modules.OptionItems;
using BetterAmongUs.Modules.OptionItems.NoneOption;
using BetterAmongUs.Modules.Support;
using HarmonyLib;
using UnityEngine;

namespace BetterAmongUs.Patches.Gameplay.UI.Settings;

class BetterGameSettings
{
    internal static OptionStringItem? WhenCheating;
    internal static OptionCheckboxItem? InvalidFriendCode;
    internal static OptionCheckboxItem? UseBanPlayerList;
    internal static OptionCheckboxItem? UseBanNameList;
    internal static OptionCheckboxItem? UseBanWordList;
    internal static OptionCheckboxItem? UseBanWordListOnlyLobby;
    internal static OptionIntItem? HideAndSeekImpNum;
    internal static OptionIntItem? KickLevelBelow;
    internal static OptionCheckboxItem? DetectCheatClients;
    internal static OptionCheckboxItem? DetectInvalidRPCs;
    internal static OptionStringItem? RoleRandomizer;
    internal static OptionCheckboxItem? DesyncRoles;
    internal static OptionCheckboxItem? CancelInvalidSabotage;
    internal static OptionCheckboxItem? CensorDetectionReason;
    internal static OptionCheckboxItem? RemovePetOnDeath;
    internal static OptionCheckboxItem? DisableSabotages;
}

class BetterGameSettingsTemp
{
    internal static OptionPlayerItem? HideAndSeekImp2;
    internal static OptionPlayerItem? HideAndSeekImp3;
    internal static OptionPlayerItem? HideAndSeekImp4;
    internal static OptionPlayerItem? HideAndSeekImp5;
}

[HarmonyPatch]
internal static class GameSettingsPatch
{
    internal static OptionTab? BetterSettingsTab;

    internal static void SetupSettings(bool IsPreload = false)
    {
        // Use 1800 next ID

        BetterSettingsTab = OptionTab.Create(3, "BetterSetting", "BetterSetting.Description", Color.green);

        OptionHeaderItem.Create(BetterSettingsTab, "BetterSetting.MainHeader.System");
        OptionPresetItem.Create();

        // Anti-Cheat Settings
        {
            OptionHeaderItem.Create(BetterSettingsTab, "BetterSetting.MainHeader.AntiCheat");

            if (IsPreload || GameState.IsHost)
            {
                OptionTitleItem.Create(BetterSettingsTab, "BetterSetting.TextHeader.HostOnly");
                // Default to Kick (index 1), NOT Ban (index 2). Innersloth's 2026.x
                // anti-abuse system bans the HOST when the host issues KickPlayer
                // with ban=true (which writes to AU's lobby ban list). BAU's own
                // CheatData persistence auto-re-kicks the same cheater on future
                // games anyway, so Ban mode adds risk without benefit. Users can
                // still flip this to Ban in BAU settings if they accept the risk.
                BetterGameSettings.WhenCheating = OptionStringItem.Create(100, BetterSettingsTab, "BetterSetting.Setting.WhenCheating",
                    ["BetterSetting.Setting.WhenCheating.Notify", "BetterSetting.Setting.WhenCheating.Kick", "BetterSetting.Setting.WhenCheating.Ban"], 1);
                BetterGameSettings.InvalidFriendCode = OptionCheckboxItem.Create(200, BetterSettingsTab, "BetterSetting.Setting.InvalidFriendCode", true);
                BetterGameSettings.CancelInvalidSabotage = OptionCheckboxItem.Create(900, BetterSettingsTab, "BetterSetting.Setting.CancelInvalidSabotage", true);
                BetterGameSettings.UseBanPlayerList = OptionCheckboxItem.Create(300, BetterSettingsTab, "BetterSetting.Setting.UseBanPlayerList", true);
                BetterGameSettings.UseBanNameList = OptionCheckboxItem.Create(400, BetterSettingsTab, "BetterSetting.Setting.UseBanNameList", true);
                BetterGameSettings.UseBanWordList = OptionCheckboxItem.Create(500, BetterSettingsTab, "BetterSetting.Setting.UseBanWordList", true);
                BetterGameSettings.UseBanWordListOnlyLobby = OptionCheckboxItem.Create(1400, BetterSettingsTab, "BetterSetting.Setting.UseBanWordListOnlyLobby", true, BetterGameSettings.UseBanWordList);
            }

            OptionTitleItem.Create(BetterSettingsTab, "BetterSetting.TextHeader.Detections");
            BetterGameSettings.CensorDetectionReason = OptionCheckboxItem.Create(1300, BetterSettingsTab, "BetterSetting.Setting.CensorDetectionReason", false);
            BetterGameSettings.KickLevelBelow = OptionIntItem.Create(1700, BetterSettingsTab, "BetterSetting.Setting.KickLevelBelow", (0, 10000, 1), 0, ("Lv ", ""));
            BetterGameSettings.DetectCheatClients = OptionCheckboxItem.Create(700, BetterSettingsTab, "BetterSetting.Setting.DetectCheatClients", true);
            BetterGameSettings.DetectInvalidRPCs = OptionCheckboxItem.Create(800, BetterSettingsTab, "BetterSetting.Setting.DetectInvalidRPCs", true);
        }

        if (IsPreload || GameState.IsHost)
        {
            OptionHeaderItem.Create(BetterSettingsTab, "BetterSetting.MainHeader.RoleAlgorithm");
            BetterGameSettings.RoleRandomizer = OptionStringItem.Create(1100, BetterSettingsTab, "BetterSetting.Setting.RoleRandomizer", ["System.Random", "UnityEngine.Random"], 0);
            BetterGameSettings.DesyncRoles = OptionCheckboxItem.Create(1200, BetterSettingsTab, "BetterSetting.Setting.DesyncRoles", true);
        }

        // Gameplay Settings
        {
            if (IsPreload || GameState.IsHost && GameState.IsPrivateOnlyLobby)
            {
                if (IsPreload || !GameState.IsHideNSeek)
                {
                    OptionHeaderItem.Create(BetterSettingsTab, "BetterSetting.MainHeader.Gameplay");
                    BetterGameSettings.DisableSabotages = OptionCheckboxItem.Create(1500, BetterSettingsTab, "BetterSetting.Setting.DisableSabotages", false);
                    BetterGameSettings.RemovePetOnDeath = OptionCheckboxItem.Create(1600, BetterSettingsTab, "BetterSetting.Setting.RemovePetOnDeath", false);
                }
                else if (IsPreload || GameState.IsHideNSeek)
                {
                    OptionHeaderItem.Create(BetterSettingsTab, "BetterSetting.MainHeader.HideNSeek");
                    OptionTitleItem.Create(BetterSettingsTab, $"<color={RoleTypes.Impostor.GetRoleHex()}>{Translator.GetString(StringNames.ImpostorsCategory)}</color>");
                    BetterGameSettings.HideAndSeekImpNum = OptionIntItem.Create(1000, BetterSettingsTab, "BetterSetting.Setting.HideAndSeekImpNum", (1, 5, 1), 1);

                    BetterGameSettingsTemp.HideAndSeekImp2 = OptionPlayerItem.Create(0, BetterSettingsTab, "BetterSetting.TempSetting.HideAndSeekImpNum", BetterGameSettings.HideAndSeekImpNum);
                    BetterGameSettingsTemp.HideAndSeekImp2.ShowCondition = () => BetterGameSettings.HideAndSeekImpNum.GetValue() >= 2;
                    BetterGameSettingsTemp.HideAndSeekImp3 = OptionPlayerItem.Create(1, BetterSettingsTab, "BetterSetting.TempSetting.HideAndSeekImpNum", BetterGameSettings.HideAndSeekImpNum);
                    BetterGameSettingsTemp.HideAndSeekImp3.ShowCondition = () => BetterGameSettings.HideAndSeekImpNum.GetValue() >= 3 &&
                    BetterGameSettingsTemp.HideAndSeekImp2.GetValue() != -1;
                    BetterGameSettingsTemp.HideAndSeekImp4 = OptionPlayerItem.Create(2, BetterSettingsTab, "BetterSetting.TempSetting.HideAndSeekImpNum", BetterGameSettings.HideAndSeekImpNum);
                    BetterGameSettingsTemp.HideAndSeekImp4.ShowCondition = () => BetterGameSettings.HideAndSeekImpNum.GetValue() >= 4 &&
                    BetterGameSettingsTemp.HideAndSeekImp2.GetValue() != -1 &&
                    BetterGameSettingsTemp.HideAndSeekImp3.GetValue() != -1;
                    BetterGameSettingsTemp.HideAndSeekImp5 = OptionPlayerItem.Create(3, BetterSettingsTab, "BetterSetting.TempSetting.HideAndSeekImpNum", BetterGameSettings.HideAndSeekImpNum);
                    BetterGameSettingsTemp.HideAndSeekImp5.ShowCondition = () => BetterGameSettings.HideAndSeekImpNum.GetValue() >= 5 &&
                    BetterGameSettingsTemp.HideAndSeekImp2.GetValue() != -1 &&
                    BetterGameSettingsTemp.HideAndSeekImp3.GetValue() != -1 &&
                    BetterGameSettingsTemp.HideAndSeekImp4.GetValue() != -1;
                }
            }
        }

        BetterSettingsTab.UpdateVisuals();
    }

    [HarmonyPatch(typeof(GameSettingMenu), nameof(GameSettingMenu.Start))]
    [HarmonyPostfix]
    private static void GameSettingMenu_Start_Postfix(GameSettingMenu __instance)
    {
        if (BAUModdedSupportFlags.HasFlag(BAUModdedSupportFlags.Disable_AllGameOptions)) return;

        SetupSettings();

        __instance.gameObject.transform.SetLocalY(-0.1f);
        GameObject PanelSprite = __instance.gameObject.transform.Find("PanelSprite").gameObject;
        if (PanelSprite != null)
        {
            PanelSprite.transform.SetLocalY(-0.32f);
            PanelSprite.transform.localScale = new Vector3(PanelSprite.transform.localScale.x, 0.625f);
        }

        __instance.GamePresetsButton.OnMouseOver.RemoveAllListeners();
        __instance.GameSettingsButton.OnMouseOver.RemoveAllListeners();
        __instance.RoleSettingsButton.OnMouseOver.RemoveAllListeners();


        BetterSettingsTab.TabButton.transform.localPosition = BetterSettingsTab.TabButton.transform.localPosition - new Vector3(0f, 1.265f, 0f);
        if (!GameState.IsHideNSeek && GameState.IsHost)
        {
            __instance.ChangeTab(1, false);
        }
        else if (GameState.IsHost)
        {
            __instance.RoleSettingsButton.gameObject.SetActive(true);
            __instance.RoleSettingsButton.GetComponent<PassiveButton>().enabled = false;
            __instance.RoleSettingsButton.inactiveSprites.GetComponent<SpriteRenderer>().color = new(0.5f, 0.5f, 0.5f, 1f);
            __instance.ChangeTab(1, false);
        }
        else
        {
            __instance.GamePresetsButton.GetComponent<PassiveButton>().enabled = false;
            __instance.GameSettingsButton.GetComponent<PassiveButton>().enabled = false;
            __instance.RoleSettingsButton.GetComponent<PassiveButton>().enabled = false;
            __instance.GamePresetsButton.inactiveSprites.GetComponent<SpriteRenderer>().color = new(0.5f, 0.5f, 0.5f, 1f);
            __instance.GameSettingsButton.inactiveSprites.GetComponent<SpriteRenderer>().color = new(0.5f, 0.5f, 0.5f, 1f);
            __instance.RoleSettingsButton.inactiveSprites.GetComponent<SpriteRenderer>().color = new(0.5f, 0.5f, 0.5f, 1f);
            __instance.ChangeTab(3, false);
        }
    }

    [HarmonyPatch(typeof(GameSettingMenu), nameof(GameSettingMenu.ChangeTab))]
    [HarmonyPrefix]
    private static void GameSettingMenu_ChangeTab_Prefix(GameSettingMenu __instance, [HarmonyArgument(0)] int tabNum, [HarmonyArgument(1)] bool previewOnly)
    {
        if (BAUModdedSupportFlags.HasFlag(BAUModdedSupportFlags.Disable_AllGameOptions)) return;

        if (BetterSettingsTab == null) return;

        BetterSettingsTab.AUTab?.gameObject?.SetActive(false);
        BetterSettingsTab.TabButton?.SelectButton(false);

        if (previewOnly && Controller.currentTouchType == Controller.TouchType.Joystick || !previewOnly)
        {
            switch (tabNum)
            {
                case 3:
                    BetterSettingsTab.AUTab?.gameObject?.SetActive(true);
                    BetterSettingsTab.TabButton?.SelectButton(true);
                    __instance.MenuDescriptionText.text = BetterSettingsTab.Description;
                    break;
            }
        }
    }

    [HarmonyPatch(typeof(GameOptionsMenu), nameof(GameOptionsMenu.CreateSettings))]
    [HarmonyPrefix]
    private static bool GameOptionsMenu_CreateSettings_Prefix(GameOptionsMenu __instance)
    {
        if (BAUModdedSupportFlags.HasFlag(BAUModdedSupportFlags.Disable_AllGameOptions)) return true;

        if (__instance == BetterSettingsTab.AUTab)
        {
            return false;
        }

        return true;
    }

    [HarmonyPatch(typeof(OptionsConsole), nameof(OptionsConsole.CanUse))]
    [HarmonyPrefix]
    private static void OptionsConsole_CanUse_Prefix(OptionsConsole __instance)
    {
        if (BAUModdedSupportFlags.HasFlag(BAUModdedSupportFlags.Disable_AllGameOptions)) return;

        __instance.HostOnly = false;
    }
}