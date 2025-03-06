using System.IO;
using System.Text;
using UnityEngine;

[GameService]
public class LogsSaver : BaseService
{
    public static StringBuilder Logs;

    public override void Initialize()
    {
		Logs = new StringBuilder(16384);
		Application.logMessageReceived += Application_logMessageReceived;
        Application.quitting += Deinitialize;
    }

    private void OnApplicationFocus(bool focus) => Deinitialize();
    private void OnApplicationPause(bool pause) => Deinitialize();

    public override void Deinitialize()
    {
        string path = Path.Combine(Application.persistentDataPath, "img", "Logs.txt");
        File.WriteAllText(path, Logs.ToString());
    }

    private void Application_logMessageReceived(string condition, string stackTrace, LogType type)
	{
		if (type == LogType.Error || type == LogType.Assert || type == LogType.Exception)
        {
            Logs.AppendLine();
            Logs.AppendLine($"{Time.frameCount} [{type}]: {condition}");
            Logs.AppendLine(stackTrace);
            Logs.AppendLine();
        }
        else
        {
            Logs.AppendLine($"{Time.frameCount} [{type}]: {condition}");
        }
	}
}
