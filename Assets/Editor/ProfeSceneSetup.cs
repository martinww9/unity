#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ProfeSceneSetup
{
    const string MenuRoot = "Tools/Profe/";

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
}
#endif
