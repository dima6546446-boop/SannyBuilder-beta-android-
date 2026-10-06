using UnityEditor;

namespace CaucasusDrive.EditorTools
{
    /// <summary>Значки интерфейса (Resources/Icons): без сжатия и мипов, прозрачность — по альфе.</summary>
    public class IconImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Contains("/Resources/Icons/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Default;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.npotScale = TextureImporterNPOTScale.None;
        }
    }
}
