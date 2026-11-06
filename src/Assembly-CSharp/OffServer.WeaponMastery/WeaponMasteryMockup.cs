using System.Reflection;
using Durango.UI.Popup;

namespace OffServer.WeaponMastery;

public static class WeaponMasteryMockup
{
	private static FarmingEncyclopediaPopup active;

	private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

	public static bool IsOpen
	{
		get
		{
			if (active != null)
			{
				return active.IsVisible;
			}
			return false;
		}
	}

	public static void Close()
	{
		WeaponMasteryPreview.HideTooltip();
		if (active != null)
		{
			active.Hide();
		}
	}

	private static T Field<T>(object owner, string name)
	{
		return (T)owner.GetType().GetField(name, Private).GetValue(owner);
	}

	public static FarmingEncyclopediaPopup Prepare()
	{
		FarmingEncyclopediaPopup farmingEncyclopediaPopup = (active = UIManager.Popup.Tooltip<FarmingEncyclopediaPopup>());
		WeaponMasteryMockupCleanup weaponMasteryMockupCleanup = farmingEncyclopediaPopup.GetComponent<WeaponMasteryMockupCleanup>();
		if (weaponMasteryMockupCleanup == null)
		{
			weaponMasteryMockupCleanup = farmingEncyclopediaPopup.gameObject.AddComponent<WeaponMasteryMockupCleanup>();
		}
		weaponMasteryMockupCleanup.Owner = farmingEncyclopediaPopup;
		if (weaponMasteryMockupCleanup.OriginalTitle == null)
		{
			weaponMasteryMockupCleanup.OriginalTitle = Field<UILabel>(farmingEncyclopediaPopup, "_titleLabel").text;
		}
		farmingEncyclopediaPopup.Set(null);
		farmingEncyclopediaPopup.AutoPosition = false;
		return farmingEncyclopediaPopup;
	}
}
