using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GravityPrototype.Editor
{
    /// <summary>
    /// Get-or-create helpers for the code-generated prototype assets. Both scene builders share these
    /// so Sprite, Material and folder rules stay in one place instead of drifting apart.
    /// </summary>
    public static class PrototypeAssetFactory
    {
        public const string ArtPath = "Assets/GravityPrototype/Art";
        public const string PrefabPath = "Assets/GravityPrototype/Prefabs";
        public const string ScenesPath = "Assets/GravityPrototype/Scenes";

        public static Sprite GetOrCreateSprite(string name, bool circular)
        {
            string path = ArtPath + "/" + name + ".asset";
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is Sprite sprite)
                {
                    return sprite;
                }
            }

            if (File.Exists(path))
            {
                throw new InvalidOperationException("Existing sprite asset has no Sprite subasset: " + path);
            }

            const int resolution = 64;
            Texture2D texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            texture.name = name + " Texture";
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            Color[] pixels = new Color[resolution * resolution];
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), Vector2.one * resolution * 0.5f);
                    float alpha = circular ? Mathf.Clamp01(resolution * 0.5f - distance) : 1f;
                    pixels[y * resolution + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            EnsureFolder(ArtPath);
            AssetDatabase.CreateAsset(texture, path);
            Sprite result = Sprite.Create(texture, new Rect(0f, 0f, resolution, resolution), Vector2.one * 0.5f, resolution);
            result.name = name;
            AssetDatabase.AddObjectToAsset(result, texture);
            return result;
        }

        public static Material GetOrCreateLineMaterial()
        {
            const string path = ArtPath + "/FrameLines.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                throw new InvalidOperationException("Sprites/Default shader was not found.");
            }

            EnsureFolder(ArtPath);
            Material material = new Material(shader) { name = "Frame Lines" };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        public static PhysicsMaterial2D GetOrCreateContactMaterial()
        {
            const string path = ArtPath + "/BallContact.physicsMaterial2D";
            PhysicsMaterial2D material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(path);
            if (material != null)
            {
                return material;
            }

            EnsureFolder(ArtPath);
            material = new PhysicsMaterial2D("Ball Contact") { friction = 0.35f, bounciness = 0.12f };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
