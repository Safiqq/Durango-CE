using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Durango.Logic;
using Durango.Network;
using Durango.Terrain;
using Durango.UI;
using Durango.UI.Control;
using Durango.UI.Popup;
using Durango.Utils;
using InteractionData;
using Messages;
using UnityEngine;
using UnityEngine.SceneManagement;
using Yaml;

namespace DurangoCE.ClientMod
{
    /// <summary>
    /// Cleanroom Durango-CE HUD polish module loaded natively via ScriptingAssemblies.json:
    /// 1. Replaces the floating orange Run circle button with a clean bottom-center character state text
    ///    ("Idle", "Walking", "Running", "Gathering", "In Combat", "Swimming", "Riding").
    /// 2. Adds crisp outlined description captions below all main-screen HUD buttons.
    /// 3. Smooths on-foot walking stride animation speed (0.72x) vs Shift sprint (1.45x) so Walk vs Run
    ///    feels clearly distinct on PC keyboards.
    /// </summary>
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Init()
        {
            if (GameObject.Find("DurangoCE_HudPolish") != null)
            {
                return;
            }
            var go = new GameObject("DurangoCE_HudPolish");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<HudPolishBehaviour>();
            var autoType = typeof(Bootstrap).Assembly.GetType("DurangoCE.ClientMod.AutoPlaytestBehaviour");
            autoType?.GetMethod("TryAttach", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                ?.Invoke(null, new object[] { go });
            UnityEngine.Debug.LogWarning("[Durango-CE] HudPolishMod initialized.");
        }
    }

    public sealed class HudPolishBehaviour : MonoBehaviour
    {
        private const string CaptionName = "DurangoCE_Caption";
        private static readonly BindingFlags AllInstance =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly BindingFlags AllStatic =
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        private GameObject _statusRoot;
        private UILabel _statusLabel;
        private UILabel _fatigueRateLabel;
        private UILabel _cachedStyle;
        private Shader _cachedNguiShader;
        private float _nextScanAt;
        private float _nextStyleLookupAt;
        private float _nextMenuLookupAt;
        private float _lastFatigueVel = float.NaN;
        private string _lastStateText = string.Empty;
        private bool _slowedWalkAnim;
        private Animator _slowedAnimator;
        private PlayerBehavior _lastWalkPlayer;
        private PlayerController _hookedController;
        private object _patchedKeyboardRef;
        private float _nextBottomMenuLookupAt;
        private bool _appliedLoginZoom;
        private GameObject _menuExitBtn;
        private Texture2D _whiteTex;
        private MenuListGroupBase _cachedMenuGroup;
        private BottomMenuWidgetBase _cachedBottomMenu;
        private EquipQuickSlotWidget _cachedEquipQuick;
        private SideEffectGroup _cachedSideEffect;
        private MinimapGroupBase _cachedMinimap;
        private PlayerHudGroupBase _cachedPlayerHud;
        private ContextActionGroupBase _cachedCtxGroup;
        private int _lastPlayerHudChildCount = -1;
        private int _lastCtxGroupChildCount = -1;
        private readonly List<ContextActionButtonBase> _staleContextKeys =
            new List<ContextActionButtonBase>();
        private bool _clearedForNotReady;
        private string _sanitizedLocale = string.Empty;
        private float _nextUiSanitizeAt;
        private EditPlayerNamePage _hookedNamePage;
        private UILabel _nameWarnLabel;
        private float _nextNamePageCheckAt;
        private bool _overlayOpenPrevFrame;
        private bool _escPressedThisFrame;
        private bool _escSimulatedThisFrame;

        private static readonly Regex RichTagRegex =
            new Regex("<[^>]+>|\\[[^\\]]+\\]", RegexOptions.Compiled);

        // Cached reflection handles (avoid per-frame allocations / lookups)
        private static Type _runButtonType;
        private static Func<bool> _runRunningGetter;
        private static Type _camCtrlType;
        private static MethodInfo _camHasInstance;
        private static MethodInfo _camInstance;
        private static MethodInfo _camSetZoom;
        private static FieldInfo _camLastZoomField;
        private static FieldInfo _localizeDictField;

        private readonly Dictionary<ContextActionButtonBase, UILabel> _contextCaptions =
            new Dictionary<ContextActionButtonBase, UILabel>();

        internal void SimulateEscapeKey()
        {
            _escPressedThisFrame = true;
            _escSimulatedThisFrame = true;
        }

