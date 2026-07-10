#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class OneButtonBackgroundSetup
{
    private const string TexturePath = "Assets/OneButton/Textures/background1.png";
    private const string PrefabPath = "Assets/OneButton/Prefabs/Images/Background Image.prefab";
    private const int Columns = 2;
    private const int Rows = 2;

    [InitializeOnLoadMethod]
    private static void RegisterAutoSetup()
    {
        EditorApplication.delayCall += TryAutoSetup;
    }

    [MenuItem("Tools/OneButton/Setup Background Frames")]
    public static void SetupBackgroundFrames()
    {
        if (!EnsureBackgroundSprites())
        {
            return;
        }

        if (!ApplyBackgroundPrefabFrames())
        {
            return;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("OneButton background frames are configured.");
    }

    private static void TryAutoSetup()
    {
        EditorApplication.delayCall -= TryAutoSetup;

        if (EditorApplication.isPlayingOrWillChangePlaymode || IsBackgroundConfigured())
        {
            return;
        }

        SetupBackgroundFrames();
    }

    private static bool IsBackgroundConfigured()
    {
        Sprite[] sprites = LoadOrderedSprites();
        if (sprites.Length != Columns * Rows)
        {
            return false;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            OneButtonSpriteFrames spriteFrames = root.GetComponent<OneButtonSpriteFrames>();
            if (spriteFrames == null)
            {
                return false;
            }

            SerializedObject serializedObject = new SerializedObject(spriteFrames);
            SerializedProperty framesProperty = serializedObject.FindProperty("frames");
            if (framesProperty == null || framesProperty.arraySize != sprites.Length)
            {
                return false;
            }

            for (int i = 0; i < sprites.Length; i++)
            {
                if (framesProperty.GetArrayElementAtIndex(i).objectReferenceValue != sprites[i])
                {
                    return false;
                }
            }

            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static bool EnsureBackgroundSprites()
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        if (texture == null)
        {
            Debug.LogError($"Background texture not found: {TexturePath}");
            return false;
        }

        TextureImporter importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError($"Texture importer not found: {TexturePath}");
            return false;
        }

        int frameWidth = texture.width / Columns;
        int frameHeight = texture.height / Rows;
        SpriteMetaData[] spritesheet = new SpriteMetaData[Columns * Rows];
        int index = 0;

        for (int row = 0; row < Rows; row++)
        {
            for (int column = 0; column < Columns; column++)
            {
                spritesheet[index] = new SpriteMetaData
                {
                    name = $"background1_{index}",
                    alignment = (int)SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                    rect = new Rect(
                        column * frameWidth,
                        texture.height - ((row + 1) * frameHeight),
                        frameWidth,
                        frameHeight)
                };
                index++;
            }
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.alphaIsTransparency = true;
        importer.spritesheet = spritesheet;
        AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceUpdate);
        return true;
    }

    private static bool ApplyBackgroundPrefabFrames()
    {
        Sprite[] sprites = LoadOrderedSprites();
        if (sprites.Length != Columns * Rows)
        {
            Debug.LogError($"Expected {Columns * Rows} background sprites, found {sprites.Length}.");
            return false;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Image image = root.GetComponent<Image>();
            OneButtonSpriteFrames spriteFrames = root.GetComponent<OneButtonSpriteFrames>();
            if (image == null || spriteFrames == null)
            {
                Debug.LogError($"Background prefab is missing required components: {PrefabPath}");
                return false;
            }

            image.sprite = sprites[0];
            EditorUtility.SetDirty(image);

            SerializedObject serializedObject = new SerializedObject(spriteFrames);
            serializedObject.FindProperty("image").objectReferenceValue = image;
            serializedObject.FindProperty("sheetSprite").objectReferenceValue = null;
            serializedObject.FindProperty("columns").intValue = 1;
            serializedObject.FindProperty("rows").intValue = 1;

            SerializedProperty framesProperty = serializedObject.FindProperty("frames");
            framesProperty.arraySize = sprites.Length;
            for (int i = 0; i < sprites.Length; i++)
            {
                framesProperty.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(spriteFrames);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Sprite[] LoadOrderedSprites()
    {
        return AssetDatabase
            .LoadAllAssetsAtPath(TexturePath)
            .OfType<Sprite>()
            .Where(sprite => sprite.name.StartsWith("background1_", StringComparison.Ordinal))
            .OrderBy(sprite => sprite.name, StringComparer.Ordinal)
            .ToArray();
    }
}
#endif
