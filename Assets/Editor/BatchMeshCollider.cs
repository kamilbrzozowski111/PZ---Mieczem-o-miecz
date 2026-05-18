using UnityEngine;
using UnityEditor;

public class BatchMeshCollider : EditorWindow
{
    [MenuItem("Tools/Physics/Add MeshColliders to Selected")]
    public static void AddColliders()
    {
        GameObject[] selectedObjects = Selection.gameObjects;
        int count = 0;

        foreach (GameObject obj in selectedObjects)
        {
            // Check if the object has a MeshFilter (needed for a MeshCollider)
            MeshFilter meshFilter = obj.GetComponent<MeshFilter>();

            if (meshFilter != null)
            {
                // Check if it already has a MeshCollider to avoid duplicates
                if (obj.GetComponent<MeshCollider>() == null)
                {
                    Undo.AddComponent<MeshCollider>(obj);
                    count++;
                }
            }
        }

        Debug.Log($"Successfully added MeshColliders to {count} objects.");
    }

    // Validation: Only enable the menu item if something is selected
    [MenuItem("Tools/Physics/Add MeshColliders to Selected", true)]
    private static bool ValidateAddColliders()
    {
        return Selection.gameObjects.Length > 0;
    }
}