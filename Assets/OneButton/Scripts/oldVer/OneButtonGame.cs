using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public sealed class OneButtonGame : MonoBehaviour
{
    [Header("Timing")]
    // 2回目以降の「押しすぎたら死ぬ時間」をランダムで決める範囲。
    // 元のC++版と同じく、400～700フレームの間で変わる。
    [SerializeField] private Vector2 deadRangeFrames = new Vector2(400f, 700f);

    // ゲームオーバー後、何秒長押ししたら復活準備OKにするか。
    // UnityのInspectorから調整できる。
    [SerializeField] private float restartHoldSeconds = 3f;

    // ここから下は、ゲーム中に自動で作る画像や音の入れ物。
    // Inspectorで手動設定しなくていい。Resourcesフォルダから勝手に読む。
    private RawImage backgroundImage;
    private RawImage closedEyeImage;
    private RawImage smallGhostImage;
    private RawImage bigGhostImage;
    private RawImage attackImage;
    private RawImage resultTextImage;
    private AudioSource bgmSource;
    private AudioSource seSource;
    private AudioClip bgmClip;
    private AudioClip attackClip;
    private AudioClip deadClip;
    private AudioClip heartbeatSlowClip;
    private AudioClip heartbeatFastClip;
    private AudioClip stepClip;
    private AudioClip warningClip;
    private AudioClip closeClip;

    // 元のゲームは「1秒60フレーム」の考え方で時間を数えていた。
    // Unityでも同じ感覚になるように、秒ではなくフレームっぽい数に変換して使う。
    private const float FramesPerSecond = 60f;
    private const float AttackDuration = 200f / FramesPerSecond;
    private const float GameOverPromptDelay = 240f / FramesPerSecond;

    // rangeは「ボタンを押しっぱなしにした長さ」。
    // deadRangeは「これ以上押したら死ぬ長さ」。
    // result = range / deadRange で、どれくらい攻めたかを判定する。
    private float range;
    private float deadRange;

    // 入力やゲームオーバー中の状態を覚えておくためのフラグ。
    private bool wasPressed;
    private bool isGameOver;
    private bool gameOverPromptPlayed;
    private bool restartPromptPlayed;

    // アニメーション用のタイマー。
    private float gameOverTimer;
    private float attackTimer;
    private float backgroundTimer;
    private float eyeFrameValue = 14f;
    private int backgroundFrame;
    private int eyeFrame = 14;

    // 押している間に鳴り続ける音。
    // 成功音や死亡音は単発なので、これとは別にseSourceで鳴らす。
    private AudioSource heartbeatSlowSource;
    private AudioSource heartbeatFastSource;
    private AudioSource stepSource;
    private AudioSource warningSource;
    private AudioSource closeSource;

    private void Awake()
    {
        // スマホ横持ち用。ゲーム開始時に横画面へ固定する。
        Screen.orientation = ScreenOrientation.LandscapeLeft;

        // 画像・音・Canvasなど、ゲームに必要な物を全部用意する。
        EnsureRuntimeObjects();

        // 最初だけは元のC++版と同じく、死亡ラインを500フレームにする。
        // 2回目以降はランダムになる。
        deadRange = 500f;
        ResetRoundVisuals();

        // BGMを小さめの音量でループ再生する。
        if (bgmSource != null && bgmClip != null)
        {
            bgmSource.clip = bgmClip;
            bgmSource.loop = true;
            bgmSource.volume = 0.08f;
            bgmSource.Play();
        }
    }

    private void EnsureRuntimeObjects()
    {
        // 画像がまだ無い場合だけ、CanvasとUI画像を自動で作る。
        // つまり、シーンに何も置いてなくてもこのスクリプトだけで動く。
        if (backgroundImage == null)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
            }

            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.orthographic = true;

            Canvas canvas = CreateCanvas();
            backgroundImage = CreateRawImage(canvas.transform, "Background", LoadTexture("background1"), StretchFull(), 0);
            smallGhostImage = CreateRawImage(canvas.transform, "Small Ghost", LoadTexture("ghost"), Centered(new Vector2(100f, 200f)), 1);
            bigGhostImage = CreateRawImage(canvas.transform, "Big Ghost", LoadTexture("ghost_Big"), StretchFull(), 2);
            attackImage = CreateRawImage(canvas.transform, "Attack", LoadTexture("attack"), Centered(Vector2.zero), 3);
            resultTextImage = CreateRawImage(canvas.transform, "Result Text", LoadTexture("resultText"), Centered(new Vector2(200f, 400f)), 4);
            closedEyeImage = CreateRawImage(canvas.transform, "Closed Eye", LoadTexture("closeYourEye"), StretchFull(), 5);

            if (FindFirstObjectByType<EventSystem>() == null)
            {
                GameObject eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<EventSystem>();
                eventSystem.AddComponent<InputSystemUIInputModule>();
            }
        }

        // 音を鳴らすためのAudioSourceを作る。
        // BGM用と単発SE用は分けておく。
        if (bgmSource == null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
        }

        if (seSource == null)
        {
            seSource = gameObject.AddComponent<AudioSource>();
            seSource.playOnAwake = false;
        }

        // Resources/OneButton/... から画像や音を読む。
        // ファイル名を変えるとここも変える必要がある。
        bgmClip = LoadAudio("bgm_1");
        attackClip = LoadAudio("attack");
        deadClip = LoadAudio("dead");
        heartbeatSlowClip = LoadAudio("Heartbeat04-4(Slow-Reverb-Short)");
        heartbeatFastClip = LoadAudio("Heartbeat04-6(Fast-Reverb-Short)");
        stepClip = LoadAudio("step");
        warningClip = LoadAudio("bilibili");
        closeClip = LoadAudio("biiiii");

        // 押している間に鳴り続ける音を、それぞれ別のAudioSourceにする。
        // 1個のAudioSourceだけだと、心拍と警報を同時に鳴らせない。
        heartbeatSlowSource = CreateLoopSource(heartbeatSlowClip, 1.2f);
        heartbeatFastSource = CreateLoopSource(heartbeatFastClip, 1.2f);
        stepSource = CreateLoopSource(stepClip, 1.2f);
        warningSource = CreateLoopSource(warningClip, 0.7f);
        closeSource = CreateLoopSource(closeClip, 0.7f);
    }

    private void Update()
    {
        // 今ボタンを押しているか調べる。
        // スマホはタッチ、PCテストではスペースキーやマウス左クリック。
        bool isPressed = IsButtonPressed();
        bool canUsePress = !isGameOver || gameOverPromptPlayed;

        // Unityの秒時間を、元ゲームと同じ「60fpsのフレーム数」っぽく変換する。
        float frameDelta = Time.deltaTime * FramesPerSecond;

        // 毎フレーム必要なアニメーション更新。
        AnimateBackground();
        AnimateClosedEye(isPressed && canUsePress, frameDelta);
        UpdateAttack();

        // 押している間はrangeを増やす。
        // rangeが増えるほど、成功に近づくが、押しすぎると死亡する。
        // ゲームオーバー直後はまだリトライ案内が出ていないので、押しても無視する。
        if (isPressed && canUsePress)
        {
            range += frameDelta;
            if (!isGameOver)
            {
                resultTextImage.gameObject.SetActive(false);
                PlayChargeCue(range / deadRange);
            }
        }

        // 押していたボタンを離した瞬間に、成功・失敗・死亡を判定する。
        if (!isGameOver && wasPressed && !isPressed)
        {
            ReleaseAttack();
        }
        // 死亡中は、復活待ちの処理だけをする。
        else if (isGameOver)
        {
            UpdateGameOver(isPressed);
        }

        // 次のフレームで「今離したか」を判断するため、今回の入力を保存する。
        wasPressed = isPressed;
    }

    private bool IsButtonPressed()
    {
        // スマホでもPCでも同じように遊べるように、3種類の入力を見る。
        bool keyboardPressed = Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
        bool mousePressed = Mouse.current != null && Mouse.current.leftButton.isPressed;
        bool touchPressed = Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed;
        return keyboardPressed || mousePressed || touchPressed;
    }

    private void ReleaseAttack()
    {
        // resultが1.0なら押しすぎ。
        // 0.85より大きくて1.0未満なら成功。
        // それ以下なら失敗。
        float result = range / deadRange;

        // ボタンを離したら、押している間のループ音は全部止める。
        StopChargeCue();
        range = 0f;

        // 押しすぎ。大きい幽霊が出てゲームオーバー。
        if (result >= 1f)
        {
            bigGhostImage.gameObject.SetActive(true);
            smallGhostImage.gameObject.SetActive(false);
            ShowResultTextFrame(0, new Vector2(400f, 800f));
            PlayOneShot(deadClip, 2f);
            isGameOver = true;
            gameOverPromptPlayed = false;
            restartPromptPlayed = false;
            gameOverTimer = 0f;
            return;
        }

        // かなりギリギリまで押せたら攻撃成功。
        if (result > 0.85f)
        {
            smallGhostImage.gameObject.SetActive(false);
            attackImage.gameObject.SetActive(true);
            attackTimer = 0f;
            ShowResultTextFrame(2, new Vector2(200f, 400f));
            PlayOneShot(attackClip, 1.5f);
        }
        else
        {
            // 早く離しすぎ。小さい幽霊が出るだけ。
            ShowSmallGhost(result);
        }

        // 次の挑戦からは、死亡ラインを400～700フレームでランダムにする。
        deadRange = Random.Range(deadRangeFrames.x, deadRangeFrames.y);
    }

    private void UpdateGameOver(bool isPressed)
    {
        // 死亡してからの時間を数える。
        gameOverTimer += Time.deltaTime;

        // 死亡後しばらくすると、文字を切り替えて効果音を鳴らす。
        if (gameOverTimer >= GameOverPromptDelay && !gameOverPromptPlayed)
        {
            gameOverPromptPlayed = true;
            range = 0f;
            ShowResultTextFrame(1, new Vector2(200f, 400f));
            PlayOneShot(attackClip, 1.5f);
        }

        // ゲームオーバー中に長押しすると、復活準備の表示にする。
        if (isPressed && range >= restartHoldSeconds * FramesPerSecond && !restartPromptPlayed)
        {
            gameOverTimer = 0f;
            restartPromptPlayed = true;
            ShowResultTextFrame(2, new Vector2(200f, 400f));
            PlayOneShot(attackClip, 1.5f);
        }

        // 長押ししたあとに離したら復活。
        // ただ押してすぐ離しただけでは復活しない。
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
        // 背景画像は2x2のスプライトシート。
        // 0.2秒ごとに次のコマへ進める。
        backgroundTimer += Time.deltaTime;
        if (backgroundTimer < 0.2f)
        {
            return;
        }

        backgroundTimer = 0f;
        backgroundFrame = (backgroundFrame + 1) % 4;
        SetUvFrame(backgroundImage, backgroundFrame, 2, 2);
    }

    private void AnimateClosedEye(bool isPressed, float frameDelta)
    {
        // 押している間は目を閉じる方向。
        // 離している間は目を開く方向。
        int direction = isPressed ? -1 : 1;
        eyeFrameValue = Mathf.Clamp(eyeFrameValue + direction * frameDelta, 0f, 14f);
        int nextFrame = Mathf.RoundToInt(eyeFrameValue);
        if (nextFrame == eyeFrame)
        {
            return;
        }

        eyeFrame = nextFrame;
        SetUvFrame(closedEyeImage, eyeFrame, 4, 4);
    }

    private void UpdateAttack()
    {
        // 攻撃画像が出ていない時は何もしない。
        if (!attackImage.gameObject.activeSelf)
        {
            return;
        }

        // 攻撃画像を大きくしながら薄くする。
        // 時間が終わったら非表示に戻す。
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
        // 小さい幽霊はランダムな見た目で出す。
        // 押した長さに応じて位置と大きさも少し変える。
        int frame = Random.Range(0, 8);
        SetUvFrame(smallGhostImage, frame, 4, 2);

        float x = Random.Range(-200f, 0f);
        float y = result * -300f;
        float scale = Mathf.Max(result * 5f, 1f);
        smallGhostImage.rectTransform.anchoredPosition = new Vector2(x, y);
        smallGhostImage.rectTransform.sizeDelta = new Vector2(100f * scale, 200f * scale);
        smallGhostImage.gameObject.SetActive(true);
    }

    private void ShowResultTextFrame(int frame, Vector2 size)
    {
        // resultText.pngは横3コマの画像。
        // frameで、どの文字を表示するか決める。
        SetUvFrame(resultTextImage, frame, 3, 1);
        resultTextImage.rectTransform.sizeDelta = size;
        resultTextImage.gameObject.SetActive(true);
    }

    private void PlayChargeCue(float result)
    {
        // 押している長さに応じて、ループ音を開始する。
        // ここで鳴らした音は、ボタンを離すまで止めない。
        if (result > 0f && result < 0.05f)
        {
            PlayLoop(heartbeatSlowSource);
        }

        if (result >= 0.05f && result < 0.10f)
        {
            PlayLoop(stepSource);
        }

        if (result >= 0.30f && result < 0.40f)
        {
            StopLoop(heartbeatSlowSource);
            PlayLoop(heartbeatFastSource);
        }

        if (result >= 0.50f && result < 0.55f)
        {
            PlayLoop(warningSource);
        }

        if (result >= 0.95f && result < 1f)
        {
            PlayLoop(closeSource);
        }
    }

    private void StopChargeCue()
    {
        // ボタンを離したら、押している間に鳴っていた音を全部止める。
        StopLoop(heartbeatSlowSource);
        StopLoop(heartbeatFastSource);
        StopLoop(stepSource);
        StopLoop(warningSource);
        StopLoop(closeSource);
    }

    private void PlayOneShot(AudioClip clip, float volumeScale = 1f)
    {
        // 成功音・死亡音など、一回だけ鳴らす音。
        if (clip != null && seSource != null)
        {
            seSource.volume = 1f;
            seSource.PlayOneShot(clip, volumeScale);
        }
    }

    private AudioSource CreateLoopSource(AudioClip clip, float volume)
    {
        // ループ音専用のAudioSourceを作る。
        // 心拍、足音、警報を同時に鳴らすために、音ごとに分ける。
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.clip = clip;
        source.volume = volume;
        return source;
    }

    private static void PlayLoop(AudioSource source)
    {
        // すでに鳴っているなら、最初から鳴らし直さない。
        if (source != null && source.clip != null && !source.isPlaying)
        {
            source.Play();
        }
    }

    private static void StopLoop(AudioSource source)
    {
        // 鳴っているループ音だけ止める。
        if (source != null && source.isPlaying)
        {
            source.Stop();
        }
    }

    private void ResetRoundVisuals()
    {
        // ゲーム開始時の見た目に戻す。
        // 小さい幽霊、大きい幽霊、攻撃、文字は最初は非表示。
        SetUvFrame(backgroundImage, 0, 2, 2);
        SetUvFrame(closedEyeImage, eyeFrame, 4, 4);
        SetUvFrame(resultTextImage, 2, 3, 1);

        smallGhostImage.gameObject.SetActive(false);
        bigGhostImage.gameObject.SetActive(false);
        attackImage.gameObject.SetActive(false);
        resultTextImage.gameObject.SetActive(false);
    }

    private static void SetUvFrame(RawImage image, int frame, int columns, int rows)
    {
        // スプライトシートの中から、指定した1コマだけを表示する。
        // columnsは横のコマ数、rowsは縦のコマ数。
        if (image == null)
        {
            return;
        }

        int col = frame % columns;
        int row = frame / columns;
        image.uvRect = new Rect(
            col / (float)columns,
            1f - (row + 1f) / rows,
            1f / columns,
            1f / rows);
    }

    private static Canvas CreateCanvas()
    {
        // 画面全体にUIを表示するためのCanvasを作る。
        // 1920x1080基準なので、スマホ横画面に合わせやすい。
        GameObject canvasObject = new GameObject("Landscape Canvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    private static RawImage CreateRawImage(Transform parent, string name, Texture texture, RectPreset preset, int siblingIndex)
    {
        // 画像表示用のUIオブジェクトを1個作る。
        // siblingIndexは重なり順。数字が大きいほど前に出る。
        GameObject gameObject = new GameObject(name);
        gameObject.layer = LayerMask.NameToLayer("UI");
        gameObject.transform.SetParent(parent, false);
        gameObject.transform.SetSiblingIndex(siblingIndex);

        RawImage image = gameObject.AddComponent<RawImage>();
        image.texture = texture;
        image.raycastTarget = false;
        ApplyRectPreset(image.rectTransform, preset);
        return image;
    }

    private static RectPreset StretchFull()
    {
        // 画面いっぱいに広げる設定。
        return new RectPreset(Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Vector2.zero);
    }

    private static RectPreset Centered(Vector2 size)
    {
        // 画面中央に、指定サイズで置く設定。
        return new RectPreset(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size, new Vector2(0.5f, 0.5f));
    }

    private static void ApplyRectPreset(RectTransform rect, RectPreset preset)
    {
        // RectTransformに位置・サイズ設定をまとめて入れる。
        rect.anchorMin = preset.AnchorMin;
        rect.anchorMax = preset.AnchorMax;
        rect.anchoredPosition = preset.AnchoredPosition;
        rect.sizeDelta = preset.SizeDelta;
        rect.pivot = preset.Pivot;

        if (preset.AnchorMin == Vector2.zero && preset.AnchorMax == Vector2.one)
        {
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }

    private static Texture LoadTexture(string fileName)
    {
        // Resources/OneButton/Textures から画像を読む。
        return Resources.Load<Texture>("OneButton/Textures/" + fileName);
    }

    private static AudioClip LoadAudio(string fileName)
    {
        // Resources/OneButton/Sounds から音を読む。
        return Resources.Load<AudioClip>("OneButton/Sounds/" + fileName);
    }

    private readonly struct RectPreset
    {
        // UI画像の置き方をまとめて持つための小さい箱。
        public readonly Vector2 AnchorMin;
        public readonly Vector2 AnchorMax;
        public readonly Vector2 AnchoredPosition;
        public readonly Vector2 SizeDelta;
        public readonly Vector2 Pivot;

        public RectPreset(Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta, Vector2 pivot)
        {
            AnchorMin = anchorMin;
            AnchorMax = anchorMax;
            AnchoredPosition = anchoredPosition;
            SizeDelta = sizeDelta;
            Pivot = pivot;
        }
    }
}
