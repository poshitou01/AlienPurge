using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Editor-only test profiles. Native bootstrap/save logic stays intact, while
// these two labs cannot write the profile used by another checkout's gameplay.
[InitializeOnLoad]
public static class CoreEnemyTestProfileIsolation
{
    const string Key="CoreEnemies.TestProfile";
    static CoreEnemyTestProfileIsolation()
    {
        EditorApplication.playModeStateChanged+=Changed;
        // Batch verification can exit in an earlier EnteredEditMode subscriber.
        EditorApplication.quitting+=Restore;
    }
    static void Changed(PlayModeStateChange state)
    {
        if(state==PlayModeStateChange.ExitingEditMode)
        {
            string path=SceneManager.GetActiveScene().path;
            if(path!=CoreEnemyArtSetup.ScenePath && path!=SupportExploderSetup.ScenePath) return;
            SessionState.SetString(Key+".Company",PlayerSettings.companyName);
            SessionState.SetString(Key+".Product",PlayerSettings.productName);
            SessionState.SetBool(Key,true);
            PlayerSettings.companyName="AlienPurgeTests";
            // Each independent checkout has a different profile namespace.
            PlayerSettings.productName="EnemyArt_"+System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(Application.dataPath));
            Debug.Log("Enemy art test isolated save directory: "+Application.persistentDataPath);
        }
        if(state==PlayModeStateChange.EnteredEditMode) Restore();
    }
    public static void Restore()
    {
        if(!SessionState.GetBool(Key,false)) return;
        PlayerSettings.companyName=SessionState.GetString(Key+".Company","DefaultCompany");
        PlayerSettings.productName=SessionState.GetString(Key+".Product","AlienPurge");
        SessionState.SetBool(Key,false);
    }
}
