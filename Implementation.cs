﻿using MelonLoader;
using HarmonyLib;
using UnityEngine;
using Il2Cpp;
using Il2CppInterop;
using Il2CppInterop.Runtime.Injection;
using Il2CppTLD.Gear;
using Il2CppTLD.IntBackedUnit;

namespace DrawFromDrawer
{
	public class DrawFromDrawer : MelonMod
	{
		public override void OnInitializeMelon()
		{           
			MelonLogger.Msg("Mod started!");
        }
    }

	public class DrawerPatches
	{
		public static Container? associatedContainerA;
		public static Container? associatedContainerB;
		public static Inventory? combinedInventory;

		[HarmonyPatch(typeof(Panel_Crafting), nameof(Panel_Crafting.Enable), new[] { typeof(bool), typeof(bool) })]
		internal class PanelCraftingEnablePatch
		{
			static void Prefix(bool enable, bool fromPanel, Panel_Crafting __instance)
			{
				if (!enable)
				{
					associatedContainerA = null;
					associatedContainerB = null;
					combinedInventory = null;
				}
			}
		}

        [HarmonyPatch(typeof(Panel_Crafting), nameof(Panel_Crafting.RefreshSelectedBlueprint))]
		internal class PanelCraftingRefreshSelectedBlueprintPatch
		{
			static void Postfix(Panel_Crafting __instance)
			{
                if (associatedContainerA is null) return;
				CraftingRequirementQuantitySelect crqs = __instance.m_RequirementContainer.m_QuantitySelect;

				BlueprintData bd = __instance.SelectedBPI;
				List<int> maximums = new List<int>();

                Inventory playerInventory = GameObject.Find("SCRIPT_PlayerSystems").GetComponent<Inventory>();

                foreach (BlueprintData.RequiredGearItem rgi in bd.m_RequiredGear)
				{
					int inInventory = playerInventory.GetNumGearWithName(rgi.m_Item.name);
					int inContainer = associatedContainerA.GetNumGearWithName(rgi.m_Item.name);
					int inContainerB = associatedContainerB is not null ? associatedContainerB.GetNumGearWithName(rgi.m_Item.name) : 0;
					int total = inInventory + inContainer + inContainerB;
					int maximum = (int)Math.Floor((double)total / (double)rgi.m_Count);
                    maximums.Add(maximum);
				}

				foreach (BlueprintData.RequiredLiquid rl in bd.m_RequiredLiquid)
				{
					ItemLiquidVolume inInventory = playerInventory.GetTotalLiquidVolume(rl.m_Liquid);
                    ItemLiquidVolume inContainer = associatedContainerA.GetLiquidItemAmount(rl.m_Liquid);
                    ItemLiquidVolume inContainerB = associatedContainerB is not null ? associatedContainerB.GetLiquidItemAmount(rl.m_Liquid) : ItemLiquidVolume.Zero;
					ItemLiquidVolume total = inInventory + inContainer + inContainerB;
					int maximum = (int)Math.Floor(total / rl.m_Volume);
					maximums.Add(maximum);
				}

				foreach (BlueprintData.RequiredPowder rp in bd.m_RequiredPowder)
				{
					ItemWeight inInventory = playerInventory.GetTotalPowderWeight(rp.m_Powder);
					ItemWeight inContainer = associatedContainerA.GetPowderItemAmount(rp.m_Powder);
					ItemWeight inContainerB = associatedContainerB is not null ? associatedContainerB.GetPowderItemAmount(rp.m_Powder) : ItemWeight.Zero;
					ItemWeight total = inInventory + inContainer + inContainerB;
					int maximum = (int)Math.Floor(total / rp.m_Quantity);
					maximums.Add(maximum);
				}

				crqs.m_Maximum = maximums.Min();
            }
		}

        [HarmonyPatch(typeof(WorkBench), nameof(WorkBench.InteractWithWorkbench))]
		internal class WorkBenchInteractWithWorkbenchPatch
		{
			static void Prefix(WorkBench __instance)
			{
				GameObject wbgo = __instance.gameObject;
				associatedContainerA = wbgo.GetComponentInChildren<Container>();

				Inventory playerInventory = GameObject.Find("SCRIPT_PlayerSystems").GetComponent<Inventory>();

				combinedInventory = new Inventory();
				foreach (GearItemObject gi in playerInventory.m_Items)
				{
					combinedInventory.m_Items.Add(gi);
				}
				foreach (GearItemObject gi in associatedContainerA.m_Items)
				{
					combinedInventory.m_Items.Add(gi);
				}
			}
		}

