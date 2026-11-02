using UnityEngine;
using UnityEngine.SceneManagement;

// Shared scene transitions ensure restarting/returning also recovers from a paused game.
public static class SceneNavigation
{
    public const string MainMenuPath = "Assets/_Project/Scenes/Main menu.unity";
    public const string GameplayPath = "Assets/_Project/Scenes/Level_Art.unity";

    public static bool Load(string path, Object context)
    {
        int index = SceneUtility.GetBuildIndexByScenePath(path);
        if (index < 0 || !Application.CanStreamedLevelBeLoaded(index))
        {
            Debug.LogError("Add the destination scene to Build Settings: " + path, context);
            return false;
        }
        PlayerPrefs.Save();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        return UiTransition.LoadScene(index);
    }

    public static void Quit()
    {
        PlayerPrefs.Save();
        Time.timeScale = 1;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
