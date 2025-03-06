

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Profiling;
using Object = UnityEngine.Object;

/// <summary>
/// Class responsible for finding all types with the [GameService] and [LowLevelService]
/// attributes and instantiating a singleton copy of each.
/// This is triggered by ServiceLoader.cs after the first scene has been loaded
/// This class also handles the Tick and Deinitialize of all services.
/// 
/// A service is a functionality that is needed throughout the entire application life cycle
/// and works behind the scenes to provide some specific feature.
/// 
/// All services marked with [LowLevelService] execute first and services marked with
/// [GameService] execute later.
/// 
/// All services must also inherit from BaseService or BaseServiceWithGUI
/// </summary>
public class ServicesManager : MonoBehaviour
{
	private static BaseService[] Services { get; set; }

	private void Awake()
	{
		/// On awake, we mark this object as DontDestroyOnLoad
		/// and we query the current assembly for all [LowLevelService]
		/// and all [GameService]. These are added to the Services list
		/// and execution order is in alphabetical order, with all
		/// [LowLevelService] executing first
		Object.DontDestroyOnLoad(this);

		List<(Type type, GameService attribute)> descriptors = FindServices();
		Services = new BaseService[descriptors.Count];
		for (int i = 0; i < descriptors.Count; i++)
			Services[i] = InstantiateService(descriptors[i].type, descriptors[i].attribute);

		// Services are only initialized after all services have been created
		foreach (BaseService service in Services)
		{
			try
			{
				service.Name = service.GetType().Name;
				Debug.Log($"Initializing {service.Name}");
                service.Initialize();
			}
			catch (Exception ex)
			{
				Debug.LogException(ex);
			}
		}
	}

	private void Update()
	{
		foreach (BaseService service in Services)
		{
			Profiler.BeginSample(service.Name);
			try
			{
				service.Tick(Time.deltaTime);
			}
			catch (Exception ex)
			{
				Debug.LogException(ex, service);
			}

			Profiler.EndSample();
		}
	}

	private void OnDestroy()
	{
		// Each service calls its own Deinitialize method on its OnDestroy
		// so we don't need to call it here
		// This solves some script execution order issues
		Services = null;
	}

	private List<(Type type, GameService attribute)> FindServices()
	{
		List<(Type type, GameService attribute)> services = new(64);
		Type gameServiceType = typeof(GameService);
		Type baseSeriveType = typeof(BaseService);
		foreach (Type t in this.GetType().Assembly.GetTypes())
			if (t.IsDefined(gameServiceType) && t.IsSubclassOf(baseSeriveType))
				services.Add((t, t.GetCustomAttribute<GameService>()));

		int Comparer((Type t, GameService att) a, (Type t, GameService att) b)
		{
			if (a.att.Priority == b.att.Priority)
				return a.t.Name.CompareTo(b.t.Name);
			return a.att.Priority.CompareTo(b.att.Priority);
		}

		services.Sort(Comparer);
		return services;
	}
	private BaseService InstantiateService(Type serviceType, GameService attribute)
	{
		GameObject serviceContainer = new GameObject();
		GameObject.DontDestroyOnLoad(serviceContainer);
		serviceContainer.name = $"[{serviceType.Name}]";
		BaseService service = (BaseService)serviceContainer.AddComponent(serviceType);
		service.Priority = attribute.Priority;
		service.Verbosity = attribute.Verbosity;

		return service;
	}
}

