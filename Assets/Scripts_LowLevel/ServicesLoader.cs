using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Class responsible for creating the Services object after the 
/// first game scene has loaded
/// </summary>
public static class ServicesLoader
{
	public static string FirstLoadedScene { get; private set; }

	/// <summary>
	/// This method is always called only once and after the very first scene has been loaded
	/// </summary>
	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void LoadServices()
	{
		FirstLoadedScene = SceneManager.GetActiveScene().name;

		GameObject system = new GameObject("[Services]");
		system.AddComponent<ServicesManager>();
	}
}

