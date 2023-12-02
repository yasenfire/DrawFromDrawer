using MelonLoader;
using HarmonyLib;
using UnityEngine;
using Il2Cpp;
using Il2CppInterop;
using Il2CppInterop.Runtime.Injection;
using Il2CppTLD.Gear;
using static Il2CppNodeCanvas.Tasks.Actions.Action_ShowPanel;

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
		public static Container? associatedContainer;
		public static Inventory? combinedInventory;

		[HarmonyPatch(typeof(Panel_Crafting), nameof(Panel_Crafting.Enable), new[] { typeof(bool), typeof(bool) })]
		internal class PanelCraftingEnablePatch
		{
			static void Prefix(bool enable, bool fromPanel, Panel_Crafting __instance)
			{
				if (!enable)
				{
					associatedContainer = null;
					combinedInventory = null;
				}
			}
		}

        [HarmonyPatch(typeof(Panel_Crafting), nameof(Panel_Crafting.RefreshSelectedBlueprint))]
		internal class PanelCraftingRefreshSelectedBlueprintPatch
		{
			static void Postfix(Panel_Crafting __instance)
			{
                if (associatedContainer is null) return;
				CraftingRequirementQuantitySelect crqs = __instance.m_RequirementContainer.m_QuantitySelect;

				BlueprintData bd = __instance.m_FilteredBlueprints[__instance.m_CurrentBlueprintIndex];
				List<int> maximums = new List<int>();

                Inventory playerInventory = GameObject.Find("SCRIPT_PlayerSystems").GetComponent<Inventory>();

                foreach (BlueprintData.RequiredGearItem rgi in bd.m_RequiredGear)
				{
					int inInventory = playerInventory.GetNumGearWithName(rgi.m_Item.name);
					int inContainer = associatedContainer.GetNumGearWithName(rgi.m_Item.name);
					int total = inInventory + inContainer;
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
				associatedContainer = wbgo.GetComponentInChildren<Container>();

				Inventory playerInventory = GameObject.Find("SCRIPT_PlayerSystems").GetComponent<Inventory>();

				combinedInventory = new Inventory();
				foreach (GearItemObject gi in playerInventory.m_Items)
				{
					combinedInventory.m_Items.Add(gi);
				}
				foreach (GearItemObject gi in associatedContainer.m_Items)
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
				if (associatedContainer is null) return;
				int gearInInventory = __instance.GetNumGearWithName(gearName);
				int missing = numUnits - gearInInventory;
				if (missing <= 0) return;

				while (missing > 0)
				{
					GearItem gi = associatedContainer.GetClosestMatchStackable(gearName, 0f);
					if (gi is null) break;
                    int numToRemove = Math.Min(missing, gi.m_StackableItem.m_Units);
					if (numToRemove == gi.m_StackableItem.m_Units) associatedContainer.RemoveGear(gi);
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
				if (associatedContainer is null) return;
                Inventory playerInventory = GameObject.Find("SCRIPT_PlayerSystems").GetComponent<Inventory>();
				BlueprintData.RequiredGearItem rgi = bp.m_RequiredGear[requiredIndex];
                int inInventory = playerInventory.GetNumGearWithName(rgi.m_Item.name);
                int inContainer = associatedContainer.GetNumGearWithName(rgi.m_Item.name);
				int total = inInventory + inContainer;
				int requiredTotal = rgi.m_Count * quantity;

				string tail = __instance.m_Display.mText.Split('/')[1];
				__instance.m_Display.mText = total + "/" + tail;
				__instance.m_Display.MarkAsChanged();

				__instance.ApplyTints(total >= requiredTotal, panel.m_RequirementsNotMetTint);
            }
		}
	}
}
