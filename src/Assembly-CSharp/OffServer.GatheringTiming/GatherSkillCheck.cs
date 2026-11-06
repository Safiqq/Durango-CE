using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Durango.Logic.Timer;
using Durango.Network;
using Durango.Render.Particle;
using Durango.UI;
using Durango.Utils;
using InteractionData;
using Messages;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace OffServer.GatheringTiming;

public sealed class GatherSkillCheck : MonoBehaviour
{
	private static GatherSkillCheck instance;

	private readonly SkillCheckModel model = new SkillCheckModel();

	private GatheringSystem owner;

	private PredictTimer timer;

	private UIPanel panel;

	private SkillCheckGraphic dial;

	private UILabel result;

	private UILabel combo;

	private Transform content;

	private Transform shakeRoot;

	private UIRoot uiRoot;

	private UIWidget tapSurface;

	private BoxCollider tapCollider;

	private GatherSkillCheckEffects effects;

	private float started;

	private float showUntil;

	private float lingerUntil;

	private bool serverShown;

	private const float LingerSeconds = 1f;

	private bool active;

	private bool pointerHeld;

	private bool presented;

	private bool armed;

	private const float Scale = 0.85f;

	private Camera uiCamera;

	private int ticket;

	private static Connection listeningOn;

	private JObject heldStart;

	private float heldAt;

	private const float HoldSeconds = 1.5f;

	public const string Caps = "gather-check";

	private const string GoodParticle = "Particle/FX_MiniGameDanceGood.prefab";

	private const string GreatParticle = "Particle/FX_MiniGameDanceGreat.prefab";

	private double Now => Time.realtimeSinceStartupAsDouble;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void Boot()
	{
		try
		{
			EnsureInstance();
		}
		catch (Exception exception)
		{
			UnityEngine.Debug.LogException(exception);
		}
	}

	private static void EnsureInstance()
	{
		if (!(instance != null))
		{
			GameObject obj = new GameObject("Gathering timing skill check");
			UnityEngine.Object.DontDestroyOnLoad(obj);
			instance = obj.AddComponent<GatherSkillCheck>();
		}
	}

	private static void Listen(Connection conn)
	{
		if (conn != null && conn != listeningOn)
		{
			Diag("register GatherCheckDown on conn#" + Id(conn) + " (was conn#" + Id(listeningOn) + ")");
			conn.On<GatherCheckDown>(OnDown);
			listeningOn = conn;
		}
	}

	public static void Begin(GatheringSystem source, GatheringData data, PredictTimer clock)
	{
		try
		{
			if (data == null || clock == null || clock.Timer.IsStop)
			{
				return;
			}
			if (OffServerHudLayout.Editing)
			{
				Diag("Begin skipped: HUD layout editing");
				return;
			}
			EnsureInstance();
			Listen(Connections.Frontend);
			if (instance.active || instance.lingerUntil > 0f)
			{
				instance.Cancel();
			}
			instance.owner = source;
			instance.timer = clock;
			instance.armed = true;
			JObject jObject = instance.heldStart;
			instance.heldStart = null;
			if (jObject != null && Time.unscaledTime - instance.heldAt <= 1.5f)
			{
				Diag("Begin: replay start that came first");
				instance.StartServerCheck(jObject);
			}
		}
		catch (Exception exception)
		{
			UnityEngine.Debug.LogException(exception);
		}
	}

	private static void OnDown(GatherCheckDown message, PacketHeader header)
	{
		try
		{
			Diag("Down kind=" + message.Kind + " conn#" + Id(Connections.FrontendIfAny) + " listening conn#" + Id(listeningOn) + ((instance == null) ? " · no overlay instance" : ""));
			if (!(instance == null))
			{
				JObject json = JObject.Parse(message.Json ?? "{}");
				if (message.Kind == 0)
				{
					instance.StartServerCheck(json);
				}
				else if (message.Kind == 1)
				{
					instance.ShowServerResult(json);
				}
			}
		}
		catch (Exception exception)
		{
			UnityEngine.Debug.LogException(exception);
		}
	}

	private void StartServerCheck(JObject json)
	{
		if (!armed)
		{
			heldStart = json;
			heldAt = Time.unscaledTime;
			Diag("Start held: came before Begin");
			return;
		}
		if (owner == null || timer == null || timer.Timer.IsStop)
		{
			Diag("Start dropped: owner=" + (owner != null) + " timer=" + (timer != null && !timer.Timer.IsStop));
			return;
		}
		armed = false;
		ticket = Num(json["ticket"], 0, (double x) => (int)x);
		double spinStart = Now + (Num(json["spinAt"], 0.0) - Connections.Frontend.GetPredictedServerTime());
		StartCheck(owner, timer, spinStart, Num(json["target"], 0.0), Num(json["spin"], 1.8), Num(json["good"], 25.0), Num(json["great"], 4.5));
	}

