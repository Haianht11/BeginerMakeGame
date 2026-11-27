using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class GothicUIController : MonoBehaviour
{
    public enum UIState
    {
        MainMenu,
        Playing,
        Paused,
        EndDefeat,
        EndVictory
    }

    private static bool skipStartMenuNextLoad;

    [Header("HUD References")]
    [SerializeField] private Image lowHpVignette;
    [SerializeField] private GameObject hudRoot;
    [SerializeField] private HealthBar playerHealthBar;
    [SerializeField] private Image staminaFillImage;
    [SerializeField] private Image[] comboPips;
    [SerializeField] private Sprite comboPipOffSprite;
    [SerializeField] private Sprite comboPipOnSprite;
    [SerializeField] private Text areaTitleText;

    [Header("Boss Card References")]
    [SerializeField] private GameObject bossCardRoot;
    [SerializeField] private Text bossNameText;
    [SerializeField] private HealthBar bossHealthBar;

    [Header("Screen Panels & Modals")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject pauseModal;
    [SerializeField] private GameObject endModal;
    [SerializeField] private Text endTitleText;
    [SerializeField] private Text endSubtitleText;

    [Header("Menu Buttons & Styling")]
    [SerializeField] private Button[] mainMenuButtons;
    [SerializeField] private Image[] mainMenuButtonBackgrounds;
    [SerializeField] private Image[] mainMenuCursors;
    [SerializeField] private Text[] mainMenuButtonLabels;
    [SerializeField] private Button[] pauseButtons;
    [SerializeField] private Image[] pauseButtonBackgrounds;
    [SerializeField] private Image[] pauseCursors;
    [SerializeField] private Text[] pauseButtonLabels;
    [SerializeField] private Button[] endButtons;
    [SerializeField] private Image[] endButtonBackgrounds;
    [SerializeField] private Image[] endCursors;
    [SerializeField] private Text[] endButtonLabels;
    [SerializeField] private Sprite buttonNormalSprite;
    [SerializeField] private Sprite buttonHoverSprite;
    [SerializeField] private Color buttonNormalTextColor = new Color(0.890f, 0.871f, 0.839f, 1f);
    [SerializeField] private Color buttonHoverTextColor = new Color(0.596f, 0.910f, 0.784f, 1f);
    [SerializeField] private Color defeatTitleColor = new Color(0.894f, 0.361f, 0.431f, 1f);
    [SerializeField] private Color victoryTitleColor = new Color(0.596f, 0.910f, 0.784f, 1f);

    [Header("Scene References")]
    [SerializeField] private Player player;
    [SerializeField] private DoomKnightBoss boss;
    [SerializeField] private DungeonTrainingDummy trainingDummy;
    [SerializeField] private LevelGoal levelGoal;

    [Header("Configuration")]
    [SerializeField] private string areaTitle = "HẦM NGỤC TỐI";
    [SerializeField] private string bossDisplayName = "BOSS · HẮC KỴ SĨ";
    [SerializeField] private bool startAtMainMenu = true;
    [SerializeField] private string[] sceneCycleOrder = new[] { "SampleScene", "DungeonDemo", "BossArenaDemo" };

    [Header("Combat HUD Tuning")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float jumpStaminaCost = 14f;
    [SerializeField] private float attackStaminaCost = 18f;
    [SerializeField] private float staminaRecoveryDelay = 0.45f;
    [SerializeField] private float staminaRecoveryPerSecond = 28f;
    [SerializeField] private float comboDecayDuration = 1.6f;
    [SerializeField] private float lowHpThreshold = 0.25f;

    private UIState currentState = UIState.Playing;
    private int selectedIndex;
    private float currentStamina;
    private float staminaDelayTimer;
    private int currentComboCount;
    private float comboDecayTimer;
    private float currentPlayerNormalized = 1f;
    private bool buttonsBound;

    public UIState CurrentState => currentState;

    private void Awake()
    {
        ResolveSceneReferences();
        currentStamina = maxStamina;

        if (areaTitleText != null && !string.IsNullOrEmpty(areaTitle))
            areaTitleText.text = areaTitle;
        if (bossNameText != null && !string.IsNullOrEmpty(bossDisplayName))
            bossNameText.text = bossDisplayName;

        BindMenuGroup(mainMenuButtons);
        BindMenuGroup(pauseButtons);
        BindMenuGroup(endButtons);
        buttonsBound = true;
    }

    private void OnEnable()
    {
        SubscribeGameplayEvents();
    }

    private void Start()
    {
        if (player != null)
        {
            player.SuppressAutoRespawn = true;
            OnPlayerHealthChanged(player.CurrentHealth, player.MaxHealth);
        }
        else
        {
            OnPlayerHealthChanged(100f, 100f);
        }

        if (boss != null)
            boss.SuppressAutoRespawn = true;
        if (trainingDummy != null)
            trainingDummy.SuppressAutoReset = true;

        RefreshBossCardVisibilityAndHealth();
        UpdateStaminaVisual();
        UpdateComboPips(0);

        bool shouldOpenMainMenu = startAtMainMenu && !skipStartMenuNextLoad;
        skipStartMenuNextLoad = false;
        SetState(shouldOpenMainMenu ? UIState.MainMenu : UIState.Playing);
    }

    private void OnDisable()
    {
        UnsubscribeGameplayEvents();
        if (Application.isPlaying)
            Time.timeScale = 1f;
    }

    private void OnDestroy()
    {
        if (Application.isPlaying)
            Time.timeScale = 1f;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            if (currentState == UIState.Playing)
            {
                SetState(UIState.Paused);
                return;
            }

            if (currentState == UIState.Paused)
            {
                SetState(UIState.Playing);
                return;
            }
        }

        if (currentState == UIState.Playing)
            UpdateCombatSimulation();
        else
            HandleMenuKeyboardInput(keyboard);

        UpdateLowHpVignettePulse();
    }

    public void ApplyPreviewState(
        float playerHp,
        float playerGhostHp,
        float stamina,
        int comboCount,
        float bossHp,
        bool showMainMenu,
        bool showPause,
        bool showEndDefeat,
        bool showEndVictory)
    {
        float resolvedMaxPlayerHp = player != null && player.MaxHealth > 0f ? player.MaxHealth : 100f;
        bool playerIsNormalized = playerHp <= 1f && playerGhostHp <= 1f;
        float resolvedPlayerHp = playerIsNormalized ? Mathf.Clamp01(playerHp) * resolvedMaxPlayerHp : Mathf.Max(0f, playerHp);
        float resolvedGhostHp = playerIsNormalized ? Mathf.Clamp01(playerGhostHp) * resolvedMaxPlayerHp : Mathf.Max(0f, playerGhostHp);
        currentPlayerNormalized = resolvedMaxPlayerHp > 0f ? Mathf.Clamp01(resolvedPlayerHp / resolvedMaxPlayerHp) : 0f;

        if (playerHealthBar != null)
            playerHealthBar.SetPreviewHealth(resolvedPlayerHp, resolvedMaxPlayerHp, resolvedGhostHp);

        currentStamina = stamina > 1f
            ? Mathf.Clamp(stamina, 0f, maxStamina)
            : Mathf.Clamp01(stamina) * maxStamina;
        UpdateStaminaVisual();
        UpdateComboPips(Mathf.Clamp(comboCount, 0, 3));

        if (areaTitleText != null && !string.IsNullOrEmpty(areaTitle))
            areaTitleText.text = areaTitle;
        if (bossNameText != null && !string.IsNullOrEmpty(bossDisplayName))
            bossNameText.text = bossDisplayName;

        bool hasBossTarget = boss != null || trainingDummy != null || bossHp > 0f;
        if (bossCardRoot != null)
            bossCardRoot.SetActive(hasBossTarget && !showMainMenu);

        if (bossHealthBar != null && hasBossTarget)
        {
            float resolvedMaxBossHp = boss != null && boss.MaxHealth > 0f
                ? boss.MaxHealth
                : (trainingDummy != null && trainingDummy.MaxHealth > 0f ? trainingDummy.MaxHealth : 200f);
            float resolvedBossHp = bossHp <= 1f ? Mathf.Clamp01(bossHp) * resolvedMaxBossHp : Mathf.Max(0f, bossHp);
            bossHealthBar.SetPreviewHealth(resolvedBossHp, resolvedMaxBossHp, resolvedBossHp);
        }

        if (hudRoot != null)
            hudRoot.SetActive(!showMainMenu);
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(showMainMenu);
        if (pauseModal != null)
            pauseModal.SetActive(showPause);
        if (endModal != null)
            endModal.SetActive(showEndDefeat || showEndVictory);

        if (showEndDefeat)
            ApplyEndCopy("NGÃ GỤC", "HỒI SINH TẠI ĐIỂM KHỞI ĐẦU", "HỒI SINH", false);
        else if (showEndVictory)
            ApplyEndCopy(
                "VƯỢT ẢI THÀNH CÔNG",
                (boss != null || trainingDummy != null) ? "KẺ THÙ ĐÃ BỊ TIÊU DIỆT" : "ĐÃ ĐẾN CỔNG KẾT THÚC",
                "MÀN TIẾP THEO",
                true);

        if (lowHpVignette != null)
        {
            bool showVignette = !showMainMenu && currentPlayerNormalized > 0f && currentPlayerNormalized <= lowHpThreshold;
            lowHpVignette.gameObject.SetActive(showVignette);
            if (showVignette)
                SetVignetteAlpha(0.75f);
        }

        selectedIndex = 0;
        if (showMainMenu)
            RefreshMenuGroupVisuals(mainMenuButtons, mainMenuButtonBackgrounds, mainMenuCursors, mainMenuButtonLabels, 0);
        if (showPause)
            RefreshMenuGroupVisuals(pauseButtons, pauseButtonBackgrounds, pauseCursors, pauseButtonLabels, 0);
        if (showEndDefeat || showEndVictory)
            RefreshMenuGroupVisuals(endButtons, endButtonBackgrounds, endCursors, endButtonLabels, 0);
    }

    private void ResolveSceneReferences()
    {
        if (player == null)
            player = UnityEngine.Object.FindAnyObjectByType<Player>();
        if (boss == null)
            boss = UnityEngine.Object.FindAnyObjectByType<DoomKnightBoss>();
        if (trainingDummy == null)
            trainingDummy = UnityEngine.Object.FindAnyObjectByType<DungeonTrainingDummy>();
        if (levelGoal == null)
            levelGoal = UnityEngine.Object.FindAnyObjectByType<LevelGoal>();
    }

    private void SubscribeGameplayEvents()
    {
        if (player != null)
        {
            player.HealthChanged += OnPlayerHealthChanged;
            player.Jumped += OnPlayerJumped;
            player.AttackStarted += OnPlayerAttackStarted;
            player.PlayerDefeated += OnPlayerDefeated;
        }

        if (boss != null)
        {
            boss.HealthChanged += OnBossHealthChanged;
            boss.BossDefeated += OnBossDefeated;
        }
        else if (trainingDummy != null)
        {
            trainingDummy.HealthChanged += OnBossHealthChanged;
            trainingDummy.DummyDefeated += OnDummyDefeated;
        }

        if (levelGoal != null)
            levelGoal.GoalReached += OnGoalReached;
    }

    private void UnsubscribeGameplayEvents()
    {
        if (player != null)
        {
            player.HealthChanged -= OnPlayerHealthChanged;
            player.Jumped -= OnPlayerJumped;
            player.AttackStarted -= OnPlayerAttackStarted;
            player.PlayerDefeated -= OnPlayerDefeated;
        }

        if (boss != null)
        {
            boss.HealthChanged -= OnBossHealthChanged;
            boss.BossDefeated -= OnBossDefeated;
        }

        if (trainingDummy != null)
        {
            trainingDummy.HealthChanged -= OnBossHealthChanged;
            trainingDummy.DummyDefeated -= OnDummyDefeated;
        }

        if (levelGoal != null)
            levelGoal.GoalReached -= OnGoalReached;
    }

    private void SetState(UIState nextState)
    {
        currentState = nextState;
        selectedIndex = 0;

        bool isMainMenu = nextState == UIState.MainMenu;
        bool isPlaying = nextState == UIState.Playing;
        bool isPaused = nextState == UIState.Paused;
        bool isEnd = nextState == UIState.EndDefeat || nextState == UIState.EndVictory;

        Time.timeScale = isPaused ? 0f : 1f;

        if (player != null)
            player.GameplayInputBlocked = !isPlaying;
        if (boss != null)
            boss.GameplayPaused = !isPlaying;

        if (hudRoot != null)
            hudRoot.SetActive(!isMainMenu);
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(isMainMenu);
        if (pauseModal != null)
            pauseModal.SetActive(isPaused);
        if (endModal != null)
            endModal.SetActive(isEnd);

        if (isPlaying)
            RefreshBossCardVisibilityAndHealth();

        RefreshActiveMenuVisuals();
        UpdateLowHpVignettePulse();
    }

    private void RefreshBossCardVisibilityAndHealth()
    {
        bool hasBossOrDummy = boss != null || trainingDummy != null;
        if (bossCardRoot != null)
            bossCardRoot.SetActive(hasBossOrDummy);

        if (boss != null)
            OnBossHealthChanged(boss.CurrentHealth, boss.MaxHealth);
        else if (trainingDummy != null)
            OnBossHealthChanged(trainingDummy.CurrentHealth, trainingDummy.MaxHealth);
    }

    private void UpdateCombatSimulation()
    {
        if (staminaDelayTimer > 0f)
        {
            staminaDelayTimer -= Time.deltaTime;
        }
        else if (currentStamina < maxStamina)
        {
            currentStamina = Mathf.Min(maxStamina, currentStamina + staminaRecoveryPerSecond * Time.deltaTime);
            UpdateStaminaVisual();
        }

        if (currentComboCount > 0)
        {
            comboDecayTimer -= Time.deltaTime;
            if (comboDecayTimer <= 0f)
            {
                currentComboCount = 0;
                UpdateComboPips(0);
            }
        }
    }

    private void UpdateLowHpVignettePulse()
    {
        if (lowHpVignette == null)
            return;

        bool active = currentState != UIState.MainMenu
            && currentPlayerNormalized > 0f
            && currentPlayerNormalized <= lowHpThreshold;

        lowHpVignette.gameObject.SetActive(active);
        if (active)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
            SetVignetteAlpha(Mathf.Lerp(0.45f, 0.9f, pulse));
        }
    }

    private void SetVignetteAlpha(float alpha)
    {
        Color c = lowHpVignette.color;
        c.a = alpha;
        lowHpVignette.color = c;
    }

    private void UpdateStaminaVisual()
    {
        if (staminaFillImage != null)
            staminaFillImage.fillAmount = maxStamina > 0f ? Mathf.Clamp01(currentStamina / maxStamina) : 0f;
    }

    private void UpdateComboPips(int count)
    {
        if (comboPips == null)
            return;

        for (int i = 0; i < comboPips.Length; i++)
        {
            Image pip = comboPips[i];
            if (pip == null)
                continue;

            bool lit = i < count;
            if (lit && comboPipOnSprite != null)
                pip.sprite = comboPipOnSprite;
            else if (!lit && comboPipOffSprite != null)
                pip.sprite = comboPipOffSprite;
        }
    }

    private void OnPlayerHealthChanged(float current, float max)
    {
        currentPlayerNormalized = max > 0f ? Mathf.Clamp01(current / max) : 0f;
        if (playerHealthBar != null)
            playerHealthBar.UpdateHealth(current, max);
        UpdateLowHpVignettePulse();
    }

    private void OnBossHealthChanged(float current, float max)
    {
        if (bossHealthBar != null)
            bossHealthBar.UpdateHealth(current, max);
    }

    private void OnPlayerJumped()
    {
        if (currentState != UIState.Playing)
            return;

        currentStamina = Mathf.Max(0f, currentStamina - jumpStaminaCost);
        staminaDelayTimer = staminaRecoveryDelay;
        UpdateStaminaVisual();
    }

    private void OnPlayerAttackStarted(int step)
    {
        if (currentState != UIState.Playing)
            return;

        currentStamina = Mathf.Max(0f, currentStamina - attackStaminaCost);
        staminaDelayTimer = staminaRecoveryDelay;
        UpdateStaminaVisual();

        currentComboCount = Mathf.Clamp(currentComboCount + 1, 1, 3);
        comboDecayTimer = comboDecayDuration;
        UpdateComboPips(currentComboCount);
    }

    private void OnPlayerDefeated()
    {
        if (currentState != UIState.Playing)
            return;

        ApplyEndCopy("NGÃ GỤC", "HỒI SINH TẠI ĐIỂM KHỞI ĐẦU", "HỒI SINH", false);
        SetState(UIState.EndDefeat);
    }

    private void OnBossDefeated()
    {
        if (currentState != UIState.Playing)
            return;

        ApplyEndCopy("VƯỢT ẢI THÀNH CÔNG", "KẺ THÙ ĐÃ BỊ TIÊU DIỆT", "MÀN TIẾP THEO", true);
        SetState(UIState.EndVictory);
    }

    private void OnDummyDefeated()
    {
        if (currentState != UIState.Playing)
            return;

        ApplyEndCopy("VƯỢT ẢI THÀNH CÔNG", "KẺ THÙ ĐÃ BỊ TIÊU DIỆT", "MÀN TIẾP THEO", true);
        SetState(UIState.EndVictory);
    }

    private void OnGoalReached()
    {
        if (currentState != UIState.Playing)
            return;

        ApplyEndCopy("VƯỢT ẢI THÀNH CÔNG", "ĐÃ ĐẾN CỔNG KẾT THÚC", "MÀN TIẾP THEO", true);
        SetState(UIState.EndVictory);
    }

    private void ApplyEndCopy(string title, string subtitle, string primaryButtonLabel, bool isVictory)
    {
        if (endTitleText != null)
        {
            endTitleText.text = title;
            endTitleText.color = isVictory ? victoryTitleColor : defeatTitleColor;
        }
        if (endSubtitleText != null)
            endSubtitleText.text = subtitle;

        if (endButtons != null && endButtons.Length > 0)
        {
            Text firstLabel = ResolveLabel(endButtons, endButtonLabels, 0);
            if (firstLabel != null)
                firstLabel.text = primaryButtonLabel;
        }
    }

    private void BindMenuGroup(Button[] buttons)
    {
        if (buttonsBound || buttons == null)
            return;

        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null)
                continue;

            Navigation nav = button.navigation;
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;

            int capturedIndex = i;
            button.onClick.AddListener(() =>
            {
                SelectMenuIndex(capturedIndex);
                ActivateMenuIndex(capturedIndex);
            });

            EventTrigger trigger = button.GetComponent<EventTrigger>();
            if (trigger == null)
                trigger = button.gameObject.AddComponent<EventTrigger>();

            var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enterEntry.callback.AddListener(_ => SelectMenuIndex(capturedIndex));
            trigger.triggers.Add(enterEntry);
        }
    }

    private void HandleMenuKeyboardInput(Keyboard keyboard)
    {
        if (keyboard == null)
            return;

        Button[] activeButtons = GetActiveMenuButtons();
        if (activeButtons == null || activeButtons.Length == 0)
            return;

        if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame)
        {
            selectedIndex = (selectedIndex - 1 + activeButtons.Length) % activeButtons.Length;
            RefreshActiveMenuVisuals();
        }
        else if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame)
        {
            selectedIndex = (selectedIndex + 1) % activeButtons.Length;
            RefreshActiveMenuVisuals();
        }
        else if (keyboard.enterKey.wasPressedThisFrame
            || keyboard.numpadEnterKey.wasPressedThisFrame
            || keyboard.spaceKey.wasPressedThisFrame)
        {
            ActivateMenuIndex(selectedIndex);
        }
    }

    private void SelectMenuIndex(int index)
    {
        Button[] activeButtons = GetActiveMenuButtons();
        if (activeButtons == null || activeButtons.Length == 0)
            return;

        selectedIndex = Mathf.Clamp(index, 0, activeButtons.Length - 1);
        RefreshActiveMenuVisuals();
    }

    private void ActivateMenuIndex(int index)
    {
        GetActiveMenuGroup(out Button[] buttons, out _, out _, out Text[] labels);
        if (buttons == null || index < 0 || index >= buttons.Length)
            return;

        Text label = ResolveLabel(buttons, labels, index);
        string actionKey = label != null ? label.text.Trim().ToUpperInvariant() : string.Empty;

        if (actionKey == "BẮT ĐẦU" || actionKey == "TIẾP TỤC")
        {
            SetState(UIState.Playing);
            return;
        }

        if (actionKey == "ĐỔI MÀN" || actionKey == "MÀN TIẾP THEO")
        {
            LoadNextSceneInCycle();
            return;
        }

        if (actionKey == "HỒI SINH")
        {
            RespawnPlayerInCurrentScene();
            return;
        }

        if (actionKey == "CHƠI LẠI")
        {
            ReloadCurrentScene();
            return;
        }

        if (actionKey == "VỀ MENU")
        {
            ReturnToMainMenu();
            return;
        }

        if (actionKey == "THOÁT")
        {
            QuitGame();
            return;
        }

        // Positional fallback if labels were customized
        switch (currentState)
        {
            case UIState.MainMenu:
                if (index == 0) SetState(UIState.Playing);
                else if (index == 1) LoadNextSceneInCycle();
                else if (index == 2) ReloadCurrentScene();
                else QuitGame();
                break;
            case UIState.Paused:
                if (index == 0) SetState(UIState.Playing);
                else if (index == 1) LoadNextSceneInCycle();
                else if (index == 2) ReturnToMainMenu();
                else QuitGame();
                break;
            case UIState.EndDefeat:
                if (index == 0) RespawnPlayerInCurrentScene();
                else if (index == 1) ReloadCurrentScene();
                else if (index == 2) ReturnToMainMenu();
                else QuitGame();
                break;
            case UIState.EndVictory:
                if (index == 0) LoadNextSceneInCycle();
                else if (index == 1) ReloadCurrentScene();
                else if (index == 2) ReturnToMainMenu();
                else QuitGame();
                break;
        }
    }

    private void RespawnPlayerInCurrentScene()
    {
        if (player != null)
            player.RespawnNow();
        if (boss != null)
            boss.ResetBoss();
        if (trainingDummy != null)
            trainingDummy.ResetDummy();

        ResetCombatMeters();
        SetState(UIState.Playing);
    }

    private void ReturnToMainMenu()
    {
        skipStartMenuNextLoad = false;
        if (player != null)
            player.RespawnNow();
        if (boss != null)
            boss.ResetBoss();
        if (trainingDummy != null)
            trainingDummy.ResetDummy();
        if (levelGoal != null)
            levelGoal.ResetGoal();

        ResetCombatMeters();
        SetState(UIState.MainMenu);
    }

    private void ResetCombatMeters()
    {
        currentStamina = maxStamina;
        staminaDelayTimer = 0f;
        currentComboCount = 0;
        comboDecayTimer = 0f;
        UpdateStaminaVisual();
        UpdateComboPips(0);
    }

    private void ReloadCurrentScene()
    {
        skipStartMenuNextLoad = true;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void LoadNextSceneInCycle()
    {
        if (sceneCycleOrder == null || sceneCycleOrder.Length == 0)
        {
            ReloadCurrentScene();
            return;
        }

        string activeName = SceneManager.GetActiveScene().name;
        int currentIndex = -1;
        for (int i = 0; i < sceneCycleOrder.Length; i++)
        {
            if (string.Equals(sceneCycleOrder[i], activeName, StringComparison.OrdinalIgnoreCase))
            {
                currentIndex = i;
                break;
            }
        }

        int nextIndex = (currentIndex + 1) % sceneCycleOrder.Length;
        string nextScene = sceneCycleOrder[nextIndex];
        if (string.IsNullOrEmpty(nextScene))
        {
            ReloadCurrentScene();
            return;
        }

        skipStartMenuNextLoad = true;
        Time.timeScale = 1f;
        SceneManager.LoadScene(nextScene);
    }

    private static void QuitGame()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private Button[] GetActiveMenuButtons()
    {
        GetActiveMenuGroup(out Button[] buttons, out _, out _, out _);
        return buttons;
    }

    private void RefreshActiveMenuVisuals()
    {
        GetActiveMenuGroup(out Button[] buttons, out Image[] backgrounds, out Image[] cursors, out Text[] labels);
        RefreshMenuGroupVisuals(buttons, backgrounds, cursors, labels, selectedIndex);
    }

    private void GetActiveMenuGroup(out Button[] buttons, out Image[] backgrounds, out Image[] cursors, out Text[] labels)
    {
        switch (currentState)
        {
            case UIState.MainMenu:
                buttons = mainMenuButtons;
                backgrounds = mainMenuButtonBackgrounds;
                cursors = mainMenuCursors;
                labels = mainMenuButtonLabels;
                break;
            case UIState.Paused:
                buttons = pauseButtons;
                backgrounds = pauseButtonBackgrounds;
                cursors = pauseCursors;
                labels = pauseButtonLabels;
                break;
            case UIState.EndDefeat:
            case UIState.EndVictory:
                buttons = endButtons;
                backgrounds = endButtonBackgrounds;
                cursors = endCursors;
                labels = endButtonLabels;
                break;
            default:
                buttons = null;
                backgrounds = null;
                cursors = null;
                labels = null;
                break;
        }
    }

    private void RefreshMenuGroupVisuals(
        Button[] buttons,
        Image[] backgrounds,
        Image[] cursors,
        Text[] labels,
        int activeIndex)
    {
        if (buttons == null)
            return;

        for (int i = 0; i < buttons.Length; i++)
        {
            bool isSelected = i == activeIndex;

            Image background = ResolveArrayElement(backgrounds, i);
            if (background == null && buttons[i] != null)
                background = buttons[i].GetComponent<Image>();
            if (background != null)
            {
                if (isSelected)
                {
                    if (buttonHoverSprite != null)
                        background.sprite = buttonHoverSprite;
                    Color bgColor = background.color;
                    bgColor.a = 1f;
                    background.color = bgColor;
                }
                else if (buttonNormalSprite != null)
                {
                    background.sprite = buttonNormalSprite;
                    Color bgColor = background.color;
                    bgColor.a = 1f;
                    background.color = bgColor;
                }
                else
                {
                    background.sprite = null;
                    Color bgColor = background.color;
                    bgColor.a = 0f;
                    background.color = bgColor;
                }
            }

            Image cursor = ResolveArrayElement(cursors, i);
            if (cursor != null)
                cursor.enabled = isSelected;

            Text label = ResolveLabel(buttons, labels, i);
            if (label != null)
                label.color = isSelected ? buttonHoverTextColor : buttonNormalTextColor;
        }
    }

    private static Text ResolveLabel(Button[] buttons, Text[] labels, int index)
    {
        Text label = ResolveArrayElement(labels, index);
        if (label == null && buttons != null && index >= 0 && index < buttons.Length && buttons[index] != null)
            label = buttons[index].GetComponentInChildren<Text>(true);
        return label;
    }

    private static T ResolveArrayElement<T>(T[] array, int index) where T : UnityEngine.Object
    {
        if (array == null || index < 0 || index >= array.Length)
            return null;
        return array[index];
    }
}
