using System;
using Durango.Logic;
using Durango.Logic.Clusters;
using Durango.Logic.Notification;
using Durango.Network;
using Durango.UI.Control;
using Durango.Utils;
using Durango.Utils.Extensions;
using L10N;
using Messages;
using NestedPrefab;
using Community;
using Community.Rift;
using Shared.Rank;
using Shared.Season2;
using UnityEngine;
using Yaml;
using Yaml.Util;

namespace Durango.UI;

[Uri("WarpRush")]
public class WarpRushGroup : UIBase, IUIInitializable, INotificationable
{
	public enum Tab
	{
		Invalid = -1,
		[T.EnumName("탐사대")]
		Lobby,
		[T.EnumName("워프 스톤 교환")]
		Reward,
		[T.EnumName("랭킹")]
		Ranking
	}

	[SerializeField]
	private UITitle _titleWidget;

	[SerializeField]
	private NestedPrefabLinker _tabLinker;

	[SerializeField]
	[EnumList(typeof(Tab), false, 0, -1)]
	private NestedPrefabLinker[] _contents;

	[SerializeField]
	private CurrencyWidgetBase[] _currencyWidgets;

	private IconTabList _tabList;

	private int _selectedTab = -1;

	private int _seasonShopTab = -1;

	private readonly Toggle _notification = new Toggle(Durango.Logic.Notification.Type.Important);

	public static bool SeasonPassMode;

	public Notification Notification => _notification;

	void IUIInitializable.Init()
	{
		_openCloseSound = UISound.GroupType.Default;
		_tabList = _tabLinker.Object.GetComponent<IconTabList>();
		_tabList.Clicked += OnClickTab;
		_tabList.BeginLoad();
		for (int i = 0; i < _contents.Length; i++)
		{
			Tab tab = (Tab)i;
			if (tab != Tab.Ranking || OptionSystem.IsWarpRushRankingEnabled())
			{
				_tabList.Add(IconMap.Get(tab), tab.GetName());
			}
		}
		IconTabWidget iconTabWidget = _tabList.Add("money_coin", RiftText.T("ร\u0e49านค\u0e49า K", "K's shop"));
		_tabList.EndLoad();
		_seasonShopTab = _tabList.ScrollView.Nodes.IndexOf(iconTabWidget.gameObject);
		_titleWidget.Object.SetTitle(T._("워프 러시"));
		_currencyWidgets[0].SetWarpRushResource(ResourceType.BravoStone, total: true);
		_currencyWidgets[1].SetWarpRushResource(ResourceType.AlphaStone, total: true);
		_currencyWidgets[2].SetVoucherType(Yaml.Util.Singleton<Constants>.Instance.Season2.Voucher.Id);
		GameSystem<WarpRushSystem>.Instance().SurvivorRegionChanged += delegate(ResourceType resourceType)
		{
			UIManager.Alarm.ShowNotify(T._("{0} 크레이터 한 곳이 워프됐습니다.", WarpRushSystem.GetResourceName(resourceType)), "act_warpback", major: false);
		};
		GameSystem<WarpRushSystem>.Instance().RewardStatusChanged += OnRewardStatusChange;
		CommunitySeasonPass.Updated += delegate
		{
			if (SeasonPassMode && base.IsOpened)
			{
				if (CommunitySeasonPass.ShowSeasonUi)
				{
					ApplySeasonPassMode();
				}
				else
				{
					Close();
				}
			}
			if (CommunityLink.Active)
			{
				_notification.On = CommunitySeasonPass.ClaimableCount > 0;
			}
		};
		GameSystem<WarpRushSystem>.Instance().RewardedRankingUpdated += delegate
		{
			bool hasNotification = false;
			DateTime utcNow = Times.UnixTimeToDateTimeUtc(Connections.Frontend.GetPredictedServerTime());
			foreach (Category ranking in Yaml.Util.Singleton<Constants>.Instance.Season2.Rankings)
			{
				if (!string.IsNullOrEmpty(SingletonDict<Category, Ranking>.Instance.Get(ranking)?.GetCurrentAndPrevRevisionId(utcNow).Value) && GameSystem<WarpRushSystem>.Instance().AnyRewardLeft(ranking))
				{
					hasNotification = true;
					break;
				}
			}
			RefreshNotification(hasNotification);
		};
		SetChildrenActive(activated: false);
	}

