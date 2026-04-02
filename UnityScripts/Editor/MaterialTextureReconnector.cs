using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// Unity Editor tool that automatically reconnects textures to materials
/// after importing an AssetRipper export. Materials often lose their texture
/// references because GUIDs don't match between the export and Unity's asset database.
///
/// Usage: In Unity, go to Tools → AssetRipper → Reconnect Material Textures
/// </summary>
public class MaterialTextureReconnector : EditorWindow
{
    private string searchFolder = "Assets";
    private bool includeSubfolders = true;
    private bool overwriteExisting = false;
    private bool dryRun = false;
    private Vector2 scrollPos;
    private List<string> logMessages = new List<string>();
    private int fixedCount = 0;
    private int skippedCount = 0;
    private int errorCount = 0;

    // Common texture property names used in Unity shaders
    private static readonly Dictionary<string, string[]> TexturePropertyPatterns = new Dictionary<string, string[]>
    {
        // Main texture / Albedo
        { "_MainTex", new[] { "_MainTex", "_Albedo", "_BaseMap", "_BaseColorMap", "_Diffuse", "_Color" } },
        { "_BaseMap", new[] { "_BaseMap", "_MainTex", "_Albedo", "_BaseColorMap", "_Diffuse" } },
        { "_BaseColorMap", new[] { "_BaseColorMap", "_BaseMap", "_MainTex", "_Albedo" } },

        // Normal map
        { "_BumpMap", new[] { "_BumpMap", "_Normal", "_NormalMap", "_Normals" } },
        { "_NormalMap", new[] { "_NormalMap", "_BumpMap", "_Normal", "_Normals" } },

        // Metallic / Smoothness
        { "_MetallicGlossMap", new[] { "_MetallicGlossMap", "_Metallic", "_MetallicMap", "_MetallicSmoothness" } },
        { "_MaskMap", new[] { "_MaskMap", "_MetallicGlossMap", "_Metallic" } },

        // Specular
        { "_SpecGlossMap", new[] { "_SpecGlossMap", "_Specular", "_SpecularMap" } },

        // Emission
        { "_EmissionMap", new[] { "_EmissionMap", "_Emission", "_Emissive", "_EmissiveMap" } },

        // Occlusion / AO
        { "_OcclusionMap", new[] { "_OcclusionMap", "_Occlusion", "_AO", "_AmbientOcclusion" } },

        // Detail
        { "_DetailAlbedoMap", new[] { "_DetailAlbedoMap", "_DetailAlbedo", "_Detail" } },
        { "_DetailNormalMap", new[] { "_DetailNormalMap", "_DetailNormal" } },
        { "_DetailMask", new[] { "_DetailMask" } },

        // Parallax / Height
        { "_ParallaxMap", new[] { "_ParallaxMap", "_HeightMap", "_Height", "_Displacement" } },
    };

    // Common texture name suffixes that hint at which slot they belong in
    private static readonly Dictionary<string, string[]> SuffixToProperty = new Dictionary<string, string[]>
    {
        { "_MainTex", new[] { "_d", "_diff", "_diffuse", "_albedo", "_base", "_basecolor", "_color", "_col", "_tex" } },
        { "_BumpMap", new[] { "_n", "_nrm", "_normal", "_normals", "_bump", "_normalmap" } },
        { "_MetallicGlossMap", new[] { "_m", "_met", "_metallic", "_metallicsmoothness", "_metalness" } },
        { "_SpecGlossMap", new[] { "_s", "_spec", "_specular" } },
        { "_EmissionMap", new[] { "_e", "_em", "_emission", "_emissive", "_glow", "_illum" } },
        { "_OcclusionMap", new[] { "_ao", "_occlusion", "_occ", "_ambient" } },
        { "_ParallaxMap", new[] { "_h", "_height", "_disp", "_displacement", "_parallax" } },
    };

    [MenuItem("Tools/AssetRipper/Reconnect Material Textures")]
    public static void ShowWindow()
    {
        GetWindow<MaterialTextureReconnector>("Material Texture Reconnector");
    }

