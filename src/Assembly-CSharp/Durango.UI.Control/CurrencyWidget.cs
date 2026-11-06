using Durango.Logic.Clusters;
using UnityEngine;

namespace Durango.UI.Control;

[ExecuteInEditMode]
public class CurrencyWidget : CurrencyWidgetBase
{
	public bool IsBuilt => _component != null;

	protected override bool MakeComponent()
	{
		if (GameManager.ClusterMode != Mode.Online)
		{
			return false;
		}
		return Build();
	}

	public bool EnsureBuilt()
	{
		if (!Application.isPlaying)
		{
			return false;
		}
		if (!base.gameObject.activeSelf)
		{
			base.gameObject.SetActive(value: true);
		}
		return Build();
	}

	private bool Build()
	{
		if (_presetPrefab == null)
		{
			return false;
		}
		if (_component == null)
		{
			_component = Object.Instantiate(_presetPrefab.gameObject, base.transform).GetComponent<PresetCurrencyWidget>();
			_component.Init();
			_component.HideExtraButton(_hideExtraButton);
		}
		Refresh();
		return true;
	}
}
