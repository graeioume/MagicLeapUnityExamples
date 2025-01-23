using System.Text;
using UnityEngine;

public static class LogsSaver
{
    public static StringBuilder Logs;

    public static void Initialize()
    {
		Logs = new StringBuilder(16384);
		Application.logMessageReceived += Application_logMessageReceived;
    }

	private static void Application_logMessageReceived(string condition, string stackTrace, LogType type)
	{
		Logs.AppendLine($"{Time.frameCount} [{type}: {condition}");
		if (type == LogType.Error)
			Logs.AppendLine(stackTrace);
	}
}
