using UnityEngine;
using UnityEditor;

public class MaterialSlotFixer : Editor
{
    [MenuItem("Tools/Corregir Slots de Materiales")]
    public static void FixMaterials()
    {
        GameObject[] seleccionados = Selection.gameObjects;
        int corregidos = 0;

        foreach (GameObject obj in seleccionados)
        {
            MeshRenderer[] renderers = obj.GetComponentsInChildren<MeshRenderer>();
            foreach (var renderer in renderers)
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                {
                    int subMeshCount = filter.sharedMesh.subMeshCount;
                    if (renderer.sharedMaterials.Length > subMeshCount)
                    {
                        Material[] newMaterials = new Material[subMeshCount];
                        for (int i = 0; i < subMeshCount; i++)
                        {
                            newMaterials[i] = renderer.sharedMaterials[i];
                        }
                        Undo.RecordObject(renderer, "Fix Material Slots");
                        renderer.sharedMaterials = newMaterials;
                        corregidos++;
                    }
                }
            }
        }
        Debug.Log($"¡Listo! Se han corregido slots en {corregidos} objetos.");
    }

    [MenuItem("Tools/Asignar Material '1' a Selección")]
    public static void AssignMaterialOne()
    {
        // 1. Buscar el material llamado exactamente "1"
        string[] guids = AssetDatabase.FindAssets("1 t:Material");
        Material targetMaterial = null;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            // Verificamos que el nombre sea exactamente "1" y no "Floor_1" o similares
            if (System.IO.Path.GetFileNameWithoutExtension(path) == "1")
            {
                targetMaterial = AssetDatabase.LoadAssetAtPath<Material>(path);
                break;
            }
        }

        if (targetMaterial == null)
        {
            Debug.LogError("No se encontró ningún material llamado exactamente '1'. Revisa el nombre en tu carpeta de Assets.");
            return;
        }

        // 2. Aplicar a los objetos seleccionados y sus hijos
        GameObject[] seleccionados = Selection.gameObjects;
        int aplicados = 0;

        foreach (GameObject obj in seleccionados)
        {
            MeshRenderer[] renderers = obj.GetComponentsInChildren<MeshRenderer>();
            foreach (var renderer in renderers)
            {
                Undo.RecordObject(renderer, "Asignar Material 1");
                
                // Llenamos todos los slots existentes con el material "1"
                Material[] materials = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < materials.Length; i++)
                {
                    materials[i] = targetMaterial;
                }
                
                renderer.sharedMaterials = materials;
                aplicados++;
            }
        }

        Debug.Log($"¡Éxito! Se asignó el material '{targetMaterial.name}' a {aplicados} renderers.");
    }
}