	private static T Num<T>(JToken token, T fallback, Func<double, T> map)
	{
		if (token != null && token.Type != JTokenType.Null)
		{
			return map(token.Value<double>());
		}
		return fallback;
	}

	private static double Num(JToken token, double fallback)
	{
		return Num(token, fallback, (double x) => x);
	}

	private static void Diag(string message)
	{
		UnityEngine.Debug.Log("[OffServer][GC-1] " + message);
	}

	private static int Id(Connection conn)
	{
		if (conn != null)
		{
			return RuntimeHelpers.GetHashCode(conn);
		}
		return 0;
	}

	private void ShowServerResult(JObject json)
	{
		if (Num(json["ticket"], -1, (double x) => (int)x) != ticket || result == null || !presented)
		{
			if (lingerUntil > 0f)
			{
				Cancel();
			}
			return;
		}
		int num = Num(json["result"], 0, (double x) => (int)x);
		result.text = num switch
		{
			1 => "GOOD",
			2 => "GREAT",
			_ => "MISS",
		} + " SKILL CHECK";
		int num2 = Num(json["streak"], 0, (double x) => (int)x);
		bool flag = json["boost"] != null && json["boost"].Type == JTokenType.Boolean && (bool)json["boost"];
		combo.text = ((num2 >= 2) ? ("x" + num2 + " COMBO") : "");
		combo.color = (flag ? PresetColor.UILightGreen : Color.white);
		serverShown = true;
		showUntil = Time.unscaledTime + 1f;
		if (lingerUntil > 0f)
		{
			lingerUntil = showUntil;
		}
		panel.gameObject.SetActive(value: true);
	}

	public static void Stop(GatheringSystem source)
	{
		if (instance != null && instance.owner == source)
		{
			instance.EndGather();
		}
	}

	private void EndGather()
	{
		if (!active)
		{
			return;
		}
		if (!presented)
		{
			Cancel();
			return;
		}
		active = false;
		pointerHeld = false;
		if (tapCollider != null)
		{
			tapCollider.enabled = false;
		}
		lingerUntil = (serverShown ? Mathf.Max(showUntil, Time.unscaledTime) : (Time.unscaledTime + 1f));
	}

	public static bool ConsumeSpace()
	{
		if (instance == null || !instance.active || instance.panel == null || !instance.panel.gameObject.activeInHierarchy)
		{
			return false;
		}
		instance.Hit();
		return true;
	}

	private void Setup()
	{
		UILabel componentInChildren = Resources.Load<GameObject>("popup/ActionTooltip_PC").GetComponentInChildren<UILabel>(includeInactive: true);
		Shader shader = Resources.LoadAll<Shader>("Shaders").First((Shader x) => x.name == "Durango/NGUI/Transparent");
		panel = NGUITools.CreateUI(advanced3D: false);
		panel.name = "Gather Skill Check Overlay";
		UnityEngine.Object.DontDestroyOnLoad(panel.transform.root.gameObject);
		uiRoot = panel.GetComponentInParent<UIRoot>();
		uiRoot.scalingStyle = UIRoot.Scaling.Constrained;
		uiRoot.manualWidth = 960;
		uiRoot.manualHeight = 720;
		uiRoot.fitWidth = (uiRoot.fitHeight = true);
		Camera camera = panel.GetComponentInParent<Camera>();
		if (camera == null)
		{
			camera = panel.transform.root.GetComponentInChildren<Camera>();
		}
		if (camera != null)
		{
			camera.depth = 100f;
			camera.clearFlags = CameraClearFlags.Depth;
		}
		uiCamera = camera;
		panel.depth = 200;
		panel.gameObject.SetActive(value: false);
		GameObject gameObject = new GameObject("SkillCheck screen tap");
		gameObject.layer = panel.gameObject.layer;
		gameObject.transform.SetParent(panel.transform, worldPositionStays: false);
		tapSurface = gameObject.AddComponent<UIWidget>();
		tapSurface.depth = 200;
		tapCollider = gameObject.AddComponent<BoxCollider>();
		tapCollider.isTrigger = true;
		UIEventListener.Get(gameObject).onPress = delegate(GameObject go, bool down)
		{
			pointerHeld = down;
			if (down && active)
			{
				Hit("screen-tap");
			}
		};
		GameObject gameObject2 = new GameObject("Compact skill check — lower right thumb area");
		gameObject2.layer = panel.gameObject.layer;
		content = gameObject2.transform;
		content.SetParent(panel.transform, worldPositionStays: false);
		content.localScale = Vector3.one * 0.85f;
		GameObject gameObject3 = new GameObject("Skill check impact shake");
		gameObject3.layer = panel.gameObject.layer;
		shakeRoot = gameObject3.transform;
		shakeRoot.SetParent(content, worldPositionStays: false);
		GameObject gameObject4 = new GameObject("Skill check circle");
		gameObject4.layer = panel.gameObject.layer;
		gameObject4.transform.SetParent(shakeRoot, worldPositionStays: false);
		dial = gameObject4.AddComponent<SkillCheckGraphic>();
		dial.SharedMaterial = new Material(shader)
		{
			mainTexture = Texture2D.whiteTexture
		};
		dial.width = 500;
		dial.height = 400;
		dial.DrawProgress = false;
		dial.depth = 201;
		Label(componentInChildren, Application.isMobilePlatform ? "TAP" : "SPACE", -26f, 22);
		result = Label(componentInChildren, "", 175f, 27);
		combo = Label(componentInChildren, "", 207f, 22);
		effects = new GatherSkillCheckEffects(content, shakeRoot, dial, result);
		Layout();
		panel.gameObject.SetActive(value: false);
	}

