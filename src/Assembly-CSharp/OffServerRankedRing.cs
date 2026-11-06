using System;
using System.Globalization;
using Durango.Terrain;
using Durango.UI.InGame;
using Durango.Utils;
using Newtonsoft.Json.Linq;
using UnityEngine;

public class OffServerRankedRing : GameSystem<OffServerRankedRing>
{
	public const float TileUnits = 200f;

	public static float Band = 60f;

	public static bool Wave;

	public static string Style = "gta";

	public const string ViewId = "ranked";

	private Vector3 _center;

	private float _outer = -1f;

	private float _band = -1f;

	private bool _wave;

	public static bool Shown { get; private set; }

	public static bool Outside { get; private set; }

	public static double Radius { get; private set; }

	public static Vector2 Center { get; private set; }

	public static double Distance { get; private set; }

	public static double DamageRatio { get; private set; } = -1.0;

	private void Update()
	{
		try
		{
			Tick();
		}
		catch (Exception exception)
		{
			Debug.LogException(exception);
			Outside = false;
			Hide();
		}
	}

	private void Tick()
	{
		JObject match = OffServerRanked.Match;
		JObject jObject = ((match != null) ? (match["ring"] as JObject) : null);
		string phase = OffServerRanked.Phase;
		bool flag = phase == "Live" || phase == "Overtime";
		if (jObject == null || !OffServerRanked.InMatch || OffServerRanked.Result != null || (!flag && !(phase == "Countdown") && !(phase == "Arriving")))
		{
			Outside = false;
			Hide();
			return;
		}
		Radius = RadiusNow(jObject, OffServerRanked.Now);
		Center = new Vector2((float)OffServerRanked.Num(jObject["cx"]), (float)OffServerRanked.Num(jObject["cz"]));
		double? num = OffServerRanked.NumOrNull(jObject["dmg"]);
		DamageRatio = (num.HasValue ? num.Value : (-1.0));
		PlayerBehavior localPlayer = PlayerBehavior.LocalPlayer;
		if (localPlayer != null)
		{
			Distance = (Util.ClientPositionToTilePosition(localPlayer.CurrentPosition) - Center).magnitude;
		}
		Outside = localPlayer != null && flag && !OffServerRanked.Flag(match["out"]) && Radius > 0.0 && Distance > Radius;
		Draw();
	}

	public static double RadiusNow(JObject ring, double now)
	{
		double? num = OffServerRanked.NumOrNull(ring["shrinkFrom"]);
		double? num2 = OffServerRanked.NumOrNull(ring["shrinkTo"]);
		double? num3 = OffServerRanked.NumOrNull(ring["r0"]);
		double? num4 = OffServerRanked.NumOrNull(ring["rMin"]);
		if (num.HasValue && num2.HasValue && num3.HasValue && num4.HasValue)
		{
			double num5 = Math.Max(0.0, Math.Min(1.0, (now - num.Value) / Math.Max(0.01, num2.Value - num.Value)));
			return num3.Value - (num3.Value - num4.Value) * num5;
		}
		return OffServerRanked.Num(ring["r"]);
	}

	private void Draw()
	{
		if (Style == "gta")
		{
			HideCatapult();
			OffServerRingView.Show("ranked", Center, (float)Radius);
			Shown = OffServerRingView.Count > 0;
			return;
		}
		OffServerRingView.Hide("ranked");
		if (!Singleton<CatapultRangeView>.HasInstance() || Radius <= 0.0)
		{
			Hide();
			return;
		}
		Vector3 vector = Util.TilePositionToClientPosition(Center);
		float num = (float)(Radius * 200.0);
		float num2 = Mathf.Clamp(Band, 14f, num);
		CatapultRangeView catapultRangeView = Singleton<CatapultRangeView>.Instance();
		if (_outer < 0f || (vector - _center).sqrMagnitude > 1f || Mathf.Abs(num - _outer) > 1f || num2 != _band)
		{
			catapultRangeView.Show(vector, num - num2, num);
			_center = vector;
			_outer = num;
			_band = num2;
			Shown = true;
		}
		if (_wave != Wave)
		{
			catapultRangeView.ShowWave(Wave);
			_wave = Wave;
		}
	}

	private void Hide()
	{
		OffServerRingView.Hide("ranked");
		HideCatapult();
		Shown = false;
	}

	private void HideCatapult()
	{
		if (_outer >= 0f && Singleton<CatapultRangeView>.HasInstance())
		{
			Singleton<CatapultRangeView>.Instance().Hide();
		}
		_wave = false;
		_outer = -1f;
	}

	public static string Describe()
	{
		return "ring shown " + Shown + " outside " + Outside + " r " + Radius.ToString("0.00") + " centre " + Center.x.ToString("0.0") + "," + Center.y.ToString("0.0") + " dist " + Distance.ToString("0.00") + " dmg " + DamageRatio + " style " + Style + " band " + Band + " wave " + Wave + " view " + Singleton<CatapultRangeView>.HasInstance() + " · " + OffServerRingView.Describe();
	}

	public static string DebugCommand(string[] args)
	{
		string text = ((args.Length > 2) ? args[2].ToLowerInvariant() : string.Empty);
		float num = ((args.Length > 3) ? F(args[3]) : float.NaN);
		float num2 = ((args.Length > 4) ? F(args[4]) : float.NaN);
		switch (text)
		{
		case "style":
			Style = ((args.Length > 3 && args[3] == "catapult") ? "catapult" : "gta");
			break;
		case "wall":
			if (!float.IsNaN(num))
			{
				OffServerRingView.WallHeight = Mathf.Clamp(num, 20f, 2000f);
			}
			if (!float.IsNaN(num2))
			{
				OffServerRingView.WallAlpha = Mathf.Clamp01(num2);
			}
			OffServerRingView.Restyle();
			break;
		case "ground":
			if (!float.IsNaN(num))
			{
				OffServerRingView.GroundBand = Mathf.Clamp(num, 8f, 600f);
			}
			if (!float.IsNaN(num2))
			{
				OffServerRingView.GroundAlpha = Mathf.Clamp01(num2);
			}
			OffServerRingView.Restyle();
			break;
		case "pulse":
			if (!float.IsNaN(num))
			{
				OffServerRingView.Pulse = Mathf.Clamp01(num);
			}
			break;
		case "band":
			if (!float.IsNaN(num))
			{
				Band = Mathf.Clamp(num, 14f, 2000f);
			}
			break;
		case "wave":
			Wave = args.Length > 3 && args[3] == "1";
			break;
		}
		return Describe();
	}

	private static float F(string s)
	{
		if (!float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
		{
			return float.NaN;
		}
		return result;
	}
}
