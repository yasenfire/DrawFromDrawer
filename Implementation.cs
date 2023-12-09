using MelonLoader;
using HarmonyLib;
using UnityEngine;
using Il2Cpp;
using Il2CppInterop;
using Il2CppInterop.Runtime.Injection;
using Il2CppTLD.Gear;

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

				BlueprintData bd = __instance.m_FilteredBlueprints[__instance.m_CurrentBlueprintIndex];
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
	}
}
