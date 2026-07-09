using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class OneButtonGamePrefabVer : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField] private Vector2 deadRangeFrames = new Vector2(400f, 700f);
    [SerializeField] private float restartHoldSeconds = 3f;

    [Header("View References")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image closedEyeImage;
    [SerializeField] private Image smallGhostImage;
    [SerializeField] private Image bigGhostImage;
    [SerializeField] private Image attackImage;
    [SerializeField] private Image resultTextImage;

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

    private float range;
    private float deadRange;

    private bool wasPressed;
    private bool isGameOver;
    private bool gameOverPromptPlayed;
    private bool restartPromptPlayed;

    private float gameOverTimer;
    private float attackTimer;
    private float backgroundTimer;
    private float eyeFrameValue = 14f;
    private int backgroundFrame;
    private int eyeFrame = 14;

    private void Awake()
    {
        Screen.orientation = ScreenOrientation.LandscapeLeft;

        BindMissingReferences();
        if (!HasRequiredReferences())
        {
            enabled = false;
            return;
        }

        ConfigureCamera();

        deadRange = 500f;
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

        audioManager ??= FindFirstObjectByType<OneButtonAudioManager>(FindObjectsInactive.Include);
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
        bool isPressed = IsButtonPressed();
        bool canUsePress = !isGameOver || gameOverPromptPlayed;
        float frameDelta = Time.deltaTime * FramesPerSecond;

        AnimateBackground();
        AnimateClosedEye(isPressed && canUsePress, frameDelta);
        UpdateAttack();

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

        if (isPressed && range >= restartHoldSeconds * FramesPerSecond && !restartPromptPlayed)
        {
            gameOverTimer = 0f;
            restartPromptPlayed = true;
            ShowResultTextFrame(2, new Vector2(200f, 400f));
            audioManager.PlayAttack();
        }

        if (wasPressed && !isPressed && range > restartHoldSeconds * FramesPerSecond)
        {
            isGameOver = false;
            range = 0f;
            deadRange = Random.Range(deadRangeFrames.x, deadRangeFrames.y);
            bigGhostImage.gameObject.SetActive(false);
            resultTextImage.gameObject.SetActive(false);
            gameOverPromptPlayed = false;
            restartPromptPlayed = false;
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
        eyeFrameValue = Mathf.Clamp(eyeFrameValue + direction * frameDelta, 0f, 14f);
        int nextFrame = Mathf.RoundToInt(eyeFrameValue);
        if (nextFrame == eyeFrame)
        {
            return;
        }

        eyeFrame = nextFrame;
        closedEyeFrames.SetFrame(eyeFrame);
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
        resultTextFrames.SetFrame(frame);
        resultTextImage.rectTransform.sizeDelta = size;
        resultTextImage.gameObject.SetActive(true);
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
        closedEyeFrames.SetFrame(eyeFrame);
        resultTextFrames.SetFrame(2);

        smallGhostImage.gameObject.SetActive(false);
        bigGhostImage.gameObject.SetActive(false);
        attackImage.gameObject.SetActive(false);
        resultTextImage.gameObject.SetActive(false);
    }

}
