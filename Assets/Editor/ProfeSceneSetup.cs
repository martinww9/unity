#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Fusion.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ProfeSceneSetup
{
    const string MenuRoot = "Tools/Profe/";
    const string PrefabPath = "Assets/Prefabs/ProfeNivelSetup.prefab";
    const string JuegoScenePath = "Assets/Scenes/JuegoEscena/Juego.unity";

    static readonly string[] SetupChildNames = { "Profe", "StandPoint", "DancePoint", "TriggerStand", "TriggerDance" };

    [MenuItem(MenuRoot + "Validar escena")]
    public static void ValidateScene()
    {
        var profe = Object.FindFirstObjectByType<ProfeAnimator>();
        if (profe == null)
        {
            Debug.LogError("ProfeSceneSetup: No se encontró ProfeAnimator en la escena activa.");
            return;
        }

        Selection.activeGameObject = profe.gameObject;
        SceneView.FrameLastActiveSceneView();

        var so = new SerializedObject(profe);
        var standPoint = so.FindProperty("_standPoint").objectReferenceValue as Transform;
        var danceCenter = so.FindProperty("_danceCenter").objectReferenceValue as Transform;
        var chair = so.FindProperty("_chair").objectReferenceValue as Transform;
        var chairBackPoint = so.FindProperty("_chairBackPoint").objectReferenceValue as Transform;
        var movementPivot = GetMovementPivot(so, profe);

        Debug.Log("=== ProfeSceneSetup: validación ===", profe);

        if (movementPivot == null)
            Debug.LogWarning("FeetAnchor / _movementPivot no asignado. Usa 'Asignar FeetAnchor existente' o crea un empty bajo los pies del modelo.", profe);
        else
            Debug.Log($"Movement pivot: '{movementPivot.name}' en {movementPivot.position} (local {movementPivot.localPosition}).", movementPivot);

        if (standPoint == null)
            Debug.LogWarning("StandPoint no asignado en ProfeAnimator.", profe);
        else
        {
            LogMarkerAlignment(profe.transform, standPoint, "StandPoint (desde root)");
            if (movementPivot != null)
                LogPivotDistance(movementPivot, standPoint, "StandPoint");
        }

        if (danceCenter == null)
            Debug.LogWarning("DancePoint no asignado en ProfeAnimator.", profe);
        else
        {
            if (standPoint != null)
                LogMarkerAlignment(standPoint, danceCenter, "DancePoint (desde StandPoint)");
            else
                LogMarkerAlignment(profe.transform, danceCenter, "DancePoint");

            if (movementPivot != null)
                LogPivotDistance(movementPivot, danceCenter, "DancePoint");
        }

        if (chair == null)
            Debug.LogWarning("Chair no asignada en ProfeAnimator.", profe);
        else if (chair.gameObject.isStatic)
            Debug.LogWarning($"Silla '{chair.name}' está marcada como Static. Usa '{MenuRoot}Quitar Static de silla del Profe'.", chair);
        else
            Debug.Log($"Silla '{chair.name}': no es Static (OK).", chair);

        if (chairBackPoint != null)
            Debug.Log($"ChairBackPoint asignado: '{chairBackPoint.name}'.", chairBackPoint);
        else if (chair != null)
            Debug.Log("ChairBackPoint no asignado; se usará -chair.forward * distancia.", profe);

        float arriveThreshold = so.FindProperty("_arriveThreshold").floatValue;
        float walkArriveThreshold = so.FindProperty("_walkArriveThreshold").floatValue;
        if (arriveThreshold < 0.15f)
            Debug.LogWarning($"_arriveThreshold={arriveThreshold} (recomendado 0.15). Usa '{MenuRoot}Aplicar valores recomendados'.", profe);
        if (walkArriveThreshold > 0.05f)
            Debug.LogWarning($"_walkArriveThreshold={walkArriveThreshold} (recomendado 0.05). Usa '{MenuRoot}Aplicar valores recomendados'.", profe);

        var triggers = Object.FindObjectsByType<ProfeBarrierTrigger>(FindObjectsSortMode.None);
        Debug.Log($"Triggers encontrados: {triggers.Length}", profe);
        foreach (var trigger in triggers)
        {
            var triggerSo = new SerializedObject(trigger);
            var profeRef = triggerSo.FindProperty("_profe").objectReferenceValue;
            if (profeRef == null)
                Debug.LogWarning($"Trigger '{trigger.name}' sin Profe asignado.", trigger);
        }

        Debug.Log("Selecciona Profe en la escena para ver líneas Gizmo (StandPoint, DancePoint, retroceso silla).", profe);
    }

    [MenuItem(MenuRoot + "Colocar StandPoint delante del Profe")]
    public static void PlaceStandPointAhead()
    {
        const float standDistance = 1.2f;

        var profe = Object.FindFirstObjectByType<ProfeAnimator>();
        if (profe == null)
        {
            Debug.LogError("ProfeSceneSetup: No se encontró ProfeAnimator en la escena activa.");
            return;
        }

        var so = new SerializedObject(profe);
        var standPoint = so.FindProperty("_standPoint").objectReferenceValue as Transform;
        if (standPoint == null)
        {
            Debug.LogError("ProfeSceneSetup: ProfeAnimator no tiene StandPoint asignado.", profe);
            return;
        }

        Vector3 profePos = profe.transform.position;
        Vector3 forward = profe.transform.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 newPos = profePos + forward * standDistance;
        newPos.y = standPoint.position.y;

        Undo.RecordObject(standPoint, "Place StandPoint Ahead");
        standPoint.position = newPos;
        standPoint.rotation = profe.transform.rotation;

        EditorUtility.SetDirty(standPoint);
        EditorSceneManager.MarkSceneDirty(standPoint.gameObject.scene);

        float dot = Vector3.Dot(profe.transform.forward, (newPos - profePos).normalized);
        Selection.activeGameObject = standPoint.gameObject;
        Debug.Log($"StandPoint colocado en {newPos} (dot={dot:F2}). Guarda la escena (Ctrl+S).", standPoint);
    }

    [MenuItem(MenuRoot + "Colocar DancePoint hacia el pasillo")]
    public static void PlaceDancePointAhead()
    {
        const float danceDistance = 4f;

        var profe = Object.FindFirstObjectByType<ProfeAnimator>();
        if (profe == null)
        {
            Debug.LogError("ProfeSceneSetup: No se encontró ProfeAnimator en la escena activa.");
            return;
        }

        var so = new SerializedObject(profe);
        var standPoint = so.FindProperty("_standPoint").objectReferenceValue as Transform;
        var danceCenter = so.FindProperty("_danceCenter").objectReferenceValue as Transform;
        if (danceCenter == null)
        {
            Debug.LogError("ProfeSceneSetup: ProfeAnimator no tiene DancePoint asignado.", profe);
            return;
        }

        Transform origin = standPoint != null ? standPoint : profe.transform;
        Vector3 forward = origin.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 newPos = origin.position + forward * danceDistance;
        newPos.y = danceCenter.position.y;

        Undo.RecordObject(danceCenter, "Place DancePoint Ahead");
        danceCenter.position = newPos;
        danceCenter.rotation = origin.rotation;

        EditorUtility.SetDirty(danceCenter);
        EditorSceneManager.MarkSceneDirty(danceCenter.gameObject.scene);

        float dot = Vector3.Dot(origin.forward, (newPos - origin.position).normalized);
        Selection.activeGameObject = danceCenter.gameObject;
        Debug.Log($"DancePoint colocado en {newPos} (dot desde {origin.name}={dot:F2}). Guarda la escena (Ctrl+S).", danceCenter);
    }

    [MenuItem(MenuRoot + "Asignar FeetAnchor existente")]
    public static void AssignFeetAnchor()
    {
        var profe = Object.FindFirstObjectByType<ProfeAnimator>();
        if (profe == null)
        {
            Debug.LogError("ProfeSceneSetup: No se encontró ProfeAnimator en la escena activa.");
            return;
        }

        var feet = profe.transform.Find("FeetAnchor");
        if (feet == null)
        {
            Debug.LogError("ProfeSceneSetup: No se encontró hijo 'FeetAnchor' bajo Profe. Créalo bajo los pies del modelo.", profe);
            return;
        }

        var so = new SerializedObject(profe);
        Undo.RecordObject(profe, "Assign FeetAnchor");
        so.FindProperty("_movementPivot").objectReferenceValue = feet;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(profe);
        EditorSceneManager.MarkSceneDirty(profe.gameObject.scene);

        Selection.activeGameObject = feet.gameObject;
        Debug.Log($"FeetAnchor asignado a _movementPivot (local {feet.localPosition}). Guarda la escena (Ctrl+S).", profe);
    }

    [MenuItem(MenuRoot + "Aplicar valores recomendados")]
    public static void ApplyRecommendedValues()
    {
        var profe = Object.FindFirstObjectByType<ProfeAnimator>();
        if (profe == null)
        {
            Debug.LogError("ProfeSceneSetup: No se encontró ProfeAnimator en la escena activa.");
            return;
        }

        var so = new SerializedObject(profe);
        Undo.RecordObject(profe, "Apply Profe Recommended Values");
        so.FindProperty("_arriveThreshold").floatValue = 0.15f;
        so.FindProperty("_walkArriveThreshold").floatValue = 0.05f;
        so.FindProperty("_walkAlignAngle").floatValue = 30f;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(profe);
        EditorSceneManager.MarkSceneDirty(profe.gameObject.scene);

        Debug.Log("ProfeAnimator: _arriveThreshold=0.15, _walkArriveThreshold=0.05, _walkAlignAngle=30. Guarda la escena (Ctrl+S).", profe);
    }

    [MenuItem(MenuRoot + "Quitar Static de silla del Profe")]
    public static void UnstaticProfeChair()
    {
        var profe = Object.FindFirstObjectByType<ProfeAnimator>();
        if (profe == null)
        {
            Debug.LogError("ProfeSceneSetup: No se encontró ProfeAnimator en la escena activa.");
            return;
        }

        var so = new SerializedObject(profe);
        var chair = so.FindProperty("_chair").objectReferenceValue as Transform;
        if (chair == null)
        {
            Debug.LogError("ProfeSceneSetup: ProfeAnimator no tiene silla asignada.", profe);
            return;
        }

        if (!chair.gameObject.isStatic)
        {
            Debug.Log($"Silla '{chair.name}' ya no es Static.", chair);
            return;
        }

        Undo.RecordObject(chair.gameObject, "Unstatic Profe Chair");
        chair.gameObject.isStatic = false;
        EditorUtility.SetDirty(chair.gameObject);
        EditorSceneManager.MarkSceneDirty(chair.gameObject.scene);

        Debug.Log($"Silla '{chair.name}': Static desactivado. Guarda la escena (Ctrl+S).", chair);
    }

    static Transform GetMovementPivot(SerializedObject so, ProfeAnimator profe)
    {
        var pivot = so.FindProperty("_movementPivot").objectReferenceValue as Transform;
        if (pivot != null)
            return pivot;

        return profe.transform.Find("FeetAnchor");
    }

    static void LogPivotDistance(Transform pivot, Transform marker, string label)
    {
        Vector3 delta = marker.position - pivot.position;
        delta.y = 0f;
        float dist = delta.magnitude;
        if (dist < 0.05f)
            Debug.Log($"{label}: distancia pivot→marcador = {dist:F3}m (OK, < 5 cm).", marker);
        else
            Debug.Log($"{label}: distancia pivot→marcador = {dist:F3}m. Ajusta el marcador o FeetAnchor para que marque la posición de los pies.", marker);
    }

    static void LogMarkerAlignment(Transform profeTransform, Transform marker, string label)
    {
        Vector3 toMarker = marker.position - profeTransform.position;
        toMarker.y = 0f;
        if (toMarker.sqrMagnitude < 0.001f)
        {
            Debug.LogWarning($"{label} está en la misma posición que el Profe.", marker);
            return;
        }

        float dot = Vector3.Dot(profeTransform.forward, toMarker.normalized);
        if (dot < 0f)
            Debug.LogWarning($"{label} está DETRÁS del Profe (dot={dot:F2}). Colócalo delante de la flecha azul del Transform.", marker);
        else
            Debug.Log($"{label} alineación OK (dot={dot:F2}).", marker);
    }

    [MenuItem(MenuRoot + "Crear prefab ProfeNivelSetup desde Mundo_1")]
    public static void CreateProfeNivelSetupPrefabFromMundo1()
    {
        if (!EnsureJuegoSceneOpen())
            return;

        if (!TryBuildProfeNivelSetupRoot(out var root, out var mundo1))
            return;

        EnsurePrefabDirectory();
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        if (prefab == null)
        {
            Debug.LogError($"ProfeSceneSetup: no se pudo guardar el prefab en {PrefabPath}.");
            return;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"ProfeSceneSetup: prefab guardado en {PrefabPath}.", prefab);
    }

    [MenuItem(MenuRoot + "Instalar Profe en Mundo 2 y 3")]
    public static void InstallProfeInMundo2And3()
    {
        if (!EnsureJuegoSceneOpen())
            return;

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"ProfeSceneSetup: no existe {PrefabPath}; creándolo desde Mundo_1.");
            CreateProfeNivelSetupPrefabFromMundo1();
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }

        if (prefab == null)
        {
            Debug.LogError("ProfeSceneSetup: no se pudo cargar ProfeNivelSetup.prefab.");
            return;
        }

        var scene = SceneManager.GetActiveScene();
        int installed = 0;

        foreach (var mundoName in new[] { "Mundo_2", "Mundo_3" })
        {
            var mundo = FindMundoTransform(mundoName);
            if (mundo == null)
            {
                Debug.LogError($"ProfeSceneSetup: no se encontró '{mundoName}' en la escena.");
                continue;
            }

            RemoveProfeTestInstances(mundo);
            if (mundo.Find("ProfeNivelSetup") != null)
            {
                Debug.Log($"ProfeSceneSetup: '{mundoName}' ya tiene ProfeNivelSetup; se omite.", mundo);
                continue;
            }

            var instance = PrefabUtility.InstantiatePrefab(prefab, mundo) as GameObject;
            if (instance == null)
            {
                Debug.LogError($"ProfeSceneSetup: falló instanciar prefab en '{mundoName}'.");
                continue;
            }

            instance.name = "ProfeNivelSetup";
            var instanceTransform = instance.transform;
            instanceTransform.localPosition = Vector3.zero;
            instanceTransform.localRotation = Quaternion.identity;
            instanceTransform.localScale = Vector3.one;

            UnstaticChairsInSetup(instance);
            installed++;
            Debug.Log($"ProfeSceneSetup: ProfeNivelSetup instalado en '{mundoName}'.", instance);
        }

        if (installed > 0)
        {
            NetworkObjectPostprocessor.BakeScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"ProfeSceneSetup: escena guardada ({installed} instalación(es)).");
        }
    }

    [MenuItem(MenuRoot + "Validar los 3 profesores")]
    public static void ValidateThreeProfesMenu()
    {
        ValidateThreeProfes(silent: false);
    }

    public static bool ValidateThreeProfes(bool silent)
    {
        if (!EnsureJuegoSceneOpen())
            return false;

        var profes = Object.FindObjectsByType<ProfeAnimator>(FindObjectsSortMode.None);
        var triggers = Object.FindObjectsByType<ProfeBarrierTrigger>(FindObjectsSortMode.None);
        bool ok = true;

        if (!silent)
            Debug.Log("=== ProfeSceneSetup: validación de 3 profesores ===");

        if (profes.Length < 3)
        {
            if (!silent)
                Debug.LogError($"Se encontraron {profes.Length} ProfeAnimator; se esperaban al menos 3.");
            ok = false;
        }

        var mundoNames = new[] { "Mundo_1", "Mundo_2", "Mundo_3" };
        foreach (var mundoName in mundoNames)
        {
            var mundo = FindMundoTransform(mundoName);
            if (mundo == null)
            {
                if (!silent)
                    Debug.LogError($"No se encontró '{mundoName}'.");
                ok = false;
                continue;
            }

            var profeInMundo = profes.FirstOrDefault(p => p.transform.IsChildOf(mundo));
            if (profeInMundo == null)
            {
                if (!silent)
                    Debug.LogError($"'{mundoName}' no tiene ProfeAnimator.");
                ok = false;
                continue;
            }

            var so = new SerializedObject(profeInMundo);
            var standPoint = so.FindProperty("_standPoint").objectReferenceValue as Transform;
            var danceCenter = so.FindProperty("_danceCenter").objectReferenceValue as Transform;
            var chair = so.FindProperty("_chair").objectReferenceValue as Transform;
            var chairBackPoint = so.FindProperty("_chairBackPoint").objectReferenceValue as Transform;
            var movementPivot = GetMovementPivot(so, profeInMundo);

            if (standPoint == null || danceCenter == null || chair == null || movementPivot == null)
            {
                if (!silent)
                    Debug.LogError($"'{mundoName}': ProfeAnimator con referencias incompletas.", profeInMundo);
                ok = false;
            }
            else if (!standPoint.IsChildOf(mundo) || !danceCenter.IsChildOf(mundo))
            {
                if (!silent)
                    Debug.LogError($"'{mundoName}': StandPoint/DancePoint fuera del mundo.", profeInMundo);
                ok = false;
            }
            else if (chair.gameObject.isStatic)
            {
                if (!silent)
                    Debug.LogWarning($"'{mundoName}': silla '{chair.name}' sigue marcada Static.", chair);
                ok = false;
            }

            var mundoTriggers = triggers.Where(t => t.transform.IsChildOf(mundo)).ToList();
            if (mundoTriggers.Count < 2)
            {
                if (!silent)
                    Debug.LogError($"'{mundoName}': se encontraron {mundoTriggers.Count} triggers; se esperaban 2.", profeInMundo);
                ok = false;
            }

            foreach (var trigger in mundoTriggers)
            {
                var triggerSo = new SerializedObject(trigger);
                var profeRef = triggerSo.FindProperty("_profe").objectReferenceValue as ProfeAnimator;
                if (profeRef != profeInMundo)
                {
                    if (!silent)
                        Debug.LogError($"'{mundoName}': trigger '{trigger.name}' no apunta al Profe local.", trigger);
                    ok = false;
                }
            }

            var profeTest = FindDeepChild(mundo, "profe_test");
            if (profeTest != null)
            {
                if (!silent)
                    Debug.LogError($"'{mundoName}': aún existe profe_test.", profeTest);
                ok = false;
            }

            if (ok && !silent)
                Debug.Log($"'{mundoName}': OK (Profe + triggers + marcadores).", profeInMundo);
        }

        if (!silent)
        {
            if (ok)
                Debug.Log("Validación completada: 3 profesores OK.");
            else
                Debug.LogError("Validación completada con errores.");
        }

        return ok;
    }

    static bool EnsureJuegoSceneOpen()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path == JuegoScenePath)
            return true;

        if (!System.IO.File.Exists(JuegoScenePath))
        {
            Debug.LogError($"ProfeSceneSetup: escena no encontrada en {JuegoScenePath}.");
            return false;
        }

        if (Application.isBatchMode)
        {
            scene = EditorSceneManager.OpenScene(JuegoScenePath, OpenSceneMode.Single);
            return scene.IsValid();
        }

        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            scene = EditorSceneManager.OpenScene(JuegoScenePath, OpenSceneMode.Single);
            return scene.IsValid();
        }

        return false;
    }

    static Transform FindMundoTransform(string mundoName)
    {
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == mundoName)
                return root.transform;

            var nested = root.transform.Find(mundoName);
            if (nested != null)
                return nested;
        }

        return null;
    }

    static Transform FindDeepChild(Transform parent, string childName)
    {
        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName)
                return child;
        }

        return null;
    }

    static void EnsurePrefabDirectory()
    {
        if (AssetDatabase.IsValidFolder("Assets/Prefabs"))
            return;
        AssetDatabase.CreateFolder("Assets", "Prefabs");
    }

    static bool TryBuildProfeNivelSetupRoot(out GameObject root, out Transform mundo)
    {
        root = null;
        mundo = FindMundoTransform("Mundo_1");
        if (mundo == null)
        {
            Debug.LogError("ProfeSceneSetup: no se encontró Mundo_1.");
            return false;
        }

        var sourceProfe = mundo.Find("Profe")?.GetComponent<ProfeAnimator>();
        if (sourceProfe == null)
        {
            Debug.LogError("ProfeSceneSetup: Mundo_1 no tiene Profe con ProfeAnimator.");
            return false;
        }

        var sourceSo = new SerializedObject(sourceProfe);
        var sourceChair = sourceSo.FindProperty("_chair").objectReferenceValue as Transform;
        var sourceSillaPoint = sourceSo.FindProperty("_chairBackPoint").objectReferenceValue as Transform;
        if (sourceChair == null || sourceSillaPoint == null)
        {
            Debug.LogError("ProfeSceneSetup: Profe de Mundo_1 sin silla o SillaPoint asignados.");
            return false;
        }

        root = new GameObject("ProfeNivelSetup");
        var rootTransform = root.transform;

        var copies = new Dictionary<string, Transform>();
        foreach (var childName in SetupChildNames)
        {
            var src = mundo.Find(childName);
            if (src == null)
            {
                Debug.LogError($"ProfeSceneSetup: Mundo_1 no tiene '{childName}'.");
                Object.DestroyImmediate(root);
                root = null;
                return false;
            }

            var copy = Object.Instantiate(src.gameObject);
            copy.name = childName;
            var copyTransform = copy.transform;
            copyTransform.SetParent(rootTransform, false);
            copyTransform.localPosition = src.localPosition;
            copyTransform.localRotation = src.localRotation;
            copyTransform.localScale = src.localScale;
            copies[childName] = copyTransform;
        }

        var chairCopy = Object.Instantiate(sourceChair.gameObject);
        chairCopy.name = sourceChair.name;
        ParentRelativeToMundo(chairCopy.transform, rootTransform, sourceChair, mundo);

        var sillaCopy = Object.Instantiate(sourceSillaPoint.gameObject);
        sillaCopy.name = "SillaPoint";
        ParentRelativeToMundo(sillaCopy.transform, rootTransform, sourceSillaPoint, mundo);

        var newProfe = copies["Profe"].GetComponent<ProfeAnimator>();
        var feet = copies["Profe"].Find("FeetAnchor");
        RewireProfeAnimator(newProfe, copies["StandPoint"], copies["DancePoint"], chairCopy.transform, sillaCopy.transform, feet);
        RewireBarrierTrigger(copies["TriggerStand"].GetComponent<ProfeBarrierTrigger>(), newProfe, ProfeBarrierTrigger.ProfeBarrierAction.StandUp);
        RewireBarrierTrigger(copies["TriggerDance"].GetComponent<ProfeBarrierTrigger>(), newProfe, ProfeBarrierTrigger.ProfeBarrierAction.Dance);

        UnstaticChairsInSetup(root);
        return true;
    }

    static void ParentRelativeToMundo(Transform copy, Transform root, Transform source, Transform mundo)
    {
        copy.SetParent(root, false);
        copy.localPosition = mundo.InverseTransformPoint(source.position);
        copy.localRotation = Quaternion.Inverse(mundo.rotation) * source.rotation;
        copy.localScale = source.localScale;
    }

    static void RewireProfeAnimator(
        ProfeAnimator profe,
        Transform standPoint,
        Transform danceCenter,
        Transform chair,
        Transform chairBackPoint,
        Transform movementPivot)
    {
        var so = new SerializedObject(profe);
        so.FindProperty("_standPoint").objectReferenceValue = standPoint;
        so.FindProperty("_danceCenter").objectReferenceValue = danceCenter;
        so.FindProperty("_chair").objectReferenceValue = chair;
        so.FindProperty("_chairBackPoint").objectReferenceValue = chairBackPoint;
        so.FindProperty("_movementPivot").objectReferenceValue = movementPivot;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(profe);
    }

    static void RewireBarrierTrigger(ProfeBarrierTrigger trigger, ProfeAnimator profe, ProfeBarrierTrigger.ProfeBarrierAction action)
    {
        var so = new SerializedObject(trigger);
        so.FindProperty("_profe").objectReferenceValue = profe;
        so.FindProperty("_action").enumValueIndex = (int)action;
        so.FindProperty("_triggerOnce").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(trigger);
    }

    static void RemoveProfeTestInstances(Transform mundo)
    {
        var toRemove = new List<GameObject>();
        foreach (var child in mundo.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == "profe_test")
                toRemove.Add(child.gameObject);
        }

        foreach (var go in toRemove)
            Object.DestroyImmediate(go);
    }

    static void UnstaticChairsInSetup(GameObject setupRoot)
    {
        foreach (var profe in setupRoot.GetComponentsInChildren<ProfeAnimator>(true))
        {
            var so = new SerializedObject(profe);
            var chair = so.FindProperty("_chair").objectReferenceValue as Transform;
            if (chair != null && chair.gameObject.isStatic)
            {
                chair.gameObject.isStatic = false;
                EditorUtility.SetDirty(chair.gameObject);
            }
        }
    }
}
#endif