        private void Awake()
        {
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            var asm = typeof(PlayerController).Assembly;
            _runButtonType = asm.GetType("OffServerRunButton");
            if (_runButtonType != null)
            {
                var runningProp = _runButtonType.GetProperty("Running", AllStatic);
                var getter = runningProp != null ? runningProp.GetGetMethod(true) : null;
                if (getter != null)
                {
                    try
                    {
                        _runRunningGetter = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), getter);
                    }
                    catch
                    {
                    }
                }
            }
            _camCtrlType = asm.GetType("Durango.Render.Camera.CameraController");
            if (_camCtrlType != null)
            {
                _camHasInstance = _camCtrlType.BaseType != null ? _camCtrlType.BaseType.GetMethod("HasInstance", AllStatic) : null;
                _camInstance = _camCtrlType.BaseType != null ? _camCtrlType.BaseType.GetMethod("Instance", AllStatic) : null;
                _camSetZoom = _camCtrlType.GetMethod("SetZoom", AllInstance, null, new[] { typeof(float) }, null);
                _camLastZoomField = _camCtrlType.GetField("LastZoom", AllStatic);
            }
            _localizeDictField = typeof(LocalizeSystem).GetField("_keyValueDict", AllStatic);
            EnsureLocalizationSanitized();
        }

        private void OnSceneUnloaded(Scene scene)
        {
            ClearSceneCaches();
            string sname = scene.name ?? string.Empty;
            if (sname.IndexOf("Title", StringComparison.OrdinalIgnoreCase) >= 0 ||
                sname.IndexOf("Login", StringComparison.OrdinalIgnoreCase) >= 0 ||
                sname.IndexOf("Intro", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _appliedLoginZoom = false;
            }
        }

        private void ClearSceneCaches()
        {
            _patchedKeyboardRef = null;
            _cachedStyle = null;
            _cachedNguiShader = null;
            _cachedMenuGroup = null;
            _cachedBottomMenu = null;
            _cachedEquipQuick = null;
            _cachedSideEffect = null;
            _cachedMinimap = null;
            _cachedPlayerHud = null;
            _cachedCtxGroup = null;
            _menuExitBtn = null;
            _hookedNamePage = null;
            _nameWarnLabel = null;
            _lastPlayerHudChildCount = -1;
            _lastCtxGroupChildCount = -1;
            _lastFatigueVel = float.NaN;
            _lastWalkPlayer = null;
            _contextCaptions.Clear();
            RestoreWalkAnimator();
        }

        private void Update()
        {
            EnsureLocalizationSanitized();

            if (!GameManager.IsReady || GameManager.IsPrologueMode)
            {
                if (_statusRoot != null && _statusRoot.activeSelf)
                {
                    _statusRoot.SetActive(false);
                }
                if (!_clearedForNotReady)
                {
                    _clearedForNotReady = true;
                    ClearSceneCaches();
                }
                _overlayOpenPrevFrame = false;
                _escPressedThisFrame = false;
                _escSimulatedThisFrame = false;
                EnsureNamePageFeedback();
                return;
            }
            _clearedForNotReady = false;

            if (!Application.isMobilePlatform && Input.GetKeyDown(KeyCode.Escape))
            {
                _escPressedThisFrame = true;
            }

            EnsureStatusLabel();
            EnsureMoveCancelHook();
            if (!Application.isMobilePlatform)
            {
                EnsureMovementKeyLayers();
            }
            EnsureLoginCameraZoom();
            EnsureMenuDrawerExitButton();
            UpdateCharacterStateAndWalkAnim();

            float now = Time.unscaledTime;
            if (now >= _nextScanAt)
            {
                _nextScanAt = now + 0.60f;
                RefreshMainScreenButtonCaptions();
            }
        }

        private void LateUpdate()
        {
            if (!GameManager.IsReady || GameManager.IsPrologueMode)
            {
                _overlayOpenPrevFrame = false;
                _escPressedThisFrame = false;
                _escSimulatedThisFrame = false;
                return;
            }

            bool overlayOpenNow = IsAnyOverlayOrMenuOpen();
            if (_escPressedThisFrame)
            {
                _escPressedThisFrame = false;
                bool simulated = _escSimulatedThisFrame;
                _escSimulatedThisFrame = false;
                try
                {
                    if (_cachedMenuGroup == null)
                    {
                        _cachedMenuGroup = UIManager.FindScript<MenuListGroupBase>();
                    }
                    if (!UIManager.IsLoadingCurtain)
                    {
                        if (_cachedMenuGroup != null && _cachedMenuGroup.IsOpened)
                        {
                            _cachedMenuGroup.Close();
                            overlayOpenNow = IsAnyOverlayOrMenuOpen();
                        }
                        else if (simulated && UIBase.CurrentUI != null)
                        {
                            UIBase.CurrentUI.Close();
                            overlayOpenNow = IsAnyOverlayOrMenuOpen();
                        }
                        else if (!_overlayOpenPrevFrame && !overlayOpenNow)
                        {
                            if (_cachedMenuGroup != null && !_cachedMenuGroup.IsOpened)
                            {
                                _cachedMenuGroup.Open();
                                overlayOpenNow = true;
                            }
                        }
                    }
                }
                catch
                {
                }
            }
            _overlayOpenPrevFrame = overlayOpenNow;
        }

        private bool IsAnyOverlayOrMenuOpen()
        {
            try
            {
                if (UIManager.IsLoadingCurtain)
                {
                    return true;
                }
                if (UIBase.CurrentUI != null)
                {
                    return true;
                }
                if (UIManager.MessageBox != null && UIManager.MessageBox.IsShow)
                {
                    return true;
                }
                if (UICamera.inputHasFocus)
                {
                    return true;
                }
                if (_cachedMenuGroup == null)
                {
                    _cachedMenuGroup = UIManager.FindScript<MenuListGroupBase>();
                }
                if (_cachedMenuGroup != null && _cachedMenuGroup.IsOpened)
                {
                    return true;
                }
                var buildGrid = UIManager.FindScript<BuildGridGroupBase>();
                if (buildGrid != null && buildGrid.IsOpened)
                {
                    return true;
                }
                var interaction = UIManager.FindScript<InteractionGroup>();
                if (interaction != null && interaction.IsOpened)
                {
                    return true;
                }
                var chatPc = UIManager.FindScript<ChattingGroup_PC>();
                if (chatPc != null && chatPc.IsOpened && chatPc.Visibility == ChattingGroup_PC.ChatVisibility.Full)
                {
                    return true;
                }
            }
            catch
            {
            }
            return false;
        }

        private void OnDisable()
        {
            RestoreWalkAnimator();
            UnhookMoveCancel();
        }

        private void OnDestroy()
        {
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            ClearSceneCaches();
            UnhookMoveCancel();
            if (_whiteTex != null)
            {
                UnityEngine.Object.Destroy(_whiteTex);
                _whiteTex = null;
            }
        }

        private void EnsureMoveCancelHook()
        {
            try
            {
                if (!Singleton<PlayerController>.HasInstance())
                {
                    return;
                }
                var ctrl = Singleton<PlayerController>.Instance();
                if (ctrl == null || ctrl == _hookedController)
                {
                    return;
                }
                UnhookMoveCancel();
                _hookedController = ctrl;
                _hookedController.MoveStarted += OnManualMoveStarted;
            }
            catch
            {
            }
        }

        private void UnhookMoveCancel()
        {
            if (_hookedController != null)
            {
                try
                {
                    _hookedController.MoveStarted -= OnManualMoveStarted;
                }
                catch
                {
                }
                _hookedController = null;
            }
        }

        private void OnManualMoveStarted()
        {
            // When the player manually walks away mid-gather (WASD/Arrows/ground click — NEVER
            // fired by MoveToTarget auto-walk!), clear stacked ReservationQueue items immediately
            // AND send explicit Cancel (2036) so the server aborts the gather right away without
            // relying on delayed Move packets.
            try
            {
                var interaction = GameSystem<InteractionSystem>.Instance();
                if (interaction != null && interaction.ReservationQueue != null && interaction.ReservationQueue.Count > 0)
                {
                    interaction.ReservationQueue.Clear();
                }
            }
            catch
            {
            }
            try
            {
                var gathering = GameSystem<GatheringSystem>.Instance();
                if (gathering != null && gathering.IsGathering && Connections.Frontend != null)
                {
                    Connections.Frontend.Send(default(Cancel));
                }
            }
            catch
            {
            }
        }

        private bool IsRunActive()
        {
            try
            {
                if (_runRunningGetter != null)
                {
                    return _runRunningGetter();
                }
            }
            catch
            {
            }
            return !Application.isMobilePlatform
                && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
        }

        private bool IsMainHudVisible()
        {
            try
            {
                if (UIManager.IsLoadingCurtain)
                {
                    return false;
                }
                if (UIBase.CurrentUI != null)
                {
                    return false;
                }
                if (_cachedMenuGroup == null)
                {
                    _cachedMenuGroup = UIManager.FindScript<MenuListGroupBase>();
                }
                if (_cachedMenuGroup != null && _cachedMenuGroup.IsOpened)
                {
                    return false;
                }
                if (_cachedBottomMenu == null && Time.unscaledTime >= _nextBottomMenuLookupAt)
                {
                    _nextBottomMenuLookupAt = Time.unscaledTime + 0.60f;
                    _cachedBottomMenu = UIManager.FindScript<BottomMenuWidgetBase>();
                }
                if (_cachedBottomMenu != null && !_cachedBottomMenu.gameObject.activeInHierarchy)
                {
                    return false;
                }
                return true;
            }
            catch
            {
                return true;
            }
        }

        private void EnsureStatusLabel()
        {
            if (_statusRoot != null && _statusLabel != null)
            {
                if (_statusLabel.bitmapFont == null && _statusLabel.trueTypeFont == null)
                {
                    ApplyLabelStyle(_statusLabel, 18);
                }
                return;
            }
            if (!Singleton<UIManager>.HasInstance())
            {
                return;
            }
            var uiRoot = Singleton<UIManager>.Instance().UIRoot;
            if (uiRoot == null)
            {
                return;
            }

            _statusRoot = uiRoot.gameObject.AddChild();
            _statusRoot.name = "DurangoCE_CharacterState";
            var panel = _statusRoot.AddComponent<UIPanel>();
            panel.depth = 60;

            _statusLabel = _statusRoot.AddWidget<UILabel>(2);
            _statusLabel.name = "StateLabel";
            ApplyLabelStyle(_statusLabel, 18);
            _statusLabel.width = 320;
            _statusLabel.height = 30;
            _lastStateText = string.Empty;

            var anchor = UIRootAnchor.GetRootAnchor(UIBase.AnchorType.Base);
            if (anchor != null)
            {
                _statusLabel.SetAnchor(anchor.gameObject, 0.5f, -160, 0f, 18, 0.5f, 160, 0f, 48);
                _statusLabel.ResetAndUpdateAnchors();
            }
            else
            {
                _statusLabel.transform.localPosition = new Vector3(0f, -310f, 0f);
            }

            // Crisp top-left fatigue recovery rate visual right beside the yellow Fatigue bar
            _fatigueRateLabel = _statusRoot.AddWidget<UILabel>(3);
            _fatigueRateLabel.name = "FatigueRateLabel";
            ApplyLabelStyle(_fatigueRateLabel, 15);
            _fatigueRateLabel.alignment = NGUIText.Alignment.Left;
            _fatigueRateLabel.color = new Color(0.42f, 0.95f, 0.70f, 1f);
            _fatigueRateLabel.width = 210;
            _fatigueRateLabel.height = 24;
            _fatigueRateLabel.text = string.Empty;
            if (anchor != null)
            {
                _fatigueRateLabel.SetAnchor(anchor.gameObject, 0f, 236, 1f, -72, 0f, 446, 1f, -48);
                _fatigueRateLabel.ResetAndUpdateAnchors();
            }
            _fatigueRateLabel.gameObject.SetActive(false);
        }

        private void UpdateCharacterStateAndWalkAnim()
        {
            bool hudVisible = IsMainHudVisible();
            if (_statusRoot != null && _statusRoot.activeSelf != hudVisible)
            {
                _statusRoot.SetActive(hudVisible);
            }

            PlayerBehavior player = null;
            try
            {
                player = PlayerBehavior.LocalPlayer;
            }
            catch
            {
            }

            if (player == null)
            {
                RestoreWalkAnimator();
                return;
            }

            bool isGathering = false;
            bool inCombat = false;
            try
            {
                isGathering = GameSystem<GatheringSystem>.Instance().IsGathering;
            }
            catch
            {
            }
            try
            {
                inCombat = GameSystem<CombatSystem>.Instance().CombatMode;
            }
            catch
            {
            }

            bool isMoving = false;
            bool isRiding = false;
            bool isSwimming = false;
            bool isAlive = true;
            try
            {
                isMoving = (bool)player.IsMoving;
                isRiding = player.IsRiding;
                isSwimming = player.WaterDepthLevel >= TerrainWater.WaterDepthLevel.Swim;
                isAlive = player.IsAlive;
            }
            catch
            {
            }

            bool running = IsRunActive();
            float fatigueVel = 0f;
            try
            {
                var fatSys = GameSystem<FatigueSystem>.Instance();
                if (fatSys != null)
                {
                    fatigueVel = fatSys.FatigueVelocity;
                }
            }
            catch
            {
            }
            bool isResting = isAlive && !isMoving && !isGathering && !inCombat && !isSwimming && !isRiding && fatigueVel < -0.05f;

            // Smooth on-foot walking stride animation so Walk (no Shift) vs Run (Shift) looks visually distinct
            bool shouldSlowWalk = isAlive && isMoving && !running && !isRiding && !isSwimming && !isGathering && !inCombat;
            UpdateWalkAnimator(player, shouldSlowWalk);

            bool velChanged = float.IsNaN(_lastFatigueVel) || Mathf.Abs(fatigueVel - _lastFatigueVel) >= 0.05f;
            if (velChanged)
            {
                _lastFatigueVel = fatigueVel;
            }

            if (_fatigueRateLabel != null)
            {
                bool showRate = hudVisible && isResting;
                if (_fatigueRateLabel.gameObject.activeSelf != showRate)
                {
                    _fatigueRateLabel.gameObject.SetActive(showRate);
                }
                if (showRate)
                {
                    if (velChanged || string.IsNullOrEmpty(_fatigueRateLabel.text))
                    {
                        _fatigueRateLabel.text = string.Format("▼ {0:0.0}/s  Resting", fatigueVel);
                    }
                    float pulse = 0.72f + 0.28f * Mathf.Sin(Time.unscaledTime * 4.2f);
                    _fatigueRateLabel.color = new Color(0.42f, 0.95f, 0.70f, pulse);
                }
            }

            if (!hudVisible || _statusLabel == null)
            {
                return;
            }

            string text = null;
            Color color;
            if (!isAlive)
            {
                text = "Unconscious";
                color = new Color(0.95f, 0.45f, 0.45f, 1f);
            }
            else if (isGathering)
            {
                text = "Gathering";
                color = new Color(0.66f, 0.94f, 0.66f, 1f);
            }
            else if (inCombat)
            {
                text = isMoving ? (running ? "Combat · Running" : "Combat · Walking") : "In Combat";
                color = new Color(1f, 0.58f, 0.51f, 1f);
            }
            else if (isSwimming)
            {
                text = isMoving ? "Swimming" : "Floating";
                color = new Color(0.56f, 0.85f, 1f, 1f);
            }
            else if (isRiding)
            {
                text = isMoving ? (running ? "Riding · Sprinting" : "Riding") : "Mounted";
                color = new Color(1f, 0.83f, 0.47f, 1f);
            }
            else if (isResting)
            {
                if (velChanged || !_lastStateText.StartsWith("Resting", StringComparison.Ordinal))
                {
                    text = string.Format("Resting  ·  Fatigue {0:0.0}/s", fatigueVel);
                }
                color = new Color(0.42f, 0.95f, 0.70f, 1f);
            }
            else if (isMoving && running)
            {
                text = "Running";
                color = new Color(1f, 0.78f, 0.35f, 1f);
            }
            else if (isMoving)
            {
                text = "Walking";
                color = Color.white;
            }
            else
            {
                text = "Idle";
                color = new Color(0.80f, 0.83f, 0.86f, 0.92f);
            }

            if (text != null && text != _lastStateText)
            {
                _lastStateText = text;
                _statusLabel.text = text;
                _statusLabel.color = color;
            }
        }

        private void UpdateWalkAnimator(PlayerBehavior player, bool shouldSlowWalk)
        {
            if (Application.isMobilePlatform || player == null)
            {
                RestoreWalkAnimator();
                return;
            }
            try
            {
                if (_slowedAnimator == null || _lastWalkPlayer != player)
                {
                    RestoreWalkAnimator();
                    _slowedAnimator = player.GetComponentInChildren<Animator>();
                    _lastWalkPlayer = player;
                }
                if (_slowedAnimator == null)
                {
                    return;
                }
                if (shouldSlowWalk)
                {
                    if (Mathf.Abs(_slowedAnimator.speed - 0.72f) > 0.02f)
                    {
                        _slowedAnimator.speed = 0.72f;
                    }
                    _slowedWalkAnim = true;
                }
                else if (_slowedWalkAnim)
                {
                    // Only restore 1.0x if the speed is still our slowed 0.72x value so we never
                    // clobber Shift-sprint's 1.45x or combat/action animation speeds!
                    if (Mathf.Abs(_slowedAnimator.speed - 0.72f) < 0.05f)
                    {
                        _slowedAnimator.speed = 1f;
                    }
                    _slowedWalkAnim = false;
                }
            }
            catch
            {
            }
        }

        private void RestoreWalkAnimator()
        {
            if (_slowedWalkAnim && _slowedAnimator != null)
            {
                try
                {
                    if (Mathf.Abs(_slowedAnimator.speed - 0.72f) < 0.05f)
                    {
                        _slowedAnimator.speed = 1f;
                    }
                }
                catch
                {
                }
            }
            _slowedWalkAnim = false;
            _slowedAnimator = null;
        }

        private void RefreshMainScreenButtonCaptions()
        {
            // 1. Bottom-Left: Menu, Emote, Chat (cached component refs to avoid per-tick reflection/scene scans)
            try
            {
                if (_cachedMenuGroup == null)
                {
                    _cachedMenuGroup = UIManager.FindScript<MenuListGroupBase>();
                }
                if (_cachedMenuGroup != null)
                {
                    var menuBtn = GetFieldValue<GameObject>(_cachedMenuGroup, "_menuBtn");
                    if (menuBtn != null)
                    {
                        AttachOrUpdateCaption(menuBtn.transform, "Menu", -26f, 12);
                    }
                }
            }
            catch
            {
            }

            try
            {
                if (_cachedBottomMenu == null)
                {
                    _cachedBottomMenu = UIManager.FindScript<BottomMenuWidgetBase>();
                }
                if (_cachedBottomMenu != null)
                {
                    var commBtn = GetFieldValue<Component>(_cachedBottomMenu, "_communicationButton");
                    if (commBtn != null)
                    {
                        AttachOrUpdateCaption(commBtn.transform, "Emote", -26f, 12);
                    }
                    var chatBtn = GetFieldValue<Component>(_cachedBottomMenu, "_quickChatButton");
                    if (chatBtn != null)
                    {
                        AttachOrUpdateCaption(chatBtn.transform, "Chat", -26f, 12);
                    }
                }
            }
            catch
            {
            }

            // 2. Bottom-Right: Outfit quick-slot button (T-shirt icon)
            try
            {
                if (_cachedEquipQuick == null)
                {
                    _cachedEquipQuick = UnityEngine.Object.FindAnyObjectByType<EquipQuickSlotWidget>();
                }
                if (_cachedEquipQuick != null)
                {
                    var toggle = GetFieldValue<Component>(_cachedEquipQuick, "_toggle");
                    if (toggle != null)
                    {
                        AttachOrUpdateCaption(toggle.transform, "Outfit", -24f, 12);
                    }
                }
            }
            catch
            {
            }

            // 3. Bottom-Right: Combat mode toggle button (Crossed Swords) & Zoom/Utility buttons
            try
            {
                if (_cachedSideEffect == null)
                {
                    _cachedSideEffect = UIManager.FindScript<SideEffectGroup>();
                }
                if (_cachedSideEffect != null)
                {
                    var combatBtn = _cachedSideEffect.transform.Find("CombatEffect/CombatModeButton");
                    if (combatBtn != null)
                    {
                        AttachOrUpdateCaption(combatBtn, "Combat", -24f, 12);
                    }
                }
            }
            catch
            {
            }

            // 4. Bottom-Right: Dynamic Context Action Hexagons (Anchor/Travel, Search/Radar, Drink, Wash, etc.)
            try
            {
                if (_contextCaptions.Count > 0)
                {
                    _staleContextKeys.Clear();
                    foreach (var kv in _contextCaptions)
                    {
                        if (kv.Key == null || kv.Value == null)
                        {
                            _staleContextKeys.Add(kv.Key);
                        }
                    }
                    for (int i = 0; i < _staleContextKeys.Count; i++)
                    {
                        _contextCaptions.Remove(_staleContextKeys[i]);
                    }
                }
                var contextButtons = UnityEngine.Object.FindObjectsByType<ContextActionButtonBase>();
                foreach (var btn in contextButtons)
                {
                    if (btn == null || !btn.gameObject.activeInHierarchy)
                    {
                        continue;
                    }
                    string label;
                    try
                    {
                        label = FormatContextActionLabel(btn);
                    }
                    catch
                    {
                        continue;
                    }
                    if (string.IsNullOrEmpty(label))
                    {
                        if (_contextCaptions.TryGetValue(btn, out var stale) && stale != null)
                        {
                            stale.text = string.Empty;
                        }
                        continue;
                    }
                    float yOff = ComputeButtonCaptionOffset(btn.transform, -36f);
                    if (_contextCaptions.TryGetValue(btn, out var existing) && existing != null)
                    {
                        if (existing.bitmapFont == null && existing.trueTypeFont == null)
                        {
                            ApplyLabelStyle(existing, 12);
                        }
                        if (existing.text != label)
                        {
                            existing.text = label;
                        }
                    }
                    else
                    {
                        var cap = AttachOrUpdateCaption(btn.transform, label, yOff, 12);
                        if (cap != null)
                        {
                            _contextCaptions[btn] = cap;
                        }
                    }
                }
            }
            catch
            {
            }

            // 5. Top-Right: Minimap caption (placed cleanly under coordinates)
            try
            {
                if (_cachedMinimap == null)
                {
                    _cachedMinimap = UIManager.FindScript<MinimapGroupBase>();
                }
                if (_cachedMinimap != null)
                {
                    var touchBox = GetFieldValue<Component>(_cachedMinimap, "_minimapTouchBox");
                    if (touchBox != null)
                    {
                        float yOff = ComputeButtonCaptionOffset(touchBox.transform, -76f);
                        AttachOrUpdateCaption(touchBox.transform, "Map", yOff, 12);
                    }
                }
            }
            catch
            {
            }

            // 6. Catch any remaining bottom-right HUD magnifying-glass / camera / zoom / bag icon buttons
            //    Only walk child UIEventListeners when the hierarchy child count changes (avoids GC churn!).
            try
            {
                if (_cachedPlayerHud == null)
                {
                    _cachedPlayerHud = UIManager.FindScript<PlayerHudGroupBase>();
                    _lastPlayerHudChildCount = -1;
                }
                if (_cachedPlayerHud != null)
                {
                    int c = _cachedPlayerHud.transform.childCount;
                    if (c != _lastPlayerHudChildCount)
                    {
                        _lastPlayerHudChildCount = c;
                        LabelNamedHudButtons(_cachedPlayerHud.transform);
                    }
                }
                if (_cachedCtxGroup == null)
                {
                    _cachedCtxGroup = UIManager.FindScript<ContextActionGroupBase>();
                    _lastCtxGroupChildCount = -1;
                }
                if (_cachedCtxGroup != null)
                {
                    int c = _cachedCtxGroup.transform.childCount;
                    if (c != _lastCtxGroupChildCount)
                    {
                        _lastCtxGroupChildCount = c;
                        LabelNamedHudButtons(_cachedCtxGroup.transform);
                    }
                }
            }
            catch
            {
            }
        }

        private static bool HasCaptionInHierarchy(Transform t)
        {
            if (t == null)
            {
                return false;
            }
            // Check descendants
            var labels = t.GetComponentsInChildren<UILabel>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] != null && labels[i].name == CaptionName)
                {
                    return true;
                }
            }
            // Check immediate ancestors up to 3 levels to avoid duplicating nested UIEventListener captions
            var p = t.parent;
            for (int depth = 0; depth < 3 && p != null; depth++)
            {
                if (p.Find(CaptionName) != null)
                {
                    return true;
                }
                p = p.parent;
            }
            return false;
        }

        private void LabelNamedHudButtons(Transform root)
        {
            if (root == null)
            {
                return;
            }
            var listeners = root.GetComponentsInChildren<UIEventListener>(true);
            foreach (var l in listeners)
            {
                if (l == null || !l.gameObject.activeInHierarchy)
                {
                    continue;
                }
                if (l.GetComponentInParent<ContextActionButtonBase>() != null)
                {
                    continue;
                }
                if (HasCaptionInHierarchy(l.transform))
                {
                    continue;
                }
                string n = l.gameObject.name.ToLowerInvariant();
                string spName = string.Empty;
                var sp = l.GetComponentInChildren<UISprite>();
                if (sp != null && !string.IsNullOrEmpty(sp.spriteName))
                {
                    spName = sp.spriteName.ToLowerInvariant();
                }
                string key = n + " " + spName;
                string caption = null;
                if (key.Contains("zoom") || key.Contains("camera") || key.Contains("magnif") || key.Contains("lens"))
                {
                    caption = "Zoom";
                }
                else if (key.Contains("search") || key.Contains("radar") || key.Contains("scan") || key.Contains("warphole"))
                {
                    caption = "Search";
                }
                else if (key.Contains("combat") || key.Contains("battle") || key.Contains("sword"))
                {
                    caption = "Combat";
                }
                else if (key.Contains("inven") || key.Contains("bag"))
                {
                    caption = "Bag";
                }
                else if (key.Contains("equip") || key.Contains("outfit") || key.Contains("costume") || key.Contains("clothes"))
                {
                    caption = "Outfit";
                }
                if (caption != null)
                {
                    float yOff = ComputeButtonCaptionOffset(l.transform, -24f);
                    AttachOrUpdateCaption(l.transform, caption, yOff, 12);
                }
            }
        }

        private static string FormatContextActionLabel(ContextActionButtonBase btn)
        {
            if (btn == null)
            {
                return string.Empty;
            }
            Interaction action = btn.Menu.Action;
            switch (action)
            {
                case Interaction.SearchWarphole:
                    return "Warphole";
                case Interaction.Warp:
                    return "Travel";
                case Interaction.Dash:
                    return "Dash";
                case Interaction.Attack:
                    return "Attack";
                case Interaction.DrinkWater:
                    return "Drink";
                case Interaction.WashBody:
                    return "Wash";
                case Interaction.SelectDrawContainer:
                    return "Draw";
                case Interaction.Mount:
                    return "Mount";
                case Interaction.Dismount:
                    return "Dismount";
                case Interaction.Inventory:
                    return "Bag";
                case Interaction.LookAroundArtifact:
                    return "Look";
                default:
                    if (action.ToString().IndexOf("Screenshot", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return "Photo";
                    }
                    string raw = btn.Menu.Name;
                    if (!string.IsNullOrEmpty(raw))
                    {
                        string clean = RichTagRegex.Replace(raw, string.Empty).Trim();
                        if (string.Equals(clean, "Take Screenshot", StringComparison.OrdinalIgnoreCase))
                        {
                            return "Photo";
                        }
                        if (clean.Length > 16)
                        {
                            clean = clean.Substring(0, 16).Trim();
                        }
                        if (!string.IsNullOrEmpty(clean))
                        {
                            return clean;
                        }
                    }
                    return action.ToString();
            }
        }

        private static float ComputeButtonCaptionOffset(Transform target, float fallback)
        {
            var w = target.GetComponent<UIWidget>();
            if (w != null && w.height > 16)
            {
                switch (w.pivot)
                {
                    case UIWidget.Pivot.Top:
                    case UIWidget.Pivot.TopLeft:
                    case UIWidget.Pivot.TopRight:
                        return -w.height - 9f;
                    case UIWidget.Pivot.Bottom:
                    case UIWidget.Pivot.BottomLeft:
                    case UIWidget.Pivot.BottomRight:
                        return -9f;
                    default:
                        return -(w.height * 0.5f) - 9f;
                }
            }
            return fallback;
        }

        private static int ComputeCaptionDepth(Transform parent)
        {
            int maxDepth = 25;
            var widgets = parent.GetComponentsInChildren<UIWidget>(true);
            for (int i = 0; i < widgets.Length; i++)
            {
                if (widgets[i] != null && widgets[i].depth > maxDepth)
                {
                    maxDepth = widgets[i].depth;
                }
            }
            return maxDepth + 2;
        }

        private UILabel AttachOrUpdateCaption(Transform parent, string text, float yOffset, int fontSize)
        {
            if (parent == null || string.IsNullOrEmpty(text))
            {
                return null;
            }
            var existingTf = parent.Find(CaptionName);
            if (existingTf != null)
            {
                var existingLbl = existingTf.GetComponent<UILabel>();
                if (existingLbl != null)
                {
                    if (existingLbl.bitmapFont == null && existingLbl.trueTypeFont == null)
                    {
                        ApplyLabelStyle(existingLbl, fontSize);
                    }
                    if (existingLbl.text != text)
                    {
                        existingLbl.text = text;
                    }
                }
                return existingLbl;
            }

            var lbl = parent.gameObject.AddWidget<UILabel>(ComputeCaptionDepth(parent));
            lbl.name = CaptionName;
            ApplyLabelStyle(lbl, fontSize);
            lbl.width = 112;
            lbl.height = fontSize + 6;
            lbl.text = text;
            lbl.transform.localPosition = new Vector3(0f, yOffset, 0f);
            lbl.transform.localScale = Vector3.one;
            return lbl;
        }

        private void ApplyLabelStyle(UILabel label, int fontSize)
        {
            ApplyLabelStyle(label, FindStyleLabel(), fontSize);
        }

        private static void ApplyLabelStyle(UILabel label, UILabel style, int fontSize)
        {
            if (style != null)
            {
                if (style.bitmapFont != null)
                {
                    label.bitmapFont = style.bitmapFont;
                }
                else
                {
                    label.trueTypeFont = style.trueTypeFont;
                }
            }
            label.fontSize = fontSize;
            label.alignment = NGUIText.Alignment.Center;
            label.overflowMethod = UILabel.Overflow.ShrinkContent;
            label.effectStyle = UILabel.Effect.Outline;
            label.effectColor = new Color(0f, 0f, 0f, 0.85f);
            label.effectDistance = new Vector2(1.2f, 1.2f);
            label.color = Color.white;
        }

        private UILabel FindStyleLabel()
        {
            if (_cachedStyle != null && (_cachedStyle.trueTypeFont != null || _cachedStyle.bitmapFont != null))
            {
                return _cachedStyle;
            }
            float now = Time.unscaledTime;
            if (now < _nextStyleLookupAt)
            {
                return null;
            }
            _nextStyleLookupAt = now + 0.50f;
            try
            {
                if (UIManager.MessageBox != null)
                {
                    var labels = UIManager.MessageBox.GetComponentsInChildren<UILabel>(true);
                    foreach (var l in labels)
                    {
                        if (l != null && (l.trueTypeFont != null || l.bitmapFont != null))
                        {
                            _cachedStyle = l;
                            return l;
                        }
                    }
                }
                if (Singleton<UIManager>.HasInstance())
                {
                    var uiRoot = Singleton<UIManager>.Instance().UIRoot;
                    if (uiRoot != null)
                    {
                        var labels = uiRoot.GetComponentsInChildren<UILabel>(true);
                        foreach (var l in labels)
                        {
                            if (l != null && l.name != CaptionName && l.name != "StateLabel" && (l.trueTypeFont != null || l.bitmapFont != null))
                            {
                                _cachedStyle = l;
                                return l;
                            }
                        }
                    }
                }
            }
            catch
            {
            }
            return null;
        }

        private static T GetFieldValue<T>(object target, string fieldName) where T : class
        {
            if (target == null)
            {
                return null;
            }
            var t = target.GetType();
            while (t != null)
            {
                var f = t.GetField(fieldName, AllInstance);
                if (f != null)
                {
                    return f.GetValue(target) as T;
                }
                t = t.BaseType;
            }
            return null;
        }

        private static Transform GetFieldTransform(object target, string fieldName)
        {
            var comp = GetFieldValue<Component>(target, fieldName);
            if (comp != null)
            {
                return comp.transform;
            }
            var go = GetFieldValue<GameObject>(target, fieldName);
            return go != null ? go.transform : null;
        }

        /// <summary>
        /// 1. Strips FullscreenUI (2) and InventoryUI (8) from WASD/Arrow movement bindings so misclicking
        ///    movement keys while Bag/Character/Craft/Domain is open never closes the window or walks away.
        /// 2. Rebinds Escape on Layer.GamePlay (no window open) to ShowMenuList (opens the Menu Drawer with
        ///    Bag, Character, Skills, Settings, Exit) while keeping Escape on open UI layers as Back.
        /// </summary>
        private void EnsureMovementKeyLayers()
        {
            try
            {
                var inputSys = GameSystem<InputSystem>.Instance();
                if (inputSys == null)
                {
                    return;
                }
                object kb = inputSys.Keyboard;
                if (kb == null)
                {
                    var kbField = inputSys.GetType().GetField("_keyboard", AllInstance);
                    kb = kbField != null ? kbField.GetValue(inputSys) : null;
                }
                if (kb == null || ReferenceEquals(_patchedKeyboardRef, kb))
                {
                    return;
                }
                if (PatchKeyboardKeyMap(kb))
                {
                    _patchedKeyboardRef = kb;
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[Durango-CE] EnsureMovementKeyLayers: " + ex.Message);
            }
        }

        private static object CloneKeySet(object keyObj)
        {
            object clone = Activator.CreateInstance(keyObj.GetType());
            foreach (var f in keyObj.GetType().GetFields(AllInstance))
            {
                f.SetValue(clone, f.GetValue(keyObj));
            }
            return clone;
        }

        private static bool PatchKeyboardKeyMap(object keyboard)
        {
            var keyMapField = keyboard.GetType().GetField("_keyMap", AllInstance);
            var keyMap = keyMapField != null ? keyMapField.GetValue(keyboard) as System.Collections.IDictionary : null;
            if (keyMap == null || keyMap.Count == 0)
            {
                return false;
            }
            var updates = new List<KeyValuePair<object, object>>();
            var removals = new List<object>();
            const int blockedMoveLayers = 2 | 8; // FullscreenUI (2) | InventoryUI (8)
            const int gamePlayLayer = 1;         // Layer.GamePlay (1)

            object showMenuListEnumVal = null;
            foreach (System.Collections.DictionaryEntry entry in keyMap)
            {
                if (entry.Value != null && entry.Value.ToString() == "ShowMenuList")
                {
                    showMenuListEnumVal = entry.Value;
                    break;
                }
            }

            foreach (System.Collections.DictionaryEntry entry in keyMap)
            {
                var cmdName = entry.Value != null ? entry.Value.ToString() : string.Empty;
                object keyObj = entry.Key;
                if (keyObj == null)
                {
                    continue;
                }
                var layersField = keyObj.GetType().GetField("Layers", AllInstance);
                var codeField = keyObj.GetType().GetField("Code", AllInstance);
                if (layersField == null)
                {
                    continue;
                }

                if (cmdName == "Up" || cmdName == "Down" || cmdName == "Left" || cmdName == "Right")
                {
                    int curLayers = Convert.ToInt32(layersField.GetValue(keyObj));
                    if ((curLayers & blockedMoveLayers) != 0)
                    {
                        int newLayers = curLayers & ~blockedMoveLayers;
                        object cloned = CloneKeySet(keyObj);
                        layersField.SetValue(cloned, Enum.ToObject(layersField.FieldType, newLayers));
                        removals.Add(entry.Key);
                        updates.Add(new KeyValuePair<object, object>(cloned, entry.Value));
                    }
                }
                else if (cmdName == "Back" && showMenuListEnumVal != null && codeField != null)
                {
                    var codeVal = codeField.GetValue(keyObj);
                    if (codeVal != null && codeVal.ToString() == "Escape")
                    {
                        int curLayers = Convert.ToInt32(layersField.GetValue(keyObj));
                        if ((curLayers & gamePlayLayer) != 0)
                        {
                            // Keep Escape -> Back for open windows (FullscreenUI, InventoryUI, etc.),
                            // but remove Layer.GamePlay so Escape never immediately pops "Do you really want to quit?"
                            int backLayers = curLayers & ~gamePlayLayer;
                            object clonedBack = CloneKeySet(keyObj);
                            layersField.SetValue(clonedBack, Enum.ToObject(layersField.FieldType, backLayers));
                            removals.Add(entry.Key);
                            updates.Add(new KeyValuePair<object, object>(clonedBack, entry.Value));

                            // Add Escape on Layer.GamePlay -> ShowMenuList (opens the Menu Drawer!)
                            object clonedMenu = CloneKeySet(keyObj);
                            layersField.SetValue(clonedMenu, Enum.ToObject(layersField.FieldType, gamePlayLayer));
                            updates.Add(new KeyValuePair<object, object>(clonedMenu, showMenuListEnumVal));
                        }
                    }
                }
            }

            if (removals.Count == 0 && updates.Count == 0)
            {
                return true;
            }
            for (int i = 0; i < removals.Count; i++)
            {
                keyMap.Remove(removals[i]);
            }
            for (int i = 0; i < updates.Count; i++)
            {
                keyMap[updates[i].Key] = updates[i].Value;
            }
            UnityEngine.Debug.LogWarning("[Durango-CE] Patched WASD-safe window layers & Escape -> Menu Drawer (" + updates.Count + " bindings).");
            return true;
        }

        /// <summary>
        /// Sets a comfortable zoomed-out default camera view once right after login (0.62 vs cramped 1.0 default)
        /// while leaving scroll-wheel zoom completely under the player's control afterward.
        /// </summary>
        private void EnsureLoginCameraZoom()
        {
            if (_appliedLoginZoom || _camCtrlType == null || Camera.main == null)
            {
                return;
            }
            try
            {
                if (_camHasInstance != null && !(bool)_camHasInstance.Invoke(null, null))
                {
                    return;
                }
                object ctrl = _camInstance != null ? _camInstance.Invoke(null, null) : null;
                if (ctrl == null)
                {
                    return;
                }
                if (_camSetZoom != null)
                {
                    // Smaller zoom value = farther out isometric camera (range: 0.42..2.2, default was 1.0)
                    _camSetZoom.Invoke(ctrl, new object[] { 0.62f });
                    if (_camLastZoomField != null)
                    {
                        _camLastZoomField.SetValue(null, 0.62f);
                    }
                    _appliedLoginZoom = true;
                    UnityEngine.Debug.LogWarning("[Durango-CE] Applied comfortable login camera zoom (0.62).");
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// Injects a highlighted special native NGUI "Exit" button cleanly at the bottom of the Menu window
        /// (MenuListGroupBase), positioned cleanly above the bottom tip &amp; banner, opening Durango's own
        /// native "Do you really want to quit?" MessageBox with Return to Main Menu / Exit Game / Cancel.
        /// </summary>
        private void EnsureMenuDrawerExitButton()
        {
            try
            {
                float now = Time.unscaledTime;
                if (_cachedMenuGroup == null)
                {
                    if (now < _nextMenuLookupAt)
                    {
                        return;
                    }
                    _nextMenuLookupAt = now + 0.40f;
                    _cachedMenuGroup = UIManager.FindScript<MenuListGroupBase>();
                }
                var menu = _cachedMenuGroup;
                if (menu == null)
                {
                    return;
                }
                if (_menuExitBtn != null)
                {
                    bool shouldShow = menu.IsOpened;
                    if (_menuExitBtn.activeSelf != shouldShow)
                    {
                        _menuExitBtn.SetActive(shouldShow);
                    }
                    return;
                }
                if (!menu.IsOpened)
                {
                    return;
                }

                Transform landList = GetFieldTransform(menu, "_landscapeMenuList");
                Transform portList = GetFieldTransform(menu, "_menuList");
                Transform parent = (landList != null && landList.gameObject.activeInHierarchy)
                    ? landList
                    : ((portList != null && portList.gameObject.activeInHierarchy) ? portList : menu.transform);

                var existing = parent.Find("DurangoCE_MenuExitBtn") ?? menu.transform.Find("DurangoCE_MenuExitBtn");
                if (existing != null)
                {
                    _menuExitBtn = existing.gameObject;
                    _menuExitBtn.SetActive(true);
                    return;
                }

                var styleLabel = FindStyleLabel();
                if (styleLabel == null)
                {
                    return;
                }

                int maxDepth = 40;
                var existingWidgets = parent.GetComponentsInChildren<UIWidget>(true);
                for (int i = 0; i < existingWidgets.Length; i++)
                {
                    if (existingWidgets[i] != null && existingWidgets[i].depth > maxDepth)
                    {
                        maxDepth = existingWidgets[i].depth;
                    }
                }

                // Bottom-center of Menu window, cleanly above the bottom tip bar and Play Instruments banner
                float activeHalfH = 360f;
                if (Singleton<UIManager>.HasInstance() && Singleton<UIManager>.Instance().UIRoot != null)
                {
                    activeHalfH = Singleton<UIManager>.Instance().UIRoot.activeHeight * 0.5f;
                }
                float targetY = Mathf.Clamp(-activeHalfH + 150f, -230f, -165f);

                var btn = CreateNativeNguiButton(
                    parent,
                    "DurangoCE_MenuExitBtn",
                    "Exit",
                    210,
                    46,
                    new Vector3(0f, targetY, 0f),
                    new Color(0.68f, 0.19f, 0.15f, 0.96f),   // highlighted special crimson-red fill
                    new Color(1.00f, 0.76f, 0.24f, 1.00f),   // warm Durango gold highlight border
                    Color.white,
                    21,
                    styleLabel,
                    delegate
                    {
                        try
                        {
                            menu.Close();
                        }
                        catch
                        {
                        }
                        ShowNativeQuitDialog();
                    },
                    maxDepth + 12);
                _menuExitBtn = btn;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning("[Durango-CE] EnsureMenuDrawerExitButton: " + ex.Message);
            }
        }

        private static void ShowNativeQuitDialog()
        {
            try
            {
                if (UIManager.MessageBox != null)
                {
                    UIManager.MessageBox.Show(
                        "Do you really want to quit?",
                        delegate(int idx)
                        {
                            if (idx == 0)
                            {
                                ReturnToMainMenu();
                            }
                            else if (idx == 1)
                            {
                                Application.Quit();
                            }
                        },
                        new MessageBox.Button("Return to Main Menu", PresetButton.Style.Solid),
                        new MessageBox.Button("Exit Game", PresetButton.Style.Border),
                        new MessageBox.Button("Cancel", PresetButton.Style.Border));
                    return;
                }
                ReturnToMainMenu();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("[Durango-CE] ShowNativeQuitDialog failed: " + ex);
            }
        }

        private Shader FindNguiShader()
        {
            if (_cachedNguiShader != null)
            {
                return _cachedNguiShader;
            }
            _cachedNguiShader = Shader.Find("Unlit/Transparent Colored") ?? Shader.Find("UI/Default");
            if (_cachedNguiShader != null)
            {
                return _cachedNguiShader;
            }
            try
            {
                if (Singleton<UIManager>.HasInstance() && Singleton<UIManager>.Instance().UIRoot != null)
                {
                    var uiRoot = Singleton<UIManager>.Instance().UIRoot;
                    var sprites = uiRoot.GetComponentsInChildren<UISprite>(true);
                    for (int i = 0; i < sprites.Length; i++)
                    {
                        if (sprites[i] != null && sprites[i].shader != null
                            && sprites[i].shader.name != null
                            && sprites[i].shader.name.IndexOf("Transparent Colored", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            _cachedNguiShader = sprites[i].shader;
                            return _cachedNguiShader;
                        }
                    }
                }
            }
            catch
            {
            }
            return _cachedNguiShader;
        }

        private GameObject CreateNativeNguiButton(
            Transform parent,
            string goName,
            string text,
            int width,
            int height,
            Vector3 localPos,
            Color fillColor,
            Color borderColor,
            Color textColor,
            int fontSize,
            UILabel styleLabel,
            Action onClick,
            int baseDepth = 40)
        {
            var nguiShader = FindNguiShader();
            var go = new GameObject(goName);
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one;

            var border = go.AddComponent<UITexture>();
            border.shader = nguiShader;
            border.mainTexture = GetWhiteTex();
            border.color = borderColor;
            border.width = width + 4;
            border.height = height + 4;
            border.depth = baseDepth;

            var fillGo = new GameObject("Fill");
            fillGo.layer = go.layer;
            fillGo.transform.SetParent(go.transform, false);
            var fill = fillGo.AddComponent<UITexture>();
            fill.shader = nguiShader;
            fill.mainTexture = GetWhiteTex();
            fill.color = fillColor;
            fill.width = width;
            fill.height = height;
            fill.depth = baseDepth + 1;

            var lblGo = new GameObject("Label");
            lblGo.layer = go.layer;
            lblGo.transform.SetParent(go.transform, false);
            var lbl = lblGo.AddComponent<UILabel>();
            ApplyLabelStyle(lbl, styleLabel, fontSize);
            lbl.text = text;
            lbl.color = textColor;
            lbl.effectStyle = textColor.r < 0.3f ? UILabel.Effect.None : UILabel.Effect.Outline;
            lbl.width = width - 16;
            lbl.height = height;
            lbl.depth = baseDepth + 2;

            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(width + 4, height + 4, 1f);

            var listener = UIEventListener.Get(go);
            listener.onClick = delegate { onClick?.Invoke(); };
            listener.onHover = delegate(GameObject _, bool over)
            {
                fill.color = over
                    ? new Color(
                        Mathf.Min(1f, fillColor.r + 0.14f),
                        Mathf.Min(1f, fillColor.g + 0.08f),
                        Mathf.Min(1f, fillColor.b + 0.06f),
                        fillColor.a)
                    : fillColor;
            };
            return go;
        }

        private Texture2D GetWhiteTex()
        {
            if (_whiteTex == null)
            {
                _whiteTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                _whiteTex.SetPixel(0, 0, Color.white);
                _whiteTex.Apply();
            }
            return _whiteTex;
        }

        private static void ReturnToMainMenu()
        {
            try
            {
                UnityEngine.Debug.LogWarning("[Durango-CE] Returning to Main Menu (Title)...");
                try
                {
                    var fe = Connections.Frontend;
                    if (fe != null)
                    {
                        var closeBool = fe.GetType().GetMethod(
                            "Close",
                            AllInstance,
                            null,
                            new[] { typeof(bool) },
                            null
                        );
                        if (closeBool != null)
                        {
                            closeBool.Invoke(fe, new object[] { false });
                        }
                        else
                        {
                            var closeNoArgs = fe.GetType().GetMethod("Close", AllInstance, null, Type.EmptyTypes, null)
                                ?? fe.GetType().GetMethod("Disconnect", AllInstance, null, Type.EmptyTypes, null);
                            closeNoArgs?.Invoke(fe, null);
                        }
                    }
                }
                catch
                {
                }
                if (Singleton<GameManager>.HasInstance())
                {
                    var gm = Singleton<GameManager>.Instance();
                    var m = typeof(GameManager).GetMethod("MoveToTitle", AllInstance, null, Type.EmptyTypes, null)
                        ?? typeof(GameManager).GetMethod("MoveToTitleLevel", AllInstance, null, Type.EmptyTypes, null);
                    if (m != null)
                    {
                        m.Invoke(gm, null);
                        return;
                    }
                }
                UnityEngine.SceneManagement.SceneManager.LoadScene("Title");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("[Durango-CE] ReturnToMainMenu failed: " + ex);
            }
        }

        private static string NormalizeLocalizedText(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return raw;
            }
            string s = raw;
            if (s.IndexOf("\\n", StringComparison.Ordinal) >= 0)
            {
                s = s.Replace("\\\\n", "\n").Replace("\\n", "\n");
            }
            if (s.IndexOf("<alert_icon/>", StringComparison.Ordinal) >= 0)
            {
                s = s.Replace("<alert_icon/> <alert_icon/>", "<alert_icon/>")
                     .Replace("<alert_icon/><alert_icon/>", "<alert_icon/>");
            }
            return s;
        }

        private void EnsureLocalizationSanitized()
        {
            try
            {
                string locale = LocalizeSystem.Locale ?? string.Empty;
                if (_localizeDictField != null && _sanitizedLocale != locale)
                {
                    var dict = _localizeDictField.GetValue(null) as Dictionary<string, string>;
                    if (dict != null && dict.Count > 0)
                    {
                        var updates = new List<KeyValuePair<string, string>>();
                        foreach (var kv in dict)
                        {
                            if (string.IsNullOrEmpty(kv.Value))
                            {
                                continue;
                            }
                            string cleaned = NormalizeLocalizedText(kv.Value);
                            if (!string.Equals(cleaned, kv.Value, StringComparison.Ordinal))
                            {
                                updates.Add(new KeyValuePair<string, string>(kv.Key, cleaned));
                            }
                        }
                        for (int i = 0; i < updates.Count; i++)
                        {
                            dict[updates[i].Key] = updates[i].Value;
                        }
                        _sanitizedLocale = locale;
                        if (updates.Count > 0)
                        {
                            UnityEngine.Debug.LogWarning(
                                $"[Durango-CE] Sanitized {updates.Count} localization entries for locale '{locale}'."
                            );
                        }
                    }
                }
            }
            catch
            {
            }

            float now = Time.unscaledTime;
            if (now < _nextUiSanitizeAt)
            {
                return;
            }
            _nextUiSanitizeAt = now + 0.50f;
            try
            {
                var labels = UnityEngine.Object.FindObjectsByType<UILabel>();
                for (int i = 0; i < labels.Length; i++)
                {
                    var lbl = labels[i];
                    if (lbl == null || !lbl.gameObject.activeInHierarchy)
                    {
                        continue;
                    }
                    string text = lbl.text;
                    if (string.IsNullOrEmpty(text))
                    {
                        continue;
                    }
                    if (text.IndexOf("\\n", StringComparison.Ordinal) < 0 &&
                        text.IndexOf("<alert_icon/> <alert_icon/>", StringComparison.Ordinal) < 0 &&
                        text.IndexOf("<alert_icon/><alert_icon/>", StringComparison.Ordinal) < 0)
                    {
                        continue;
                    }
                    if (lbl.GetComponentInParent<UIInput>() != null)
                    {
                        continue;
                    }
                    string cleaned = NormalizeLocalizedText(text);
                    if (!string.Equals(cleaned, text, StringComparison.Ordinal))
                    {
                        lbl.text = cleaned;
                    }
                }
            }
            catch
            {
            }
        }

        private void EnsureNamePageFeedback()
        {
            try
            {
                if (_hookedNamePage != null)
                {
                    if (_nameWarnLabel != null && _nameWarnLabel.gameObject.activeSelf)
                    {
                        var input = GetFieldValue<UIInput>(_hookedNamePage, "_textInput");
                        string trimmed = input != null && input.value != null ? input.value.Trim() : string.Empty;
                        if (trimmed.Length > 0 && trimmed.Length <= 20)
                        {
                            _nameWarnLabel.gameObject.SetActive(false);
                        }
                    }
                    return;
                }

                float now = Time.unscaledTime;
                if (now < _nextNamePageCheckAt)
                {
                    return;
                }
                _nextNamePageCheckAt = now + 0.25f;

                var page = UnityEngine.Object.FindAnyObjectByType<EditPlayerNamePage>();
                if (page == null)
                {
                    return;
                }

                var finishBtn = GetFieldValue<SelectableButton>(page, "_finishButton");
                if (finishBtn == null)
                {
                    return;
                }

                var listener = UIEventListener.Get(finishBtn.gameObject);
                listener.onClick -= OnNameFinishClicked;
                listener.onClick += OnNameFinishClicked;
                _hookedNamePage = page;
            }
            catch
            {
            }
        }

        private void OnNameFinishClicked(GameObject go)
        {
            try
            {
                if (_hookedNamePage == null)
                {
                    return;
                }
                var finishBtn = GetFieldValue<SelectableButton>(_hookedNamePage, "_finishButton");
                if (finishBtn != null && finishBtn.Disabled)
                {
                    return;
                }
                var input = GetFieldValue<UIInput>(_hookedNamePage, "_textInput");
                string raw = input != null && input.value != null ? input.value : string.Empty;
                string trimmed = raw.Trim();

                string warnMsg = null;
                if (trimmed.Length == 0)
                {
                    warnMsg = "Please enter a character name before clicking OK.";
                }
                else if (trimmed.Length > 20)
                {
                    warnMsg = "Character name must be 20 characters or fewer.";
                }

                if (warnMsg == null)
                {
                    if (_nameWarnLabel != null && _nameWarnLabel.gameObject.activeSelf)
                    {
                        _nameWarnLabel.gameObject.SetActive(false);
                    }
                    return;
                }

                ShowInlineNameWarning(warnMsg);
                if (UIManager.MessageBox != null)
                {
                    UIManager.MessageBox.Show(
                        "Character Name Required",
                        warnMsg,
                        (Action)null,
                        "OK"
                    );
                }
            }
            catch
            {
            }
        }

        private void ShowInlineNameWarning(string message)
        {
            if (_hookedNamePage == null)
            {
                return;
            }
            if (_nameWarnLabel == null)
            {
                var helpLabel = GetFieldValue<UILabel>(_hookedNamePage, "_nameRoleHelpLabel");
                Transform parent = helpLabel != null ? helpLabel.transform.parent : _hookedNamePage.transform;
                var warnGo = new GameObject("DurangoCE_NameWarn");
                warnGo.layer = parent.gameObject.layer;
                warnGo.transform.SetParent(parent, false);
                var pos = helpLabel != null ? helpLabel.transform.localPosition : Vector3.zero;
                warnGo.transform.localPosition = new Vector3(pos.x, pos.y + 34f, pos.z);
                warnGo.transform.localScale = Vector3.one;

                var lbl = warnGo.AddComponent<UILabel>();
                if (helpLabel != null)
                {
                    lbl.trueTypeFont = helpLabel.trueTypeFont;
                    lbl.bitmapFont = helpLabel.bitmapFont;
                    lbl.depth = helpLabel.depth + 5;
                    lbl.width = Mathf.Max(helpLabel.width, 460);
                }
                else
                {
                    lbl.depth = 60;
                    lbl.width = 460;
                }
                lbl.fontSize = 18;
                lbl.color = new Color(1f, 0.42f, 0.36f, 1f);
                lbl.effectStyle = UILabel.Effect.Outline;
                lbl.effectColor = new Color(0f, 0f, 0f, 0.92f);
                lbl.alignment = NGUIText.Alignment.Center;
                lbl.overflowMethod = UILabel.Overflow.ShrinkContent;
                _nameWarnLabel = lbl;
            }
            _nameWarnLabel.text = message;
            _nameWarnLabel.gameObject.SetActive(true);
        }
    }
}
