using UnityEngine;
using UnityEditor;

[System.Serializable]
public class TextureData {
    public Texture2D texture = null;
    public string name;
    public bool isCustomIcon;
    public Texture2D customIcon;
    public ResizeMode resizeMode;
    public Vector2 resizeValue;
    bool readable = false;
    
    public enum ResizeMode
    {
        Fixed=0,
        Proportional=1,
        Absolute=2,
        Relative=3
    }

    /// <summary>
    /// Readable stored and set to 1.
    /// </summary>
    public void StoreAndSetReadable()
    {
        var importer = GetImporter(texture);
        readable = importer.isReadable;
        if (readable) return;
        importer.isReadable = true;
        importer.SaveAndReimport();
    }

    /// <summary>
    /// Reset readable to stored.
    /// </summary>
    public void ResetReadable()
    {
        var importer = GetImporter(texture);
        importer.isReadable = readable;
        importer.SaveAndReimport();
    }

    /// <summary>
    /// Retrieves the texture importer for the texture.
    /// </summary>
    /// <param name="texture"></param>
    /// <returns></returns>
    private TextureImporter GetImporter(Texture texture)
    {
        string texturePath = AssetDatabase.GetAssetPath(texture);
        TextureImporter textureImporter = (TextureImporter)AssetImporter.GetAtPath(texturePath);
        return textureImporter;
    }
}