		[HarmonyPatch(typeof(AmmoWorkBench), nameof(AmmoWorkBench.InteractWithWorkbench))]
		internal class AmmoWorkBenchInteractWithWorkBenchPatch
		{
			static void Prefix(AmmoWorkBench __instance)
			{
                GameObject wbgo = __instance.gameObject;
				Container[] containers = wbgo.transform.parent.gameObject.GetComponentsInChildren<Container>();
				associatedContainerA = containers[0];
				associatedContainerB = containers[1];

                Inventory playerInventory = GameObject.Find("SCRIPT_PlayerSystems").GetComponent<Inventory>();

                combinedInventory = new Inventory();
				foreach (GearItemObject gi in playerInventory.m_Items)
				{
                    combinedInventory.m_Items.Add(gi);
                }
                foreach (GearItemObject gi in associatedContainerA.m_Items)
                {
                    combinedInventory.m_Items.Add(gi);
                }
                foreach (GearItemObject gi in associatedContainerB.m_Items)
                {
                    combinedInventory.m_Items.Add(gi);
                }
            }
		}

		[HarmonyPatch(typeof(BlueprintData), nameof(BlueprintData.CanCraftBlueprint))]
		internal class BlueprintDataCanCraftBlueprintPatch
		{
			static void Prefix(ref Inventory inventory)
			{
				if (combinedInventory is null) return;
				inventory = combinedInventory;
			}
		}

		[HarmonyPatch(typeof(BlueprintData), nameof(BlueprintData.HasRequiredMaterials))]
		internal class BlueprintDataHasRequiredMaterialsPatch
		{
			static void Prefix(ref Inventory inventory)
			{
				if (combinedInventory is null) return;
				inventory = combinedInventory;
			}
		}

		[HarmonyPatch(typeof(Inventory), nameof(Inventory.RemoveGearFromInventory))]
		internal class InventoryRemoveGearFromInventoryPatch
		{
			static void Prefix(string gearName, int numUnits, Inventory __instance)
			{
				if (associatedContainerA is null) return;
				int gearInInventory = __instance.GetNumGearWithName(gearName);
                int missing = numUnits - gearInInventory;
				if (missing <= 0) return;

				while (missing > 0)
				{
                    Il2CppSystem.Collections.Generic.List<GearItem> itemsA = new Il2CppSystem.Collections.Generic.List<GearItem>();
                    associatedContainerA.GetItems(gearName, itemsA);
                    Il2CppSystem.Collections.Generic.List<GearItem> itemsB = new Il2CppSystem.Collections.Generic.List<GearItem>();
                    if (associatedContainerB is not null) associatedContainerB.GetItems(gearName, itemsB);

                    if (itemsA.Count == 0 && itemsB.Count == 0) break;
					GearItem gi = itemsA.Count > 0 ? itemsA[itemsA.Count - 1] : itemsB[itemsB.Count - 1];
					if (gi is null) break;

					int numToRemove = gi.m_StackableItem is not null ? Math.Min(missing, gi.m_StackableItem.m_Units) : 1;

					if (gi.m_StackableItem is null || numToRemove == gi.m_StackableItem.m_Units)
					{
						if (itemsA.Count > 0) associatedContainerA.RemoveGear(gi);
						else if (associatedContainerB is not null) associatedContainerB.RemoveGear(gi);
					}
					else gi.m_StackableItem.m_Units -= numToRemove;
					missing -= numToRemove;
				}
			}
		}

		[HarmonyPatch(typeof(CraftingRequirementMaterial), nameof(CraftingRequirementMaterial.Enable))]
		internal class CraftingRequirementMaterialEnablePatch
		{
			static void Postfix(CraftingRequirementMaterial __instance, Panel_Crafting panel, BlueprintData bp, int requiredIndex, int quantity)
			{
				if (associatedContainerA is null) return;
                Inventory playerInventory = GameObject.Find("SCRIPT_PlayerSystems").GetComponent<Inventory>();
				BlueprintData.RequiredGearItem rgi = bp.m_RequiredGear[requiredIndex];
                int inInventory = playerInventory.GetNumGearWithName(rgi.m_Item.name);
                int inContainer = associatedContainerA.GetNumGearWithName(rgi.m_Item.name);
                int inContainerB = associatedContainerB is not null ? associatedContainerB.GetNumGearWithName(rgi.m_Item.name) : 0;
                int total = inInventory + inContainer + inContainerB;
				int requiredTotal = rgi.m_Count * quantity;

				string tail = __instance.m_Display.mText.Split("/")[1];
				__instance.m_Display.mText = total + "/" + tail;
				__instance.m_Display.MarkAsChanged();

				__instance.ApplyTints(total >= requiredTotal, panel.m_RequirementsNotMetTint);
            }
		}

