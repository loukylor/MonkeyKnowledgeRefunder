using HarmonyLib;
using Il2Cpp;
using Il2CppAssets.Scripts.Data.Knowledge;
using Il2CppAssets.Scripts.Models.Knowledge;
using Il2CppAssets.Scripts.Unity;
using Il2CppAssets.Scripts.Unity.UI_New.Knowledge;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

[assembly: MelonInfo(typeof(MonkeyKnowledgeRefunder.ModMain), "Monkey Knowledge Refunder", "1.0.0", "loukylor")]
[assembly: MelonGame("Ninja Kiwi", "BloonsTD6")]

namespace MonkeyKnowledgeRefunder;

public class ModMain : MelonMod
{
    private static GameObject _buttonCopy;
    private static MelonLogger.Instance _logger;

    public override void OnInitializeMelon()
    {
        MethodBase methodToPatch = AccessTools.Method(typeof(KnowledgeSkillTree), nameof(KnowledgeSkillTree.BtnClicked));
        MethodInfo patchMethod = AccessTools.Method(typeof(ModMain), nameof(OnButtonSelected));
        if (methodToPatch == null || patchMethod == null)
        {
            LoggerInstance.Error("Could not initialize properly!!! This mod will not work!");
            return;
        }

        HarmonyInstance.Patch(methodToPatch, postfix: new HarmonyMethod(patchMethod));

        methodToPatch = AccessTools.Method(typeof(KnowledgeSkillTree), nameof(KnowledgeSkillTree.AddPointClicked));
        patchMethod = AccessTools.Method(typeof(ModMain), nameof(OnKnowledgePurchased));
        if (methodToPatch == null || patchMethod == null)
        {
            LoggerInstance.Warning("Could not initialize part of this mod. The purchase button won't automatically be swapped out with the refund one.");
            return;
        }

        HarmonyInstance.Patch(methodToPatch, postfix: new HarmonyMethod(patchMethod));

        _logger = LoggerInstance;
    }

    public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
    {
        UnityEngine.Object.Destroy(_buttonCopy);
        _buttonCopy = null;
    }

    private static void OnButtonSelected(KnowledgeSkillTree __instance, KnowledgeSkillBtn __0)
    {
        if (__0 == null || __0.CurrState != KnowlegdeSkillBtnState.Purchased || !Game.Player.HasKnowledge(__0.KnowledgeID))
        {
            if (_buttonCopy != null)
            {
                _buttonCopy.active = false;
            }
        }
        else
        {
            if (_buttonCopy == null)
            {
                _buttonCopy = CreateButtonCopy(__instance);
            }
            _buttonCopy.active = IsKnowledgeRefundable(__0.knowledgeID);
        }
    }

    private static bool IsKnowledgeRefundable(string knowledgeId)
    {
        KnowledgeModel knowledge = KnowledgeHelper.GetKnowledge(knowledgeId);
        if (knowledge == null)
        {
            return false;
        }

        // Check if the knowledge is a prerequisite to any other knowledge that we own
        Il2CppReferenceArray<KnowledgeModel> knowledgeInCategory = KnowledgeHelper.GetKnowledgeForCategory(knowledge.category);
        for (int i = 0; i < knowledgeInCategory.Length; i++)
        {
            if (Game.Player.HasKnowledge(knowledgeInCategory[i].name)
                && knowledgeInCategory[i].prerequisiteIds.Contains(knowledgeId))
            {
                return false;
            }
        }

        return true;
    }

    private static GameObject CreateButtonCopy(KnowledgeSkillTree tree)
    {
        GameObject buttonCopy = UnityEngine.Object.Instantiate(
            tree.selectedPanelAddPointsBtn.gameObject,
            tree.selectedPanelAddPointsBtn.transform.parent);

        buttonCopy.name = "RemovePointButton";
        buttonCopy.active = true;

        // Change text to "Refund+1"
        Transform addPointTextTransform = buttonCopy.transform.Find("AddPointTxt");
        if (addPointTextTransform != null)
        {
            NK_TextMeshProUGUI text = addPointTextTransform.GetComponent<NK_TextMeshProUGUI>();
            if (text != null)
            {
                text.AutoLocalize = false;
                text.text = "Refund+1";
            }
            else
            {
                _logger.Warning("Could not find text object to replace the text of. Refund text will be incorrect!");
            }
        }
        else
        {
            _logger.Warning("Could not find text transform to replace the text of. Refund text will be incorrect!");
        }

        // Add our own button click event
        Button button = buttonCopy.GetComponent<Button>();
        if (button != null)
        {
            Button.ButtonClickedEvent eventListener = button.onClick;
            eventListener.RemoveAllListeners();
            eventListener.AddListener(new Action(() => OnRefund(tree)));
        }
        else
        {
            _logger.Error("Could not find button to replace callbacks of. The mod will not function!!!");
        }

        // Change image to be red rather than purple
        Transform imageTransform = buttonCopy.transform.Find("Image");
        if (imageTransform != null)
        {
            Image image = imageTransform.GetComponent<Image>();
            if (image != null)
            {
                // Not the best for performance, but I reallllllly doubt this will make a very big difference
                image.material = UnityEngine.Object.Instantiate(image.material);
                image.material.color = Color.red;
            }
            else
            {
                _logger.Warning("Could not find image object to change the color of. The button background image will not be red!");
            }
        }
        else
        {
            _logger.Warning("Could not find image transform to change the color of. The button background image will not be red!");
        }

        Transform monkeyMoneyTransform = buttonCopy.transform.Find("MonkeyMoney");
        if (monkeyMoneyTransform != null)
        {
            UnityEngine.Object.Destroy(monkeyMoneyTransform.gameObject);
        }
        else
        {
            _logger.Warning("Could not find monkey money transform to remove. The displayed monkey money will not be refunded!");
        }

        return buttonCopy;
    }

    private static void OnRefund(KnowledgeSkillTree tree)
    {
        if (tree.currSelectedBtn == null)
        {
            return;
        }
        
        string knowledgeId = tree.currSelectedBtn.KnowledgeID;
        if (!Game.Player.HasKnowledge(knowledgeId) || !Game.Player.Data.acquiredKnowledge.Remove(knowledgeId))
        {
            // We didn't remove anything, so return early and don't change anything
            return;
        }

        Game.Player.Data.KnowledgePoints++;

        tree.UpdateButtonStates();
        Button selectedPanelButton = tree.selectedPanelAddPointsBtn;
        if (selectedPanelButton != null)
        {
            selectedPanelButton.gameObject.SetActive(KnowledgeHelper.IsKnowledgePurchasable(knowledgeId));
        }
        
        if (_buttonCopy != null)
        {
            _buttonCopy.SetActive(false);
        }
    }

    private static void OnKnowledgePurchased(KnowledgeSkillTree __instance)
    {
        if (__instance.currSelectedBtn == null)
        {
            return;
        }

        if (__instance.currSelectedBtn.CurrState == KnowlegdeSkillBtnState.Purchased)
        {
            if (_buttonCopy == null)
            {
                _buttonCopy = CreateButtonCopy(__instance);
            }
            _buttonCopy.active = true;
        }
    }
}
