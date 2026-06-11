#if UNITY_EDITOR
using UnityEditor;

public static class ProfeNivelSetupBatch
{
    public static void RunAll()
    {
        ProfeSceneSetup.CreateProfeNivelSetupPrefabFromMundo1();
        ProfeSceneSetup.InstallProfeInMundo2And3();
        if (!ProfeSceneSetup.ValidateThreeProfes(silent: false))
            EditorApplication.Exit(1);
        EditorApplication.Exit(0);
    }
}
#endif
