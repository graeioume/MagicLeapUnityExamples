using System;
using UnityEngine;


/// <summary>
/// Attribute that marks a class as a Game Service
/// </summary>
[AttributeUsage(System.AttributeTargets.Class)]
public class GameService : Attribute
{
	public ServicePriority Priority { get; private set; }
	public ServiceVerbosity Verbosity { get; private set; }
	public GameService(ServicePriority priority = ServicePriority.Game, ServiceVerbosity verbosity = ServiceVerbosity.Info) 
	{
		this.Priority = priority;
		this.Verbosity = verbosity;
	}
}


/// <summary>
/// Base class for all game services.
/// Services are initialized on game startup and updated via the Tick method
/// On game shutdown, deinitialize is called automatically
/// </summary>
public abstract class BaseService : MonoBehaviour
{
	public string Name;
	public ServicePriority Priority { get; set; }
	public ServiceVerbosity Verbosity { get; set; }

	public virtual void Initialize() { }
	public virtual void Tick(float deltaTime) { }
	public virtual void Deinitialize() { }

    static bool _printStartupLogs = false;

    private void OnDestroy()
	{
		this.Deinitialize();
	}

	protected void Info(string message)
	{
		if (this.Verbosity <= ServiceVerbosity.Info)
        {
#if !UNITY_SERVER
            if (_printStartupLogs)
                Debug.Log($"<color=cyan>[{this.GetType().Name}]:</color> {message}");
#else
			Debug.Log($"[{this.GetType().Name}]: {message}");
#endif	
        }
    }

	protected void Warn(string message)
	{
		if (this.Verbosity <= ServiceVerbosity.Warning)
#if !UNITY_SERVER
			Debug.LogWarning($"<color=cyan>[{this.GetType().Name}]:</color> {message}");
#else
			Debug.LogWarning($"[{this.GetType().Name}]: {message}");
#endif
	}

	protected void Error(string message)
	{
		if (this.Verbosity <= ServiceVerbosity.Error)
#if !UNITY_SERVER
			Debug.LogError($"<color=cyan>[{this.GetType().Name}]:</color> {message}");
#else
			Debug.LogError($"[{this.GetType().Name}]: {message}");
#endif
	}
}

public enum ServicePriority : byte
{
	LowLevel,
	Game,
}
public enum ServiceVerbosity : byte
{
	Info,
	Warning, 
	Error, 
	None
}