	private UILabel Label(UILabel source, string text, float y, int size)
	{
		GameObject obj = new GameObject("Skill check label");
		obj.layer = panel.gameObject.layer;
		obj.transform.SetParent(shakeRoot, worldPositionStays: false);
		obj.transform.localPosition = new Vector3(0f, y, 0f);
		UILabel uILabel = obj.AddComponent<UILabel>();
		uILabel.trueTypeFont = source.trueTypeFont;
		uILabel.bitmapFont = source.bitmapFont;
		uILabel.width = 600;
		uILabel.height = 50;
		uILabel.fontSize = size;
		uILabel.alignment = NGUIText.Alignment.Center;
		uILabel.depth = 202;
		uILabel.text = text;
		uILabel.effectStyle = UILabel.Effect.Outline;
		uILabel.effectColor = Color.black;
		return uILabel;
	}

	private void StartCheck(GatheringSystem source, PredictTimer clock, double spinStart, double target, double spin, double goodHalf, double greatHalf)
	{
		if (panel == null)
		{
			Diag("Start: building overlay");
			Setup();
		}
		if (effects == null)
		{
			Diag("Start dropped: overlay not built");
			return;
		}
		Diag("Start: dial shown · needle in " + (spinStart - Now).ToString("0.00") + " s");
		owner = source;
		timer = clock;
		active = true;
		pointerHeld = false;
		presented = false;
		serverShown = false;
		lingerUntil = 0f;
		started = Time.unscaledTime;
		tapCollider.enabled = true;
		model.Prepare();
		model.Start(spinStart - 0.65, target, spin, goodHalf, greatHalf);
		panel.gameObject.SetActive(value: true);
		Layout();
		result.text = "GET READY";
		combo.text = "";
		Draw();
		effects.Begin();
	}

	private void Hit(string input = "space")
	{
		if (model.Press(Now))
		{
			PresentResult();
			Connections.Frontend.Send(new GatherCheckUp
			{
				Kind = 0,
				Json = "{\"ticket\":" + ticket + "}"
			});
		}
	}

	private void PresentResult()
	{
		if (!presented)
		{
			presented = true;
			result.text = model.Result.ToString().ToUpperInvariant() + " SKILL CHECK";
			showUntil = Time.unscaledTime + 1f;
			effects.Feedback(model.Result, model.AngleDegrees);
			if (model.Result == CheckResult.Good)
			{
				EmitParticles("Particle/FX_MiniGameDanceGood.prefab");
			}
			else if (model.Result == CheckResult.Great)
			{
				EmitParticles("Particle/FX_MiniGameDanceGreat.prefab");
			}
		}
	}

	private void Layout()
	{
		float num = uiRoot.activeHeight;
		float num2 = num * (float)Screen.width / Math.Max(1f, Screen.height);
		Vector3 vector = Vector3.zero;
		Vector3? vector2 = WheelScreenPoint();
		if (vector2.HasValue && uiCamera != null)
		{
			vector = content.parent.InverseTransformPoint(uiCamera.ScreenToWorldPoint(vector2.Value));
			vector.z = 0f;
		}
		content.localPosition = vector - new Vector3(0f, 29.75f, 0f);
		tapSurface.width = Mathf.CeilToInt(num2 + 4f);
		tapSurface.height = Mathf.CeilToInt(num + 4f);
		tapCollider.size = new Vector3(num2 + 4f, num + 4f, 1f);
	}