	protected override bool TryOpen()
	{
		if (SeasonPassMode && !CommunitySeasonPass.ShowSeasonUi)
		{
			return false;
		}
		ApplySeasonPassMode();
		if (SeasonPassMode)
		{
			SelectTab(1);
		}
		else if (_selectedTab == -1)
		{
			SelectTab(0);
		}
		return base.TryOpen();
	}

	private void ApplySeasonPassMode()
	{
		bool seasonPassMode = SeasonPassMode;
		_titleWidget.Object.SetTitle((seasonPassMode && SeasonLevelPage.Supports(CommunitySeasonPass.Current)) ? RiftText.T("ซ\u0e35ซ\u0e31\u0e48น", "Season") : ((!seasonPassMode) ? T._("워프 러시") : (CommunitySeasonPass.HasSeasonLevel ? CommunitySeasonPass.SeasonTitle(CommunitySeasonPass.Current) : CommunitySeasonPass.Text("title"))));
		for (int i = 0; i < _contents.Length; i++)
		{
			IconTabWidget iconTabWidget = _tabList.Get(i);
			if (!(iconTabWidget == null))
			{
				if (i != 1)
				{
					iconTabWidget.gameObject.SetActive(!seasonPassMode);
				}
				else
				{
					iconTabWidget.Set(seasonPassMode ? "icon_warprush_reward" : IconMap.Get(Tab.Reward), seasonPassMode ? CommunitySeasonPass.Text("tab_rewards") : Tab.Reward.GetName());
				}
			}
		}
		CurrencyWidgetBase[] currencyWidgets = _currencyWidgets;
		IconTabWidget iconTabWidget2 = _tabList.Get(_seasonShopTab);
		if (iconTabWidget2 != null)
		{
			iconTabWidget2.gameObject.SetActive(seasonPassMode);
		}
		_tabList.ScrollView.UpdateLayout();
		CurrencyWidgetBase[] array = currencyWidgets;
		foreach (CurrencyWidgetBase currencyWidgetBase in array)
		{
			if (currencyWidgetBase != null)
			{
				currencyWidgetBase.gameObject.SetActive(!seasonPassMode);
			}
		}
	}

	private void OnClickTab(int index)
	{
		if (SeasonPassMode && index == _seasonShopTab)
		{
			UIManager.FindScript<ShopGroup>().OpenSeasonShop();
		}
		else if (_selectedTab != index)
		{
			SelectTab(index);
		}
	}

	private void SelectTab(int index)
	{
		_selectedTab = index;
		_tabList.Select(index);
		for (int i = 0; i < _contents.Length; i++)
		{
			_contents[i].gameObject.SetActive(index == i);
		}
	}

	private void OnRewardStatusChange(ResourceType type, S02RewardStatus prev, S02RewardStatus current)
	{
		if (prev.Level != 0 && prev.Level != current.Level)
		{
			UIManager.Alarm.RewardAlarm(new AlarmRewardQueue.Args
			{
				Main = T._("<em>{0:lv:}</em>", current.Level),
				Sub = WarpRushSystem.GetBoxName(type),
				Icon = WarpRushSystem.GetResourceBoxIcon(type)
			}, AlarmGroup.RewardEffectType.WarpRushRewardReceived, 1f);
		}
	}

	public static SyncString GetDateLimitSyncString(double until, string decorator)
	{
		return new SyncString(delegate(out string text, out float period)
		{
			double num = until - Connections.Frontend.GetPredictedServerTime();
			if (num > 86400.0)
			{
				text = string.Format(decorator, T._("{0} 남음", TimedeltaFormatter.Format(num, 1, "day")));
				period = (float)(num % (double)TimedeltaFormatter.CurrentMinUnit());
			}
			else if (num > 0.0)
			{
				text = string.Format(decorator, T._("{0} 남음", TimedeltaFormatter.Format(num, 1, "min")));
				period = (float)(num % (double)TimedeltaFormatter.CurrentMinUnit());
			}
			else
			{
				text = string.Empty;
				period = 0f;
			}
		});
	}

	private void RefreshNotification(bool hasNotification)
	{
		_notification.On = hasNotification;
		_tabList.SetNotification(2, _notification.On, Durango.Logic.Notification.Type.Important);
	}

	protected override void DefaultUri()
	{
		SeasonPassMode = false;
		string argument = UriParser.GetArgument("Tab");
		if (!string.IsNullOrEmpty(argument) && argument.TryEnum<Tab>(out var value))
		{
			SelectTab((int)value);
		}
		Open();
	}
}
