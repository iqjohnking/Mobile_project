using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using StandaloneInputModule = UnityEngine.EventSystems.StandaloneInputModule;

public sealed class TitleSceneController : MonoBehaviour
{
    [Header("Scene Names")]
    [SerializeField] private string gameSceneName = "OneButtonMobilePrefabVer";

    [Header("View References")]
    [SerializeField] private GameObject manualOverlay;
    [SerializeField] private GameObject menuRoot;
    [SerializeField] private Button startButton;
    [SerializeField] private Button manualButton;
    [SerializeField] private Image closedEyeImage;
    [SerializeField] private OneButtonSpriteFrames closedEyeFrames;

    private const float FramesPerSecond = 60f;
    private const int OpenEyeFrame = 14;
    private const int ClosedEyeFrame = 0;

    private bool isStartingGame;

    private void Awake()
    {
        EnsureUiEventSystem();
        BindMissingReferences();
        EnsureCanvasIsInteractive();
        EnsureManualOverlayBackground();
        EnsureButtonHandlers();
    }

    private void Start()
    {
        ApplyInitialState();
    }

    public void OnClickStartButton()
    {
        if (isStartingGame)
        {
            return;
        }
        isStartingGame = true;

        // ボタンを無効化して、ユーザーが複数回クリックできないようにする
        SetButtonsInteractable(false);

        // 目を閉じるアニメーションを再生してからシーンをロードする
        StartCoroutine(PlayCloseEyeAndLoadScene());
    }

    public void OnClickManualButton()
    {
        if (manualOverlay == null || isStartingGame)
        {
            return;
        }

        manualOverlay.SetActive(!manualOverlay.activeSelf);
    }

    private void BindMissingReferences()
    {
        manualOverlay   ??= FindByName<GameObject>("ManualOverlayRoot");
        menuRoot        ??= FindByName<GameObject>("MenuRoot");
        startButton     ??= FindByName<Button>("StartButton");
        manualButton    ??= FindByName<Button>("ManualButton");
        closedEyeImage  ??= FindByName<Image>("Closed Eye Image");
        closedEyeFrames ??= FindByName<OneButtonSpriteFrames>("Closed Eye Image");
    }

    private void EnsureButtonHandlers()
    {
        if (startButton != null)
        {
            startButton.onClick.RemoveListener(OnClickStartButton);
            startButton.onClick.AddListener(OnClickStartButton);
        }

        if (manualButton != null)
        {
            manualButton.onClick.RemoveListener(OnClickManualButton);
            manualButton.onClick.AddListener(OnClickManualButton);
        }
    }

    private void ApplyInitialState()
    {
        if (manualOverlay != null)
        {
            manualOverlay.SetActive(false);
        }

        if (closedEyeFrames != null)
        {
            closedEyeFrames.SetFrame(OpenEyeFrame);
        }

        if (closedEyeImage != null)
        {
            closedEyeImage.gameObject.SetActive(false);
        }
    }

    private void EnsureManualOverlayBackground()
    {
        if (manualOverlay == null)
        {
            return;
        }

        Canvas canvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        if (canvas != null && manualOverlay.transform.parent != canvas.transform)
        {
            manualOverlay.transform.SetParent(canvas.transform, false);

            if (closedEyeFrames != null)
            {
                manualOverlay.transform.SetSiblingIndex(closedEyeFrames.transform.GetSiblingIndex());
            }
        }

        Image background = manualOverlay.GetComponent<Image>();
        if (background == null)
        {
            background = manualOverlay.AddComponent<Image>();
        }

        background.color = new Color(0f, 0f, 0f, 0.6f);
        background.raycastTarget = true;

        RectTransform rectTransform = manualOverlay.transform as RectTransform;
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
    }

    private IEnumerator PlayCloseEyeAndLoadScene()
    {
        if (closedEyeFrames == null)
        {
            SceneManager.LoadScene(gameSceneName);
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
            closedEyeFrames.SetFrame(Mathf.RoundToInt(frameValue));
            yield return null;
        }

        SceneManager.LoadScene(gameSceneName);
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (startButton != null)
        {
            startButton.interactable = interactable;
        }

        if (manualButton != null)
        {
            manualButton.interactable = interactable;
        }

        if (menuRoot == null)
        {
            return;
        }

        foreach (TMP_Text text in menuRoot.GetComponentsInChildren<TMP_Text>(true))
        {
            text.raycastTarget = interactable;
        }
    }

    private static T FindByName<T>(string objectName) where T : Object
    {
        if (typeof(T) == typeof(GameObject))
        {
            GameObject gameObject = GameObject.Find(objectName);
            return gameObject as T;
        }

        foreach (T component in FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (component is Component typedComponent && typedComponent.gameObject.name == objectName)
            {
                return component;
            }
        }

        return null;
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
}
