using Crafting;
using Durango.Logic.Item;
using Messages;
using Shared.Item;
using UnityEngine;

public sealed class OffServerDyeBatch : MonoBehaviour
{
	private Artifact workbench;

	private string targetId;

	private string[] plan;

	private int index;

	private bool succeeded;

	private float started;

	private string recipeId;

	private CraftSystem craft;

	public static bool Active { get; private set; }

	public static void Begin(Artifact workbench, string targetId, string[] plan)
	{
		if (!Active)
		{
			Active = true;
			OffServerDyeBatch offServerDyeBatch = new GameObject("OffServerDyeBatch").AddComponent<OffServerDyeBatch>();
			offServerDyeBatch.workbench = workbench;
			offServerDyeBatch.targetId = targetId;
			offServerDyeBatch.plan = plan;
			offServerDyeBatch.craft = GameSystem<CraftSystem>.Instance();
			offServerDyeBatch.craft.CraftSucceed += offServerDyeBatch.Succeeded;
			offServerDyeBatch.craft.CraftFailed += offServerDyeBatch.Failed;
			offServerDyeBatch.Next();
		}
	}

	private void Next()
	{
		while (index < 3 && plan[index] == null)
		{
			index++;
		}
		if (index == 3 || workbench == null)
		{
			Object.Destroy(base.gameObject);
			return;
		}
		Durango.Logic.Item.Inventory playerInventory = GameSystem<InventorySystem>.Instance().PlayerInventory;
		ItemData itemData = playerInventory.Find(targetId);
		ItemData itemData2 = playerInventory.Find(plan[index]);
		if (itemData == null || itemData2 == null)
		{
			Object.Destroy(base.gameObject);
			return;
		}
		ColorChannel channel = (ColorChannel)index;
		RecipeSystem recipeSystem = GameSystem<RecipeSystem>.Instance();
		Recipe recipe = (itemData2.HasTag("decolorizer") ? recipeSystem.GetBleachingRecipe(channel) : recipeSystem.GetDyeingRecipe(channel));
		if (recipe == null)
		{
			Object.Destroy(base.gameObject);
			return;
		}
		recipeId = recipe.Id;
		succeeded = false;
		started = Time.realtimeSinceStartup;
		craft.Dyeing(workbench, itemData, itemData2, channel);
	}

	private void Succeeded(string id, Crafted result)
	{
		if (id == recipeId)
		{
			succeeded = true;
		}
	}

	private void Failed(string id, ActionInfo info)
	{
		if (id == recipeId)
		{
			Object.Destroy(base.gameObject);
		}
	}

	private void Update()
	{
		if (succeeded)
		{
			index++;
			Next();
		}
		else if (Time.realtimeSinceStartup - started > 30f || (Time.realtimeSinceStartup - started > 0.5f && craft.CraftingTimer.Timer.IsStop))
		{
			Object.Destroy(base.gameObject);
		}
	}

	private void OnDestroy()
	{
		if (craft != null)
		{
			craft.CraftSucceed -= Succeeded;
			craft.CraftFailed -= Failed;
		}
		Active = false;
	}
}
