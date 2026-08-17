using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public class AutomatedImporter : EditorWindow
{
    private string jsonPath = "Assets/final_scene_metadata.json";
    private string texturesFolder = "Assets/Textures";

    [MenuItem("Tools/Pipeline/2. Automate Material Import & Setup")]
    public static void ShowWindow()
    {
        GetWindow<AutomatedImporter>("Automated Material Import");
    }

    private void OnGUI()
    {
        GUILayout.Label("Automatyczna Konfiguracja Sceny", EditorStyles.boldLabel);
        
        jsonPath = EditorGUILayout.TextField("Ścieżka do JSON:", jsonPath);
        texturesFolder = EditorGUILayout.TextField("Folder Tekstur:", texturesFolder);

        if (GUILayout.Button("Uruchom automatyczny montaż materiałów"))
        {
            ProcessImport();
        }
    }

    private void ProcessImport()
    {
        if (!File.Exists(jsonPath))
        {
            Debug.LogError($"Nie znaleziono pliku metadanych: {jsonPath}");
            return;
        }

        // Ładowanie wygenerowanych automatycznie szaderów (bezpośrednio z pliku lub z pamięci)
Shader triplanarShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/TriplanarPBR.shader");
if (triplanarShader == null) triplanarShader = Shader.Find("Custom/URP_TriplanarPBR");

Shader terrainShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/Terrain3Layer.shader");
if (terrainShader == null) terrainShader = Shader.Find("Custom/URP_Terrain3Layer");

Shader standardURPShader = Shader.Find("Universal Render Pipeline/Lit");

        if (triplanarShader == null || terrainShader == null)
        {
            Debug.LogError("Brak wygenerowanych szaderów! Wykonaj najpierw krok: Tools -> Pipeline -> 1. Generate Custom URP Shaders");
            return;
        }

        string jsonContent = File.ReadAllText(jsonPath);
        var materialsData = ParseJSON(jsonContent, out string terrainMatName, out List<string> terrainSubMats);

        foreach (var entry in materialsData)
        {
            string matName = entry.Key;
            MaterialInfo info = entry.Value;

            // Precyzyjne szukanie materiału po dokładnej nazwie (odrzuca częściowe dopasowania)
            Material mat = FindExactMaterial(matName);
            if (mat == null) continue;

            // 1. Obsługa Terenu
            if (matName == terrainMatName)
            {
                mat.shader = terrainShader;
                AssignTerrainSubMaterials(mat, terrainSubMats, materialsData);
                EditorUtility.SetDirty(mat);
                continue;
            }

            // 2. Przypisanie Szadera na podstawie analizy mapowania z Blendera
            if (info.mappingType == "TRIPLANAR")
            {
                mat.shader = triplanarShader;
            }
            else
            {
                mat.shader = standardURPShader != null ? standardURPShader : Shader.Find("Standard");
            }

            // 3. Podpięcie Tekstur PBR
            AssignTexture(mat, "_BaseMap", info.albedoPath, false);
            AssignTexture(mat, "_BumpMap", info.normalPath, true);
            AssignTexture(mat, "_SpecGlossMap", info.roughnessPath, false);
            AssignTexture(mat, "_MetallicGlossMap", info.metallicPath, false);

            EditorUtility.SetDirty(mat);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("=== SUKCES: Wszystkie materiały zostały automatycznie zaktualizowane i skonsolidowane! ===");
    }

    private Material FindExactMaterial(string matName)
    {
        string[] guids = AssetDatabase.FindAssets($"{matName} t:Material");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null && mat.name == matName)
            {
                return mat;
            }
        }
        return null;
    }

    private void AssignTexture(Material mat, string propName, string texPath, bool isNormal)
    {
        if (string.IsNullOrEmpty(texPath)) return;

        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

        // Fallback: jeśli ścieżka z JSON nie istnieje bezpośrednio, spróbuj odnaleźć plik w texturesFolder
        if (tex == null)
        {
            string fileName = Path.GetFileName(texPath);
            string fallbackPath = Path.Combine(texturesFolder, fileName).Replace("\\", "/");
            tex = AssetDatabase.LoadAssetAtPath<Texture2D>(fallbackPath);
            texPath = fallbackPath;
        }

        if (tex != null)
        {
            if (isNormal)
            {
                TextureImporter importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
                if (importer != null && importer.textureType != TextureImporterType.NormalMap)
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    importer.SaveAndReimport();
                }
            }
            mat.SetTexture(propName, tex);
        }
    }

    private void AssignTerrainSubMaterials(Material terrainMat, List<string> subMats, Dictionary<string, MaterialInfo> allMats)
    {
        string[] prefixes = new string[] { "_Ground", "_Leaves", "_Cliff" };

        for (int i = 0; i < 3 && i < subMats.Count; i++)
        {
            string subMatName = subMats[i];
            
            // Pobieranie ścieżek bezpośrednio ze skanowania JSON jeśli submaterial tam istnieje
            if (allMats.TryGetValue(subMatName, out MaterialInfo info))
            {
                AssignTexture(terrainMat, $"{prefixes[i]}Albedo", info.albedoPath, false);
                AssignTexture(terrainMat, $"{prefixes[i]}Normal", info.normalPath, true);
            }
            else
            {
                // Fallback na domyślną konwencję nazw
                string albedoPath = $"{texturesFolder}/{subMatName}_Albedo.png";
                string normalPath = $"{texturesFolder}/{subMatName}_Normal.png";
                AssignTexture(terrainMat, $"{prefixes[i]}Albedo", albedoPath, false);
                AssignTexture(terrainMat, $"{prefixes[i]}Normal", normalPath, true);
            }
        }
    }

    private class MaterialInfo
    {
        public string mappingType = "UV";
        public string albedoPath = "";
        public string normalPath = "";
        public string roughnessPath = "";
        public string metallicPath = "";
    }

    private Dictionary<string, MaterialInfo> ParseJSON(string json, out string terrainMatName, out List<string> terrainSubMats)
    {
        var result = new Dictionary<string, MaterialInfo>();
        terrainMatName = "M_Terrain";
        terrainSubMats = new List<string>();

        // Bezpieczny odczyt nazwy materiału terenu
        Match terrainMatch = Regex.Match(json, @"\""material_name\""\s*:\s*\""([^\""]+)\""");
        if (terrainMatch.Success)
        {
            terrainMatName = terrainMatch.Groups[1].Value;
        }

        // Bezpieczny odczyt listy sub_materials
        Match subMatsMatch = Regex.Match(json, @"\""sub_materials\""\s*:\s*\[([^\]]+)\]");
        if (subMatsMatch.Success)
        {
            string rawGroup = subMatsMatch.Groups[1].Value;
            foreach (Match item in Regex.Matches(rawGroup, @"\""([^\""]+)\"""))
            {
                terrainSubMats.Add(item.Groups[1].Value);
            }
        }

        // Parsowanie bloków materiałów
        Match materialsBlock = Regex.Match(json, @"\""materials\""\s*:\s*\{([\s\S]*?)\}\s*,\s*\""terrain_info\""");
        string matContent = materialsBlock.Success ? materialsBlock.Groups[1].Value : json;

        MatchCollection matEntries = Regex.Matches(matContent, @"\""([^\""]+)\""\s*:\s*\{([^}]+)\}");
        foreach (Match entry in matEntries)
        {
            string matName = entry.Groups[1].Value;
            string body = entry.Groups[2].Value;

            MaterialInfo info = new MaterialInfo();

            Match mapType = Regex.Match(body, @"\""mapping_type\""\s*:\s*\""([^\""]+)\""");
            if (mapType.Success) info.mappingType = mapType.Groups[1].Value;

            Match albedo = Regex.Match(body, @"\""albedo\""\s*:\s*\""([^\""]+)\""");
            if (albedo.Success) info.albedoPath = albedo.Groups[1].Value;

            Match normal = Regex.Match(body, @"\""normal\""\s*:\s*\""([^\""]+)\""");
            if (normal.Success) info.normalPath = normal.Groups[1].Value;

            Match rough = Regex.Match(body, @"\""roughness\""\s*:\s*\""([^\""]+)\""");
            if (rough.Success) info.roughnessPath = rough.Groups[1].Value;

            Match metal = Regex.Match(body, @"\""metallic\""\s*:\s*\""([^\""]+)\""");
            if (metal.Success) info.metallicPath = metal.Groups[1].Value;

            result[matName] = info;
        }

        return result;
    }
}