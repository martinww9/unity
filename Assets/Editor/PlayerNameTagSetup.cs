#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class PlayerNameTagSetup
{
    const string MenuRoot = "Tools/Player/";
    const string PrefabPath = "Assets/Prefabs/PlayerPrefab.prefab";

    [MenuItem(MenuRoot + "Setup NameTag")]
    public static void SetupNameTag()
    {
        var prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (prefabRoot == null)
        {
            Debug.LogError($"PlayerNameTagSetup: No se encontró {PrefabPath}");
            return;
        }

        Transform existing = prefabRoot.transform.Find("NameTag");
        if (existing == null)
        {
            var nameTagGo = new GameObject("NameTag");
            nameTagGo.transform.SetParent(prefabRoot.transform, false);
            nameTagGo.transform.localPosition = Vector3.zero;
            nameTagGo.transform.localRotation = Quaternion.identity;
            nameTagGo.transform.localScale = Vector3.one;
            nameTagGo.AddComponent<PlayerNameTag>();
        }
        else if (existing.GetComponent<PlayerNameTag>() == null)
        {
            existing.gameObject.AddComponent<PlayerNameTag>();
        }

        PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
        PrefabUtility.UnloadPrefabContents(prefabRoot);
        Debug.Log("PlayerNameTagSetup: NameTag añadido a PlayerPrefab. Guarda y prueba en Play Mode.");
    }
}
#endif
