using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MissingScriptFinder
{
    [MenuItem("Tools/AlienPurge/Find Missing Scripts In Open Scenes")]
    private static void FindMissingScriptsInOpenScenes()
    {
        int missingObjectCount = 0;
        int missingScriptCount = 0;

        Debug.Log(
            "===== Missing Script Scan Started ====="
        );

        for (int sceneIndex = 0;
             sceneIndex < SceneManager.sceneCount;
             sceneIndex++)
        {
            Scene scene =
                SceneManager.GetSceneAt(sceneIndex);

            if (!scene.isLoaded)
            {
                continue;
            }

            GameObject[] rootObjects =
                scene.GetRootGameObjects();

            foreach (GameObject rootObject in rootObjects)
            {
                ScanGameObjectRecursive(
                    rootObject,
                    scene.name,
                    ref missingObjectCount,
                    ref missingScriptCount
                );
            }
        }

        Debug.Log(
            "===== Missing Script Scan Finished =====\n"
            + "Objects With Missing Scripts: "
            + missingObjectCount
            + "\nTotal Missing Scripts: "
            + missingScriptCount
        );
    }


    private static void ScanGameObjectRecursive(
        GameObject gameObject,
        string sceneName,
        ref int missingObjectCount,
        ref int missingScriptCount)
    {
        int missingCount =
            GameObjectUtility
                .GetMonoBehavioursWithMissingScriptCount(
                    gameObject
                );

        if (missingCount > 0)
        {
            missingObjectCount++;
            missingScriptCount += missingCount;

            Debug.LogWarning(
                "[Missing Script]\n"
                + "Scene: "
                + sceneName
                + "\nPath: "
                + GetHierarchyPath(
                    gameObject.transform
                )
                + "\nMissing Component Count: "
                + missingCount,
                gameObject
            );
        }

        Transform transform =
            gameObject.transform;

        for (int i = 0;
             i < transform.childCount;
             i++)
        {
            ScanGameObjectRecursive(
                transform.GetChild(i).gameObject,
                sceneName,
                ref missingObjectCount,
                ref missingScriptCount
            );
        }
    }


    private static string GetHierarchyPath(
        Transform target)
    {
        string path =
            target.name;

        Transform current =
            target.parent;

        while (current != null)
        {
            path =
                current.name
                + "/"
                + path;

            current =
                current.parent;
        }

        return path;
    }
}