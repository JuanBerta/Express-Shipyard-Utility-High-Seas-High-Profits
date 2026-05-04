using HarmonyLib;
using MelonLoader;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using zip.lexy.tgame.city.ship.build;
using zip.lexy.tgame.simulation.trading;
using zip.lexy.tgame.state;
using zip.lexy.tgame.ui.settings;
using zip.lexy.tgame.ui.shipyard;
using zip.lexy.tgame.ui.widget.trader.townhall;

namespace Utility_Pack_Shipyard_Economy
{
    public class Utility_Pack_Shipyard_Economy_Class : MelonMod
    {
        [HarmonyPatch(typeof(TradingSimulationManager), nameof(TradingSimulationManager.GetAllTransactionsForGoods))]
        public static class Shipyard_Transaction_Patch
        {
            public static void Postfix(List<string> goodsToTransact, List<TradingTransaction> __result, TradingSimulationManager __instance)
            {
                if (PlayerPrefs.GetInt("mod.shipyard.buy_all", 0) == 0) return;

                // Access the protected gameState via Traverse
                var gameState = Traverse.Create(__instance).Field("gameState").GetValue<GameState>();
                if (gameState == null) return;

                foreach (var transaction in __result)
                {
                    if (gameState.corePrices.TryGetValue(transaction.good, out float basePrice))
                    {
                        // Calculate the 4x Express Cost
                        transaction.goodCost = (int)(transaction.required * basePrice * 4);

                        // Unlock the UI: Tell the game we have everything we need
                        transaction.neededAfterMarket = 0f;
                        transaction.insufficientGood = false;

                        // Visuals: Make the UI show that we are purchasing the full amount
                        transaction.boughtFromMarket = transaction.required;
                        transaction.takenFromWarehouse = 0;
                        transaction.takenFromShips = 0;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(TradingSimulationManager), nameof(TradingSimulationManager.UseGoods))]
        public static class Shipyard_Spend_Patch
        {
            [HarmonyPrefix]
            public static bool Prefix(List<string> goodsToTransact, Func<string, int> getRequiredAmount, TradingSimulationManager __instance, ref int __result)
            {
                // 1. If mod is off, let the game handle it normally
                if (PlayerPrefs.GetInt("mod.shipyard.buy_all", 0) == 0) return true;

                // 2. Check if we are in the Mayor Quest Confirmation Window (since it also uses the trading system for item turn-ins)
                var mayorWindow = UnityEngine.Object.FindObjectOfType<MayorQuestTradeConfirmationWindow>();
                if (mayorWindow != null && mayorWindow.visible)
                {
                    SendMelonLoggerMessage("Detected Mayor Quest Confirmation Window. Skipping Express Shipyard logic for this transaction.");
                    return true; // Let vanilla logic handle the quest (subtract items)
                }

                // 3. Check if we are in the Shipyard Build Window (safety check to avoid affecting other trades)
                var shipyardWindow = UnityEngine.Object.FindObjectOfType<ShipyardBuildNewWindow>();
                if (shipyardWindow == null || !shipyardWindow.gameObject.activeInHierarchy)
                {
                    SendMelonLoggerMessage("Warning: Attempting to use Express Shipyard logic outside of the shipyard window. Defaulting to vanilla behavior.");
                    return true; // Safety fallback: if we aren't in the shipyard, don't use Express logic
                }

                // 4. Get GameState (same way we did in the UI patch)
                var gameState = Traverse.Create(__instance).Field("gameState").GetValue<GameState>();
                if (gameState == null) gameState = UnityEngine.Object.FindObjectOfType<GameState>();
                if (gameState == null) return true; // Fallback to original if we can't find state

                int totalExpressCost = 0;

                // 5. Calculate the cost exactly like the UI does
                foreach (string goodId in goodsToTransact)
                {
                    int required = getRequiredAmount(goodId);
                    if (gameState.corePrices.TryGetValue(goodId, out float basePrice))
                    {
                        // Multiply by 4 for the Express fee
                        totalExpressCost += (int)(required * basePrice * 4);
                    }
                }

                // 6. Set the __result (this is the value the game will subtract from the balance)
                __result = totalExpressCost;

                SendMelonLoggerMessage($"Processed Express Shipyard Purchase. Total Cost: {totalExpressCost}. Goods: {string.Join(", ", goodsToTransact)}");

                // 7. Return false to skip the original code (which looks for items in the warehouse)
                return false;
            }
        }

        [HarmonyPatch(typeof(GeneralSettingsWindow), nameof(GeneralSettingsWindow.Show))]
        public static class GeneralSettings_UI_Injection_Patch
        {
            public static void Postfix(GeneralSettingsWindow __instance)
            {
                var languageDropdown = Traverse.Create(__instance).Field("language").GetValue<TMP_Dropdown>();
                if (languageDropdown == null) return;

                Transform container = languageDropdown.transform.parent.parent;
                Transform templateRow = languageDropdown.transform.parent;

                if (container.Find("mod_shipyard_buy_all_row") != null) return;

                GameObject newRow = UnityEngine.Object.Instantiate(templateRow.gameObject, container);
                newRow.name = "mod_shipyard_buy_all_row";

                // --- THE FIX: Maintain Position ---
                //int templateIndex = templateRow.GetSiblingIndex();
                //newRow.transform.SetSiblingIndex(templateIndex + 9);

                var label = newRow.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null) label.text = "Express Shipyard (4x Cost)";

                var dropdown = newRow.GetComponentInChildren<TMP_Dropdown>();
                if (dropdown != null)
                {
                    dropdown.onValueChanged = new TMP_Dropdown.DropdownEvent();
                    dropdown.options.Clear();
                    dropdown.options.Add(new TMP_Dropdown.OptionData { text = "Off (Manual)" });
                    dropdown.options.Add(new TMP_Dropdown.OptionData { text = "On (Automatic Buy)" });

                    int currentVal = PlayerPrefs.GetInt("mod.shipyard.buy_all", 0);
                    dropdown.SetValueWithoutNotify(currentVal);

                    dropdown.onValueChanged.AddListener((int val) =>
                    {
                        PlayerPrefs.SetInt("mod.shipyard.buy_all", val);
                        SendMelonLoggerMessage($"Express Shipyard set to {(val == 1 ? "ON" : "OFF")}");
                    });
                }

                // Force Unity to recalculate the layout immediately
                LayoutRebuilder.ForceRebuildLayoutImmediate(container.GetComponent<RectTransform>());
            }
        }