        [HarmonyPatch(typeof(CraftingRequirementMaterial), nameof(CraftingRequirementMaterial.EnableForLiquid))]
        internal class CraftingRequirementMaterialEnableForLiquidPatch
        {
            static void Postfix(CraftingRequirementMaterial __instance, Panel_Crafting panel, BlueprintData bp, int liquidIndex, int quantity)
            {
                if (associatedContainerA is null) return;
                Inventory playerInventory = GameObject.Find("SCRIPT_PlayerSystems").GetComponent<Inventory>();
                BlueprintData.RequiredLiquid rl = bp.m_RequiredLiquid[liquidIndex];
                ItemLiquidVolume inInventory = playerInventory.GetTotalLiquidVolume(rl.m_Liquid);
                ItemLiquidVolume inContainer = associatedContainerA.GetLiquidItemAmount(rl.m_Liquid);
                ItemLiquidVolume inContainerB = associatedContainerB is not null ? associatedContainerB.GetLiquidItemAmount(rl.m_Liquid) : ItemLiquidVolume.Zero;
                ItemLiquidVolume total = inInventory + inContainer + inContainerB;
                ItemLiquidVolume requiredTotal = rl.m_Volume * quantity;

                string tail = __instance.m_Display.mText.Split("/")[1];
                __instance.m_Display.mText = total.ToString() + "/" + tail;
                __instance.m_Display.MarkAsChanged();

                __instance.ApplyTints(total >= requiredTotal, panel.m_RequirementsNotMetTint);
            }
        }

        [HarmonyPatch(typeof(CraftingRequirementMaterial), nameof(CraftingRequirementMaterial.EnableForPowder))]
        internal class CraftingRequirementMaterialEnableForPowderPatch
        {
            static void Postfix(CraftingRequirementMaterial __instance, Panel_Crafting panel, BlueprintData bp, int powderIndex, int quantity)
            {
                if (associatedContainerA is null) return;
                Inventory playerInventory = GameObject.Find("SCRIPT_PlayerSystems").GetComponent<Inventory>();
                BlueprintData.RequiredPowder rp = bp.m_RequiredPowder[powderIndex];
                ItemWeight inInventory = playerInventory.GetTotalPowderWeight(rp.m_Powder);
                ItemWeight inContainer = associatedContainerA.GetPowderItemAmount(rp.m_Powder);
                ItemWeight inContainerB = associatedContainerB is not null ? associatedContainerB.GetPowderItemAmount(rp.m_Powder) : ItemWeight.Zero;
                ItemWeight total = inInventory + inContainer + inContainerB;
                ItemWeight requiredTotal = rp.m_Quantity * quantity;

                string tail = __instance.m_Display.mText.Split("/")[1];
                __instance.m_Display.mText = total.ToFormattedString() + "/" + tail;
                __instance.m_Display.MarkAsChanged();

                __instance.ApplyTints(total >= requiredTotal, panel.m_RequirementsNotMetTint);
            }
        }

