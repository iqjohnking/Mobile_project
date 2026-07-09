using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public sealed class OneButtonSpriteFrames : MonoBehaviour
{
    [SerializeField] private Image image;
    [SerializeField] private Sprite sheetSprite;
    [SerializeField] private int columns = 1;
    [SerializeField] private int rows = 1;
    [SerializeField] private Sprite[] frames;

    private Sprite[] runtimeFrames;

    public Image Image => image != null ? image : image = GetComponent<Image>();

    private void Awake()
    {
        SetFrame(0);
    }

    public void SetFrame(int frame)
    {
        Sprite sprite = GetFrame(frame);
        if (sprite != null)
        {
            Image.sprite = sprite;
        }
    }

    private Sprite GetFrame(int frame)
    {
        if (frames is { Length: > 0 })
        {
            return frames[Mathf.Clamp(frame, 0, frames.Length - 1)];
        }

        Sprite[] generatedFrames = GetRuntimeFrames();
        if (generatedFrames.Length == 0)
        {
            return null;
        }

        return generatedFrames[Mathf.Clamp(frame, 0, generatedFrames.Length - 1)];
    }

    private Sprite[] GetRuntimeFrames()
    {
        if (runtimeFrames != null)
        {
            return runtimeFrames;
        }

        if (sheetSprite == null || sheetSprite.texture == null || columns <= 0 || rows <= 0)
        {
            runtimeFrames = new Sprite[0];
            return runtimeFrames;
        }

        runtimeFrames = new Sprite[columns * rows];
        Rect sheetRect = sheetSprite.rect;
        float frameWidth = sheetRect.width / columns;
        float frameHeight = sheetRect.height / rows;

        for (int i = 0; i < runtimeFrames.Length; i++)
        {
            int column = i % columns;
            int row = i / columns;
            Rect frameRect = new Rect(
                sheetRect.x + column * frameWidth,
                sheetRect.y + (rows - row - 1) * frameHeight,
                frameWidth,
                frameHeight);

            runtimeFrames[i] = Sprite.Create(
                sheetSprite.texture,
                frameRect,
                new Vector2(0.5f, 0.5f),
                sheetSprite.pixelsPerUnit);
        }

        return runtimeFrames;
    }

    private void OnDestroy()
    {
        if (runtimeFrames == null)
        {
            return;
        }

        foreach (Sprite sprite in runtimeFrames)
        {
            if (sprite != null)
            {
                Destroy(sprite);
            }
        }
    }
}