        [HarmonyPatch(typeof(ShipyardBuildNewWindow), nameof(ShipyardBuildNewWindow.Show))]
        public static class Shipyard_Refresh_On_Show
        {
            public static void Postfix(ShipyardBuildNewWindow __instance)
            {
                // Forces the UI to recalculate prices based on the current PlayerPrefs
                Traverse.Create(__instance).Method("SetupUi").GetValue();
            }
        }

        [HarmonyPatch(typeof(ShipyardBuildNewWindow), "GetTooltipIdForBuildButton")]
        public static class Shipyard_Tooltip_Patch
        {
            public static void Postfix(ref string __result)
            {
                // If the mod is on and there are no other errors (like level requirements),
                // we can change the tooltip to explain the price.
                if (PlayerPrefs.GetInt("mod.shipyard.buy_all", 0) == 1 && string.IsNullOrEmpty(__result))
                {
                    // Note: This requires the game to have a localization key for this string, 
                    // or you can just return a raw string if the TMP supports it.
                    // For now, we'll just log it or you can keep the default empty tooltip.
                }
            }
        }

        [HarmonyPatch(typeof(ShipyardBuildNewWindow), "SetupGoods")]
        public static class Shipyard_UI_Force_Patch
        {
            public static void Postfix(ShipyardBuildNewWindow __instance, BuildShipScriptableObject shipDetails)
            {
                if (PlayerPrefs.GetInt("mod.shipyard.buy_all", 0) == 0) return;

                var traverse = Traverse.Create(__instance);

                // --- NEW STRATEGY ---
                // 1. Try to find gameState on the instance
                var gameState = traverse.Field("gameState").GetValue<GameState>();

                // 2. If it's null, let's try to find it on the parent window 
                // (Since your path shows details are nested)
                if (gameState == null)
                {
                    // Many mods use Singleton access if the instance field fails
                    gameState = GameObject.FindObjectOfType<zip.lexy.tgame.state.GameState>();
                }

                // UI Text Fields - Note: Use the exact names from your Inspector
                var materialsCostText = traverse.Field("materialsCost").GetValue<TMP_Text>();
                var totalCostText = traverse.Field("totalCost").GetValue<TMP_Text>();

                // Button and Goods list
                object buildButtonObj = traverse.Field("buildButton").GetValue();
                var goods = traverse.Field("goods").GetValue<List<string>>();

                if (gameState == null || materialsCostText == null || buildButtonObj == null)
                {
                    // Debugging log to see exactly which one is null
                    MelonLoader.MelonLogger.Error($"Mod Debug: GS:{gameState != null} | Mat:{materialsCostText != null} | Btn:{buildButtonObj != null}");
                    return;
                }

                // 1. Calculate the "Express" Cost (4x Base Price)
                int expressMaterialsCost = 0;
                foreach (string goodId in goods)
                {
                    if (gameState.corePrices.TryGetValue(goodId, out float basePrice))
                    {
                        int qty = shipDetails.GetCostByGoodType(goodId);
                        expressMaterialsCost += (int)(qty * basePrice * 4);
                    }
                }

                int totalExpressCost = shipDetails.laborCost + expressMaterialsCost;

                // 2. Overwrite the UI Text
                materialsCostText.text = expressMaterialsCost.ToString();
                totalCostText.text = totalExpressCost.ToString();

                // 3. Logic for the Build Button
                bool canAfford = totalExpressCost <= gameState.human.balance;
                bool validContracts = (bool)traverse.Method("ValidatesContractNumberRequirements").GetValue();
                bool validPortLevel = (bool)traverse.Method("ValidatesPortLevelRequirements").GetValue();

                // Use Traverse to set the 'interactable' property of the LButton 
                // This bypasses the need for the lui-assembly reference
                Traverse.Create(buildButtonObj).Property("interactable").SetValue(validContracts && validPortLevel && canAfford);

                // 4. Update the Tooltip for feedback
                var tooltip = traverse.Field("tooltipTrigger").GetValue<zip.lexy.tgame.ui.tooltip.AreaTooltipTrigger>();
                if (tooltip != null)
                {
                    if (!canAfford)
                        tooltip.tooltipId = "insufficient-balance";
                    else if (!validPortLevel)
                        tooltip.tooltipId = "shipyard-level-low"; // Example ID, check game for real one
                }
            }
        }

        static void SendMelonLoggerMessage(string _message)
        {
            MelonLogger.Msg($"[Shipyard Economy Mod] {_message}");
        }
    }
}