		[HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.GetTotalLiters))]
		internal class PlayerManagerGetTotalLitersPatch
		{
			static ItemLiquidVolume Postfix(ItemLiquidVolume result, PlayerManager __instance, LiquidType liquidType)
			{
				if (associatedContainerA is null) return result;

                Inventory playerInventory = GameObject.Find("SCRIPT_PlayerSystems").GetComponent<Inventory>();
                ItemLiquidVolume inInventory = playerInventory.GetTotalLiquidVolume(liquidType);
                ItemLiquidVolume inContainer = associatedContainerA.GetLiquidItemAmount(liquidType);
                ItemLiquidVolume inContainerB = associatedContainerB is not null ? associatedContainerB.GetLiquidItemAmount(liquidType) : ItemLiquidVolume.Zero;
                ItemLiquidVolume total = inInventory + inContainer + inContainerB;
				return total;
            }
		}

        [HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.GetTotalPowderWeight))]
        internal class PlayerManagerGetTotalPowderWeightPatch
        {
            static ItemWeight Postfix(ItemWeight result, PlayerManager __instance, PowderType type)
            {
                if (associatedContainerA is null) return result;

                Inventory playerInventory = GameObject.Find("SCRIPT_PlayerSystems").GetComponent<Inventory>();
                ItemWeight inInventory = playerInventory.GetTotalPowderWeight(type);
                ItemWeight inContainer = associatedContainerA.GetPowderItemAmount(type);
                ItemWeight inContainerB = associatedContainerB is not null ? associatedContainerB.GetPowderItemAmount(type) : ItemWeight.Zero;
                ItemWeight total = inInventory + inContainer + inContainerB;
                return total;
            }
        }

		[HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.DeductLiquidFromInventory))]
		internal class PlayerManagerDeductLiquidFromInventoryPatch
		{
			static void Prefix(PlayerManager __instance, ItemLiquidVolume literDeduction, LiquidType liquidType)
			{
				if (associatedContainerA is null) return;

				Inventory playerInventory = __instance.gameObject.GetComponent<Inventory>();
				ItemLiquidVolume inInventory = playerInventory.GetTotalLiquidVolume(liquidType);
				ItemLiquidVolume missing = literDeduction - inInventory;
				if (missing <= ItemLiquidVolume.Zero) return;

				foreach (GearItemObject gio in associatedContainerA.m_Items)
				{
					if (missing <= ItemLiquidVolume.Zero) return;
					GearItem gi = gio.m_GearItem;
					if (gi.m_LiquidItem is null || gi.m_LiquidItem.LiquidType != liquidType) continue;
					ItemLiquidVolume numToRemove = missing > gi.m_LiquidItem.m_Liquid ? gi.m_LiquidItem.m_Liquid : missing;
					gi.m_LiquidItem.m_Liquid -= numToRemove;
					missing -= numToRemove;
				}

				if (associatedContainerB is null) return;

				foreach (GearItemObject gio in associatedContainerB.m_Items)
				{
					if (missing <= ItemLiquidVolume.Zero) return;
					GearItem gi = gio.m_GearItem;
					if (gi.m_LiquidItem is null || gi.m_LiquidItem.LiquidType != liquidType) continue;
					ItemLiquidVolume numToRemove = missing > gi.m_LiquidItem.m_Liquid ? gi.m_LiquidItem.m_Liquid : missing;
                    gi.m_LiquidItem.m_Liquid -= numToRemove;
					missing -= numToRemove;
				}
			}
		}

        [HarmonyPatch(typeof(PlayerManager), nameof(PlayerManager.DeductPowderFromInventory))]
        internal class PlayerManagerDeductPowderFromInventoryPatch
        {
            static void Prefix(PlayerManager __instance, ItemWeight deduction, PowderType type)
            {
                if (associatedContainerA is null) return;

                Inventory playerInventory = __instance.gameObject.GetComponent<Inventory>();
                ItemWeight inInventory = playerInventory.GetTotalPowderWeight(type);
                ItemWeight missing = deduction - inInventory;
                if (missing <= ItemWeight.Zero) return;

				List<GearItem> killList = new List<GearItem>();

                foreach (GearItemObject gio in associatedContainerA.m_Items)
                {
                    if (missing <= ItemWeight.Zero) return;
                    GearItem gi = gio.m_GearItem;
                    if (gi.m_PowderItem is null || gi.m_PowderItem.m_Type != type) continue;
                    ItemWeight numToRemove = missing >= gi.m_PowderItem.m_Weight ? gi.m_PowderItem.m_Weight : missing;
                    gi.m_PowderItem.m_Weight -= numToRemove;
					if (gi.m_PowderItem.m_Weight <= ItemWeight.Zero) killList.Add(gi);
                    missing -= numToRemove;
                }

				foreach (GearItem gi in killList) associatedContainerA.DestroyGear(gi);

                if (associatedContainerB is null) return;

				killList = new List<GearItem>();

                foreach (GearItemObject gio in associatedContainerB.m_Items)
                {
                    if (missing <= ItemWeight.Zero) return;
                    GearItem gi = gio.m_GearItem;
                    if (gi.m_PowderItem is null || gi.m_PowderItem.m_Type != type) continue;
                    ItemWeight numToRemove = missing >= gi.m_PowderItem.m_Weight ? gi.m_PowderItem.m_Weight : missing;
                    gi.m_PowderItem.m_Weight -= numToRemove;
                    if (gi.m_PowderItem.m_Weight <= ItemWeight.Zero) killList.Add(gi);
                    missing -= numToRemove;
                }

                foreach (GearItem gi in killList) associatedContainerB.DestroyGear(gi);
            }
        }
    }
}