    private void OnGUI()
    {
        GUILayout.Label("Material Texture Reconnector", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "This tool scans all materials and tries to reconnect missing texture references " +
            "by matching texture file names to material names and shader property names.\n\n" +
            "Use after importing an AssetRipper export where materials lost their texture references.",
            MessageType.Info);

        EditorGUILayout.Space();

        searchFolder = EditorGUILayout.TextField("Search Folder", searchFolder);
        includeSubfolders = EditorGUILayout.Toggle("Include Subfolders", includeSubfolders);
        overwriteExisting = EditorGUILayout.Toggle("Overwrite Existing Textures", overwriteExisting);
        dryRun = EditorGUILayout.Toggle("Dry Run (preview only)", dryRun);

        EditorGUILayout.Space();

        if (GUILayout.Button("Reconnect Textures", GUILayout.Height(30)))
        {
            ReconnectAll();
        }

        EditorGUILayout.Space();

        if (logMessages.Count > 0)
        {
            EditorGUILayout.LabelField($"Results: {fixedCount} fixed, {skippedCount} skipped, {errorCount} errors");
            EditorGUILayout.Space();

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(300));
            foreach (string msg in logMessages)
            {
                EditorGUILayout.LabelField(msg, EditorStyles.wordWrappedLabel);
            }
            EditorGUILayout.EndScrollView();
        }
    }

    private void ReconnectAll()
    {
        logMessages.Clear();
        fixedCount = 0;
        skippedCount = 0;
        errorCount = 0;

        // Find all materials
        string[] materialGUIDs = AssetDatabase.FindAssets("t:Material", new[] { searchFolder });
        Log($"Found {materialGUIDs.Length} materials to scan.");

        // Build texture lookup: name (lowercase, no extension) -> Texture asset
        Dictionary<string, List<Texture>> textureLookup = BuildTextureLookup();
        Log($"Found {textureLookup.Count} unique texture names.");

        int processed = 0;
        foreach (string guid in materialGUIDs)
        {
            string matPath = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null) continue;

            processed++;
            if (processed % 50 == 0)
            {
                EditorUtility.DisplayProgressBar("Reconnecting Textures",
                    $"Processing material {processed}/{materialGUIDs.Length}: {mat.name}",
                    (float)processed / materialGUIDs.Length);
            }

            ProcessMaterial(mat, matPath, textureLookup);
        }

        EditorUtility.ClearProgressBar();

        if (!dryRun)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        Log($"\nDone! Fixed: {fixedCount}, Skipped: {skippedCount}, Errors: {errorCount}");
    }

    private void ProcessMaterial(Material mat, string matPath, Dictionary<string, List<Texture>> textureLookup)
    {
        if (mat.shader == null) return;

        string matNameLower = mat.name.ToLowerInvariant();
        string matDir = Path.GetDirectoryName(matPath);

        // Get all texture properties from the shader
        int propCount = ShaderUtil.GetPropertyCount(mat.shader);
        for (int i = 0; i < propCount; i++)
        {
            if (ShaderUtil.GetPropertyType(mat.shader, i) != ShaderUtil.ShaderPropertyType.TexEnv)
                continue;

            string propName = ShaderUtil.GetPropertyName(mat.shader, i);
            Texture currentTex = mat.GetTexture(propName);

            // Skip if texture is already assigned and we're not overwriting
            if (currentTex != null && !overwriteExisting)
            {
                continue;
            }

            // Try to find matching texture
            Texture matchedTexture = FindMatchingTexture(mat, propName, matNameLower, matDir, textureLookup);

            if (matchedTexture != null && matchedTexture != currentTex)
            {
                if (dryRun)
                {
                    Log($"[DRY RUN] Would set {mat.name}.{propName} = {matchedTexture.name}");
                }
                else
                {
                    mat.SetTexture(propName, matchedTexture);
                    EditorUtility.SetDirty(mat);
                    Log($"[FIXED] {mat.name}.{propName} = {matchedTexture.name}");
                }
                fixedCount++;
            }
        }
    }

    private Texture FindMatchingTexture(Material mat, string propName, string matNameLower,
        string matDir, Dictionary<string, List<Texture>> textureLookup)
    {
        // Strategy 1: Look for texture with same name as material + property suffix
        // e.g., Material "Wall_Brick" + property "_BumpMap" -> look for "Wall_Brick_Normal"
        if (SuffixToProperty.ContainsKey(propName))
        {
            // Reverse lookup not needed, we already have propName
        }

        foreach (var kvp in SuffixToProperty)
        {
            if (kvp.Key != propName) continue;

            foreach (string suffix in kvp.Value)
            {
                string searchName = (matNameLower + suffix).ToLowerInvariant();
                if (textureLookup.TryGetValue(searchName, out List<Texture> matches))
                {
                    Texture best = GetBestMatch(matches, matDir);
                    if (best != null) return best;
                }
            }
        }

        // Strategy 2: For _MainTex, try the exact material name
        if (propName == "_MainTex" || propName == "_BaseMap" || propName == "_BaseColorMap")
        {
            if (textureLookup.TryGetValue(matNameLower, out List<Texture> matches))
            {
                Texture best = GetBestMatch(matches, matDir);
                if (best != null) return best;
            }
        }

        // Strategy 3: Look for textures in the same folder as the material
        // that contain the material name and a type hint
        if (matDir != null)
        {
            string[] texGUIDs = AssetDatabase.FindAssets("t:Texture2D", new[] { matDir });
            foreach (string texGUID in texGUIDs)
            {
                string texPath = AssetDatabase.GUIDToAssetPath(texGUID);
                string texName = Path.GetFileNameWithoutExtension(texPath).ToLowerInvariant();

                // Check if texture name contains material name
                if (!texName.Contains(matNameLower) && !matNameLower.Contains(texName))
                    continue;

                // Check if texture name hints at the right property
                if (SuffixToProperty.ContainsKey(propName))
                {
                    foreach (string suffix in SuffixToProperty[propName])
                    {
                        if (texName.EndsWith(suffix) || texName.Contains(suffix))
                        {
                            return AssetDatabase.LoadAssetAtPath<Texture>(texPath);
                        }
                    }
                }

                // For main texture, accept any texture that matches the material name
                // without a suffix that maps to another property
                if (propName == "_MainTex" || propName == "_BaseMap")
                {
                    bool isOtherType = false;
                    foreach (var kvp in SuffixToProperty)
                    {
                        if (kvp.Key == "_MainTex" || kvp.Key == "_BaseMap") continue;
                        foreach (string suffix in kvp.Value)
                        {
                            if (texName.EndsWith(suffix))
                            {
                                isOtherType = true;
                                break;
                            }
                        }
                        if (isOtherType) break;
                    }

                    if (!isOtherType)
                    {
                        return AssetDatabase.LoadAssetAtPath<Texture>(texPath);
                    }
                }
            }
        }

        return null;
    }

    private Texture GetBestMatch(List<Texture> textures, string preferredDir)
    {
        if (textures.Count == 0) return null;
        if (textures.Count == 1) return textures[0];

        // Prefer textures in the same directory
        if (preferredDir != null)
        {
            foreach (Texture tex in textures)
            {
                string texPath = AssetDatabase.GetAssetPath(tex);
                if (texPath != null && Path.GetDirectoryName(texPath) == preferredDir)
                {
                    return tex;
                }
            }
        }

        return textures[0];
    }

    private Dictionary<string, List<Texture>> BuildTextureLookup()
    {
        Dictionary<string, List<Texture>> lookup = new Dictionary<string, List<Texture>>();
        string[] textureGUIDs = AssetDatabase.FindAssets("t:Texture2D", new[] { searchFolder });

        foreach (string guid in textureGUIDs)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();

            Texture tex = AssetDatabase.LoadAssetAtPath<Texture>(path);
            if (tex == null) continue;

            if (!lookup.ContainsKey(name))
            {
                lookup[name] = new List<Texture>();
            }
            lookup[name].Add(tex);
        }

        return lookup;
    }

    private void Log(string message)
    {
        logMessages.Add(message);
        Debug.Log("[MaterialTextureReconnector] " + message);
    }
}
