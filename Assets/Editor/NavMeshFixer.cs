using UnityEditor;
using UnityEngine;
using Unity.AI.Navigation;
using UnityEngine.AI;

public class NavMeshFixer
{
    // --- 1. CZYSZCZENIE ZAZNACZONEGO OBIEKTU (I JEGO DZIECI) ---
    [MenuItem("Tools/NavMesh/Usun NavMeshModifier z ZAZNACZONEGO obiektu (i dzieci)")]
    public static void RemoveFromSelected()
    {
        if (Selection.activeGameObject == null)
        {
            Debug.LogWarning("[NavMeshFixer] Najpierw zaznacz obiekt w Hierarchy (np. Gracza)!");
            return;
        }

        NavMeshModifier[] modifiers = Selection.activeGameObject.GetComponentsInChildren<NavMeshModifier>(true);
        int count = modifiers.Length;

        Undo.IncrementCurrentGroup();
        foreach (var mod in modifiers)
        {
            Undo.DestroyObjectImmediate(mod);
        }

        Debug.Log($"[NavMeshFixer] Usunięto {count} komponentów NavMeshModifier z zaznaczonego obiektu i jego dzieci.");
    }

    // --- 2. PEŁNY RESET SCENY ---
    [MenuItem("Tools/NavMesh/Usun WSZYSTKIE NavMeshModifier ze sceny")]
    public static void RemoveAllFromScene()
    {
        NavMeshModifier[] allModifiers = Object.FindObjectsByType<NavMeshModifier>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int count = allModifiers.Length;

        Undo.IncrementCurrentGroup();
        foreach (var mod in allModifiers)
        {
            Undo.DestroyObjectImmediate(mod);
        }

        Debug.Log($"[NavMeshFixer] Wyczyść scenę: usunięto łącznie {count} komponentów NavMeshModifier.");
    }

    // --- 3. INTELIGENTNE NAKŁADANIE PRZESZKÓD ---
    [MenuItem("Tools/NavMesh/Oznacz Default jako Not Walkable (Bezpiecznie)")]
    public static void FixDefaultLayerNavMesh()
    {
        int defaultLayer = LayerMask.NameToLayer("Default");
        GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        int modifiedCount = 0;
        Undo.IncrementCurrentGroup();

        foreach (GameObject go in allObjects)
        {
            // Sprawdzamy obiekt, jego rodziców oraz nazwę główną (Root)
            bool isPlayerHierarchy = 
                go.CompareTag("Player") || 
                go.transform.root.CompareTag("Player") ||
                go.transform.root.name.ToLower().Contains("player") ||
                go.GetComponentInParent<CharacterController>() != null ||
                go.GetComponentInParent<NavMeshAgent>() != null;

            // Jeśli to gracz lub cokolwiek w jego hierarchii - pomijamy!
            if (isPlayerHierarchy)
            {
                // Jeśli przez przypadek ma modyfikator – usuwamy go
                NavMeshModifier badMod = go.GetComponent<NavMeshModifier>();
                if (badMod != null)
                {
                    Undo.DestroyObjectImmediate(badMod);
                }
                continue;
            }

            // Działamy tylko na warstwie Default posiadającej Collider
            if (go.layer == defaultLayer && go.GetComponent<Collider>() != null)
            {
                NavMeshModifier modifier = go.GetComponent<NavMeshModifier>();
                if (modifier == null)
                {
                    modifier = Undo.AddComponent<NavMeshModifier>(go);
                }

                Undo.RecordObject(modifier, "Set Not Walkable");
                modifier.overrideArea = true;
                modifier.area = 1; // 1 = Not Walkable
                modifiedCount++;
            }
        }

        Debug.Log($"[NavMeshFixer] Gotowe! Zaktualizowano {modifiedCount} przeszkód na warstwie Default (gracz został zignorowany).");
    }
}