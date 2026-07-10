using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using StandaloneInputModule = UnityEngine.EventSystems.StandaloneInputModule;

public sealed class OneButtonGamePrefabVer : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField] private Vector2 deadRangeFrames = new Vector2(400f, 700f);
    [SerializeField] private float restartHoldSeconds = 3f;
    [SerializeField] private string titleSceneName = "TitleScene";

    [Header("View References")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image closedEyeImage;
    [SerializeField] private Image smallGhostImage;
    [SerializeField] private Image bigGhostImage;
    [SerializeField] private Image attackImage;
    [SerializeField] private Image resultTextImage;
    [SerializeField] private Button returnToTitleButton;

    [Header("Sprite Frames")]
    [SerializeField] private OneButtonSpriteFrames backgroundFrames;
    [SerializeField] private OneButtonSpriteFrames closedEyeFrames;
    [SerializeField] private OneButtonSpriteFrames smallGhostFrames;
    [SerializeField] private OneButtonSpriteFrames resultTextFrames;

    [Header("Audio")]
    [SerializeField] private OneButtonAudioManager audioManager;

    private const float FramesPerSecond = 60f;
    private const float AttackDuration = 200f / FramesPerSecond;
    private const float GameOverPromptDelay = 240f / FramesPerSecond;
    private const int OpenEyeFrame = 14;
    private const int ClosedEyeFrame = 0;

    private float range;
    private float deadRange;

    private bool wasPressed;
    private bool isGameOver;
    private bool gameOverPromptPlayed;
    private bool restartPromptPlayed;

    private float gameOverTimer;
    private float attackTimer;
    private float backgroundTimer;
    private float currentEyeFrameValue = OpenEyeFrame;
    private int backgroundFrame;
    private int currentEyeFrame = OpenEyeFrame;
    private bool introTransitionActive = true;
    private bool isReturningToTitle;

    private void Awake()
    {
        Screen.orientation = ScreenOrientation.LandscapeLeft;

        // 参照が設定されていない場合は、シーン内のオブジェクトを検索して自動的にバインドする。
        BindMissingReferences();
        if (!HasRequiredReferences())
        {
            enabled = false;
            return;
        }

        ConfigureCamera();
        EnsureUiEventSystem();
        EnsureCanvasIsInteractive();
        BindReturnToTitleButton();

        deadRange = 500f;
        SetEyeFrame(ClosedEyeFrame);
        ResetRoundVisuals();

        audioManager.PlayBgm();
    }

    private void BindMissingReferences()
    {
        backgroundImage ??= FindImage("Background Image");
        closedEyeImage ??= FindImage("Closed Eye Image");
        smallGhostImage ??= FindImage("Small Ghost Image");
        bigGhostImage ??= FindImage("Big Ghost Image");
        attackImage ??= FindImage("Attack Image");
        resultTextImage ??= FindImage("Result Text Image");

        backgroundFrames ??= FindSpriteFrames("Background Image");
        closedEyeFrames ??= FindSpriteFrames("Closed Eye Image");
        smallGhostFrames ??= FindSpriteFrames("Small Ghost Image");
        resultTextFrames ??= FindSpriteFrames("Result Text Image");

        // AudioManagerの参照を取得する。
        // OneButtonAudioManagerがシーンに存在しない場合は、非アクティブなオブジェクトも含めて検索する。
        audioManager = OneButtonAudioManager.Instance != null
            ? OneButtonAudioManager.Instance
            : FindFirstObjectByType<OneButtonAudioManager>(FindObjectsInactive.Include);
        returnToTitleButton ??= FindButton("ReturnToTitleButton");
    }

    private static Button FindButton(string objectName)
    {
        foreach (Button button in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (button.gameObject.name == objectName)
            {
                return button;
            }
        }

        return null;
    }

    private static Image FindImage(string objectName)
    {
        foreach (Image image in FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (image.gameObject.name == objectName)
            {
                return image;
            }
        }

        return null;
    }

    private static OneButtonSpriteFrames FindSpriteFrames(string objectName)
    {
        foreach (OneButtonSpriteFrames frames in FindObjectsByType<OneButtonSpriteFrames>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (frames.gameObject.name == objectName)
            {
                return frames;
            }
        }

        return null;
    }

    private bool HasRequiredReferences()
    {
        bool configured =
            backgroundImage != null &&
            closedEyeImage != null &&
            smallGhostImage != null &&
            bigGhostImage != null &&
            attackImage != null &&
            resultTextImage != null &&
            backgroundFrames != null &&
            closedEyeFrames != null &&
            smallGhostFrames != null &&
            resultTextFrames != null &&
            audioManager != null;

        if (!configured)
        {
            Debug.LogError("OneButtonGamePrefabVer is missing serialized references.", this);
        }

        return configured;
    }

    private static void ConfigureCamera()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.orthographic = true;
    }

    private void Update()
    {
        float frameDelta = Time.deltaTime * FramesPerSecond;

        AnimateBackground();
        UpdateAttack();

        if (isReturningToTitle)
        {
            return;
        }

        if (introTransitionActive)
        {
            AnimateClosedEye(false, frameDelta);
            if (currentEyeFrame >= OpenEyeFrame)
            {
                introTransitionActive = false;
            }

            wasPressed = false;
            return;
        }

        bool isPressed = IsButtonPressed();
        bool canUsePress = !isGameOver || gameOverPromptPlayed;

        AnimateClosedEye(isPressed && canUsePress, frameDelta);

        if (isPressed && canUsePress)
        {
            range += frameDelta;
            if (!isGameOver)
            {
                resultTextImage.gameObject.SetActive(false);
                PlayChargeCue(range / deadRange);
            }
        }

        if (!isGameOver && wasPressed && !isPressed)
        {
            ReleaseAttack();
        }
        else if (isGameOver)
        {
            UpdateGameOver(isPressed);
        }

        wasPressed = isPressed;
    }

    private static bool IsButtonPressed()
    {
        bool keyboardPressed = Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
        bool mousePressed = Mouse.current != null && Mouse.current.leftButton.isPressed;
        bool touchPressed = Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed;
        return keyboardPressed || mousePressed || touchPressed;
    }

    private void ReleaseAttack()
    {
        float result = range / deadRange;

        StopChargeCue();
        range = 0f;

        if (result >= 1f)
        {
            bigGhostImage.gameObject.SetActive(true);
            smallGhostImage.gameObject.SetActive(false);
            ShowResultTextFrame(0, new Vector2(400f, 800f));
            audioManager.PlayDead();
            isGameOver = true;
            gameOverPromptPlayed = false;
            restartPromptPlayed = false;
            gameOverTimer = 0f;
            return;
        }

        if (result > 0.85f)
        {
            smallGhostImage.gameObject.SetActive(false);
            attackImage.gameObject.SetActive(true);
            attackTimer = 0f;
            ShowResultTextFrame(2, new Vector2(200f, 400f));
            audioManager.PlayAttack();
        }
        else
        {
            ShowSmallGhost(result);
        }

        deadRange = Random.Range(deadRangeFrames.x, deadRangeFrames.y);
    }

    private void UpdateGameOver(bool isPressed)
    {
        gameOverTimer += Time.deltaTime;

        if (gameOverTimer >= GameOverPromptDelay && !gameOverPromptPlayed)
        {
            gameOverPromptPlayed = true;
            range = 0f;
            ShowResultTextFrame(1, new Vector2(200f, 400f));
            audioManager.PlayAttack();
        }

        if (gameOverPromptPlayed && returnToTitleButton != null)
        {
            returnToTitleButton.gameObject.SetActive(true);
        }

        if (isPressed && range >= restartHoldSeconds * FramesPerSecond && !restartPromptPlayed)
        {
            gameOverTimer = 0f;
            restartPromptPlayed = true;
            ShowResultTextFrame(2, new Vector2(200f, 400f));
            audioManager.PlayAttack();
        }

        if (wasPressed && !isPressed && range > restartHoldSeconds * FramesPerSecond)
        {
            RestartRoundInScene();
        }
    }

    private void RestartRoundInScene()
    {
        isGameOver = false;
        range = 0f;
        deadRange = Random.Range(deadRangeFrames.x, deadRangeFrames.y);
        bigGhostImage.gameObject.SetActive(false);
        resultTextImage.gameObject.SetActive(false);
        gameOverPromptPlayed = false;
        restartPromptPlayed = false;

        if (returnToTitleButton != null)
        {
            returnToTitleButton.gameObject.SetActive(false);
            returnToTitleButton.interactable = true;
        }
    }

    private void AnimateBackground()
    {
        backgroundTimer += Time.deltaTime;
        if (backgroundTimer < 0.2f)
        {
            return;
        }

        backgroundTimer = 0f;
        backgroundFrame = (backgroundFrame + 1) % 4;
        backgroundFrames.SetFrame(backgroundFrame);
    }

    private void AnimateClosedEye(bool isPressed, float frameDelta)
    {
        int direction = isPressed ? -1 : 1;
        currentEyeFrameValue = Mathf.Clamp(
            currentEyeFrameValue + direction * frameDelta,
            ClosedEyeFrame,
            OpenEyeFrame);

        int nextFrame = Mathf.RoundToInt(currentEyeFrameValue);
        if (nextFrame == currentEyeFrame)
        {
            return;
        }

        SetEyeFrame(nextFrame);
    }

    private void UpdateAttack()
    {
        if (!attackImage.gameObject.activeSelf)
        {
            return;
        }

        attackTimer += Time.deltaTime;
        float progress = Mathf.Clamp01(attackTimer / AttackDuration);
        float size = Mathf.Lerp(0f, 700f, progress);
        attackImage.rectTransform.sizeDelta = new Vector2(size, size);
        attackImage.color = new Color(1f, 1f, 1f, 1f - progress);

        if (progress >= 1f)
        {
            attackImage.gameObject.SetActive(false);
            attackImage.color = Color.white;
        }
    }

    private void ShowSmallGhost(float result)
    {
        int frame = Random.Range(0, 8);
        smallGhostFrames.SetFrame(frame);

        float x = Random.Range(-200f, 0f);
        float y = result * -300f;
        float scale = Mathf.Max(result * 5f, 1f);
        smallGhostImage.rectTransform.anchoredPosition = new Vector2(x, y);
        smallGhostImage.rectTransform.sizeDelta = new Vector2(100f * scale, 200f * scale);
        smallGhostImage.gameObject.SetActive(true);
    }

    private void ShowResultTextFrame(int frame, Vector2 size)
    {
        // SerActive()を呼び出しとき、中身のSetFrame(0)が呼ばれるため、SetFrame()の前にを呼び出さないといけない
        resultTextImage.gameObject.SetActive(true);  
        resultTextFrames.SetFrame(frame);
        resultTextImage.rectTransform.sizeDelta = size;
    }

    private void PlayChargeCue(float result)
    {
        if (result > 0f && result < 0.05f)
        {
            audioManager.PlayHeartbeatSlow();
        }

        if (result >= 0.05f && result < 0.10f)
        {
            audioManager.PlayStep();
        }

        if (result >= 0.30f && result < 0.40f)
        {
            audioManager.StopHeartbeatSlow();
            audioManager.PlayHeartbeatFast();
        }

        if (result >= 0.50f && result < 0.55f)
        {
            audioManager.PlayWarning();
        }

        if (result >= 0.95f && result < 1f)
        {
            audioManager.PlayCloseEye();
        }
    }

    private void StopChargeCue()
    {
        audioManager.StopChargeLoops();
    }

    private void ResetRoundVisuals()
    {
        backgroundFrames.SetFrame(0);
        closedEyeFrames.SetFrame(currentEyeFrame);
        resultTextFrames.SetFrame(2);

        smallGhostImage.gameObject.SetActive(false);
        bigGhostImage.gameObject.SetActive(false);
        attackImage.gameObject.SetActive(false);
        resultTextImage.gameObject.SetActive(false);

        if (returnToTitleButton != null)
        {
            returnToTitleButton.gameObject.SetActive(false);
            returnToTitleButton.interactable = true;
        }
    }

    private void BindReturnToTitleButton()
    {
        if (returnToTitleButton == null)
        {
            return;
        }

        returnToTitleButton.onClick.RemoveListener(ReturnToTitleScene);
        returnToTitleButton.onClick.AddListener(ReturnToTitleScene);
        returnToTitleButton.gameObject.SetActive(false);
    }

    private void ReturnToTitleScene()
    {
        if (isReturningToTitle)
        {
            return;
        }
        isReturningToTitle = true;

        // 音を止める
        StopChargeCue(); 

        // ボタンを無効化して二重クリックを防ぐ
        if (returnToTitleButton != null)
        {
            returnToTitleButton.interactable = false;
        }

        // 目を閉じるアニメーションを再生してからタイトルシーンに遷移する
        StartCoroutine(PlayCloseEyeAndLoadTitleScene());
    }

    private void SetEyeFrame(int frame)
    {
        currentEyeFrame = Mathf.Clamp(frame, ClosedEyeFrame, OpenEyeFrame);
        currentEyeFrameValue = currentEyeFrame;
        closedEyeFrames.SetFrame(currentEyeFrame);
    }

    private static void EnsureUiEventSystem()
    {
        EventSystem eventSystem = FindFirstObjectByType<EventSystem>();
        if (eventSystem != null)
        {
            if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
            {
                eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            }

            StandaloneInputModule standaloneInputModule = eventSystem.GetComponent<StandaloneInputModule>();
            if (standaloneInputModule != null)
            {
                standaloneInputModule.enabled = false;
            }

            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
    }

    private static void EnsureCanvasIsInteractive()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        if (canvas == null)
        {
            return;
        }

        RectTransform rectTransform = canvas.transform as RectTransform;
        if (rectTransform != null)
        {
            rectTransform.localScale = Vector3.one;
        }

        if (canvas.GetComponent<GraphicRaycaster>() == null)
        {
            canvas.gameObject.AddComponent<GraphicRaycaster>();
        }
    }

    private IEnumerator PlayCloseEyeAndLoadTitleScene()
    {
        if (closedEyeFrames == null)
        {
            SceneManager.LoadScene(titleSceneName);
            yield break;
        }

        if (closedEyeImage != null)
        {
            closedEyeImage.gameObject.SetActive(true);
        }

        float frameValue = OpenEyeFrame;
        while (frameValue > ClosedEyeFrame)
        {
            frameValue = Mathf.Max(ClosedEyeFrame, frameValue - Time.deltaTime * FramesPerSecond);
            SetEyeFrame(Mathf.RoundToInt(frameValue));
            yield return null;
        }

        SceneManager.LoadScene(titleSceneName);
    }

}
