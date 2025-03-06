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
    }

    public override void Deinitialize()
    {
        base.Deinitialize();
        string path = Path.Combine(Application.persistentDataPath, "Logs.txt");
        File.WriteAllText(path, Logs.ToString());
    }

    private void Application_logMessageReceived(string condition, string stackTrace, LogType type)
	{
		Logs.AppendLine($"{Time.frameCount} [{type}: {condition}");
		if (type == LogType.Error)
			Logs.AppendLine(stackTrace);
	}
}
