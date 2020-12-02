using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System;
using System.Text.RegularExpressions;
using System.Linq;

public class CameraToSkyboxWizard: ScriptableWizard {

    public Camera camera;

    [Range(16, 8192)]
    public int resolution = 1024;

    public string skyboxName = "New Skybox";

    [MenuItem ("Tools/Render Camera as Skybox")]
    static void OpenWizard() {
        CameraToSkyboxWizard wizard = ScriptableWizard.DisplayWizard<CameraToSkyboxWizard>("Render Skybox", "Render");
        wizard.camera = Camera.main;
    }

    [MenuItem ("CONTEXT/Camera/Render as Skybox")]
    static void OpenWizard(MenuCommand command) {
        Camera camera = command.context as Camera;
        CameraToSkyboxWizard wizard = ScriptableWizard.DisplayWizard<CameraToSkyboxWizard>("Render Skybox", "Render");
        wizard.camera = camera;
    }

    void OnWizardUpdate() {
        resolution = Mathf.Min(8192, Mathf.Max(16, resolution));
    }

    void OnWizardCreate() {
        if(camera != null) {
            Export();
        }
        else {
            Debug.LogError("Camera cannot be null!");
        }
    }

    private void Export() {
        string path = EditorUtility.SaveFolderPanel("Save Skybox", "Assets", "");
        if(path.Length == 0) {
            return;
        }
        Cubemap cubemap = new Cubemap(resolution, TextureFormat.RGB24, false);
        Vector3 eulerAngles = camera.transform.eulerAngles;
        camera.transform.eulerAngles = Vector3.zero;
        camera.RenderToCubemap(cubemap);
        camera.transform.eulerAngles = eulerAngles;
        if(!path.StartsWith(Application.dataPath)) {
            Debug.LogError("Must be located inside \"Assets\" folder of this project!");
            return;
        }
        path += "/" + skyboxName;
        if(!Directory.Exists(path)) {
            Directory.CreateDirectory(path);
        }
        SaveCubemap(cubemap, path);
        CreateMaterial(path);
        AssetDatabase.Refresh();
        ImportAndApplyTextures(path);
    }

    private static string GetAssetPath(string path) {
        return "Assets/" + new Regex("/Assets/").Split(path).Last();
    }

    private static void CreateMaterial(string path) {
        string shaderName = "Skybox/6 Sided";
        Shader shader = Shader.Find(shaderName);
        if(shader == null) {
            Debug.LogWarning(string.Format("Couldn't find shader \"{0}\"!", shaderName));
            return;
        }
        Material material = new Material(shader);
        AssetDatabase.CreateAsset(material, GetAssetPath(path) + "/Skybox.mat");
    }

    private static void ImportAndApplyTextures(string path) {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(GetAssetPath(path) + "/Skybox.mat");
        if(material == null) {  
            Debug.LogWarning("Skipping texture to material assignment.");
        }
        Action<CubemapFace, string> importAndApplyFace = (face, textureSlot) => {
            string textureAssetPath = string.Format("{0}/{1}.png", GetAssetPath(path), face);
            TextureImporter textureImporter = AssetImporter.GetAtPath(textureAssetPath) as TextureImporter;
            textureImporter.mipmapEnabled = false;
            textureImporter.wrapMode = TextureWrapMode.Clamp;
            textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
            AssetDatabase.ImportAsset(textureAssetPath);
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(textureAssetPath);
            Selection.activeObject = texture;
            EditorGUIUtility.PingObject(texture);
            if(material != null) {
                material.SetTexture(textureSlot, texture);
            }
        };
        importAndApplyFace(CubemapFace.NegativeX, "_RightTex");
        importAndApplyFace(CubemapFace.NegativeY, "_DownTex");
        importAndApplyFace(CubemapFace.NegativeZ, "_BackTex");
        importAndApplyFace(CubemapFace.PositiveX, "_LeftTex");
        importAndApplyFace(CubemapFace.PositiveY, "_UpTex");
        importAndApplyFace(CubemapFace.PositiveZ, "_FrontTex");
        if(material != null) {
            Selection.activeObject = material;
            EditorGUIUtility.PingObject(material);
        }
    }

    private static Texture2D FlipTexture(Texture2D texture)
    {
        int width = texture.width;
        int height = texture.height;
        Texture2D flipped = new Texture2D(width, height, texture.format, false);
        for (int x = 0; x < width; x++) {
            for (int y = 0; y < height; y++) {
                flipped.SetPixel(x, height - y - 1, texture.GetPixel(x, y));
            }
        }
        flipped.Apply();
        return flipped;
    }

    private static void SaveCubemap(Cubemap cubemap, string path) {
        Texture2D texture = new Texture2D(cubemap.width, cubemap.height, cubemap.format, false);
        Action<CubemapFace> exportFace = (face) => {
            Color[] pixels = cubemap.GetPixels(face);
            texture.SetPixels(pixels);
            texture = FlipTexture(texture);
            byte[] bytes = texture.EncodeToPNG();
            File.WriteAllBytes(string.Format("{0}/{1}.png", path, face), bytes);

        };
        exportFace(CubemapFace.NegativeX);
        exportFace(CubemapFace.NegativeY);
        exportFace(CubemapFace.NegativeZ);
        exportFace(CubemapFace.PositiveX);
        exportFace(CubemapFace.PositiveY);
        exportFace(CubemapFace.PositiveZ);
    }
}