	private static void EmitParticles(string particle)
	{
		try
		{
			InteractionGroup interactionGroup = UIManager.FindScript<InteractionGroup>();
			InteractionMenuListWidgetBase interactionMenuListWidgetBase = ((interactionGroup == null) ? null : interactionGroup.InteractionMenu);
			UIRoot uIRoot = Singleton<UIManager>.Instance().UIRoot;
			if (!(uIRoot == null))
			{
				Vector3 pos = ((interactionMenuListWidgetBase != null && interactionMenuListWidgetBase.gameObject.activeInHierarchy) ? interactionMenuListWidgetBase.transform.position : uIRoot.transform.position);
				ParticleManager.Emit(particle, pos, Quaternion.identity, comeForwardToCamera: false, groundDecal: false, uIRoot.transform.lossyScale);
			}
		}
		catch (Exception exception)
		{
			UnityEngine.Debug.LogException(exception);
		}
	}

	private static Vector3? WheelScreenPoint()
	{
		InteractionGroup interactionGroup = UIManager.FindScript<InteractionGroup>();
		InteractionMenuListWidgetBase interactionMenuListWidgetBase = ((interactionGroup == null) ? null : interactionGroup.InteractionMenu);
		if (interactionMenuListWidgetBase == null || !interactionMenuListWidgetBase.gameObject.activeInHierarchy)
		{
			return null;
		}
		UICamera uICamera = UICamera.FindCameraForLayer(interactionMenuListWidgetBase.gameObject.layer);
		if (!(uICamera == null))
		{
			return uICamera.cachedCamera.WorldToScreenPoint(interactionMenuListWidgetBase.transform.position);
		}
		return null;
	}

	private void Update()
	{
		Listen(Connections.FrontendIfAny);
		if (lingerUntil > 0f)
		{
			if (Time.unscaledTime > lingerUntil)
			{
				Cancel();
			}
		}
		else
		{
			if (!active)
			{
				return;
			}
			Layout();
			if (owner == null || !owner.IsGathering || timer == null || timer.Timer.IsStop || timer.Timer.IsInterrupt || timer.Timer.Remain <= 0f)
			{
				EndGather();
				return;
			}
			model.Tick(Now);
			if (model.Phase == CheckPhase.Spinning)
			{
				result.text = "";
			}
			if (!presented && model.Phase == CheckPhase.Result)
			{
				PresentResult();
			}
			if (model.Phase == CheckPhase.Result && Time.unscaledTime > showUntil && !pointerHeld)
			{
				panel.gameObject.SetActive(value: false);
			}
			effects.Animate(model.Phase);
			float unscaledTime = Time.unscaledTime;
			bool flag = model.Phase == CheckPhase.Result && (model.Result == CheckResult.Miss || model.Result == CheckResult.Early);
			dial.Glow = 0.35f + 0.45f * (0.5f + 0.5f * Mathf.Sin(unscaledTime * MathF.PI * 2f / 1.4f));
			dial.Ripple = ((model.Phase == CheckPhase.Warning) ? (Mathf.Repeat(unscaledTime - started, 0.65f) / 0.65f) : (-1f));
			dial.Trail = model.Phase == CheckPhase.Spinning;
			dial.HaloColor = (flag ? PresetColor.UILightRed : SkillCheckGraphic.Gold);
			Draw();
		}
	}

	private void Draw()
	{
		dial.Angle = model.AngleDegrees;
		dial.Target = model.TargetDegrees;
		dial.GoodHalf = model.GoodHalf;
		dial.GreatHalf = model.GreatHalf;
		dial.MarkAsChanged();
	}

	private void Cancel()
	{
		if (active || !(lingerUntil <= 0f))
		{
			active = false;
			lingerUntil = 0f;
			pointerHeld = false;
			model.Cancel(Now);
			if (effects != null)
			{
				effects.Cancel();
			}
			if (panel != null)
			{
				panel.gameObject.SetActive(value: false);
			}
		}
	}

	private void OnApplicationFocus(bool focus)
	{
		if (!focus)
		{
			Cancel();
		}
	}

	private void OnApplicationPause(bool paused)
	{
		if (paused)
		{
			Cancel();
		}
	}

	public static void SuspendForHudEdit()
	{
		if (instance != null)
		{
			instance.Cancel();
		}
	}

	private void OnDestroy()
	{
		if (dial != null && dial.SharedMaterial != null)
		{
			UnityEngine.Object.Destroy(dial.SharedMaterial);
		}
		if (panel != null)
		{
			UnityEngine.Object.Destroy(panel.transform.root.gameObject);
		}
		if (instance == this)
		{
			instance = null;
		}
	}
}
