using UnityEditor;
using UnityEngine;

namespace Touchline.Editor
{
    public sealed class PlayerTextureImport : AssetPostprocessor
    {
        public static bool ConfigureBadge(TextureImporter importer)
        {
            var android=importer.GetPlatformTextureSettings("Android");
            bool changed=importer.mipmapEnabled||importer.isReadable||importer.maxTextureSize!=512||importer.npotScale!=TextureImporterNPOTScale.None||importer.wrapMode!=TextureWrapMode.Clamp||!importer.alphaIsTransparency||!android.overridden||android.format!=TextureImporterFormat.ASTC_6x6||android.maxTextureSize!=512;
            importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;importer.mipmapEnabled=false;importer.isReadable=false;
            importer.maxTextureSize=512;importer.npotScale=TextureImporterNPOTScale.None;importer.wrapMode=TextureWrapMode.Clamp;importer.anisoLevel=0;
            importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=true;importer.textureCompression=TextureImporterCompression.Compressed;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings{name="Android",overridden=true,maxTextureSize=512,format=TextureImporterFormat.ASTC_6x6,compressionQuality=80});
            return changed;
        }
        void OnPreprocessTexture()
        {
            if(assetPath.StartsWith("Assets/Touchline/Resources/Logos/")){ConfigureBadge((TextureImporter)assetImporter);return;}
            if(!assetPath.StartsWith("Assets/Touchline/Resources/Models/skin-")&&!assetPath.EndsWith("Models/eyes-brown.png")&&!assetPath.EndsWith("Models/hair-short.png"))return;
            var importer=(TextureImporter)assetImporter;int size=assetPath.Contains("eyes-brown")?512:1024;
            importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;importer.mipmapEnabled=true;importer.isReadable=false;
            importer.maxTextureSize=size;importer.anisoLevel=2;importer.alphaSource=assetPath.EndsWith("hair-short.png")?TextureImporterAlphaSource.FromInput:TextureImporterAlphaSource.None;
            importer.alphaIsTransparency=assetPath.EndsWith("hair-short.png");
            importer.textureCompression=TextureImporterCompression.Compressed;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings{name="Android",overridden=true,maxTextureSize=size,format=TextureImporterFormat.ASTC_6x6,compressionQuality=70});
        }
    }
}
