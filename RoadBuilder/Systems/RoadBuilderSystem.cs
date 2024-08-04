﻿using Colossal.Entities;
using Colossal.Serialization.Entities;

using Game;
using Game.City;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Game.SceneFlow;
using Game.Tools;
using Game.UI.InGame;

using RoadBuilder.Domain.Components;
using RoadBuilder.Domain.Configurations;
using RoadBuilder.Domain.Prefabs;
using RoadBuilder.Systems.UI;
using RoadBuilder.Utilities;

using System;
using System.Collections.Generic;

using Unity.Collections;
using Unity.Entities;

namespace RoadBuilder.Systems
{
	public partial class RoadBuilderSystem : GameSystemBase
	{
		private readonly Queue<INetworkBuilderPrefab> _updatedRoadPrefabsQueue = new();

		private EntityQuery prefabRefQuery;
		private RoadNameUtil roadNameUtil;
		private PrefabSystem prefabSystem;
		private PrefabUISystem prefabUISystem;
		private NetSectionsSystem netSectionsSystem;
		private RoadBuilderSerializeSystem roadBuilderSerializeSystem;
		private CityConfigurationSystem cityConfigurationSystem;
		private RoadGenerationDataSystem roadGenerationDataSystem;
		private ModificationBarrier1 modificationBarrier;
		private Dictionary<Entity, Entity> toolbarUISystemLastSelectedAssets;

		public event Action ConfigurationsUpdated;

		public Dictionary<string, INetworkBuilderPrefab> Configurations { get; } = new();

		protected override void OnCreate()
		{
			base.OnCreate();

			prefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
			prefabUISystem = World.GetOrCreateSystemManaged<PrefabUISystem>();
			netSectionsSystem = World.GetOrCreateSystemManaged<NetSectionsSystem>();
			roadBuilderSerializeSystem = World.GetOrCreateSystemManaged<RoadBuilderSerializeSystem>();
			cityConfigurationSystem = World.GetOrCreateSystemManaged<CityConfigurationSystem>();
			roadGenerationDataSystem = World.GetOrCreateSystemManaged<RoadGenerationDataSystem>();
			modificationBarrier = World.GetOrCreateSystemManaged<ModificationBarrier1>();
			roadNameUtil = new(this, World.GetOrCreateSystemManaged<RoadBuilderUISystem>(), prefabUISystem, netSectionsSystem);
			prefabRefQuery = SystemAPI.QueryBuilder()
				.WithAll<RoadBuilderNetwork, PrefabRef>()
				.WithNone<Temp>()
				.Build();

			// Delay getting the toolbar ui system assets for the next frame
			GameManager.instance.RegisterUpdater(() => toolbarUISystemLastSelectedAssets ??= typeof(ToolbarUISystem).GetField("m_LastSelectedAssets", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(World.GetOrCreateSystemManaged<ToolbarUISystem>()) as Dictionary<Entity, Entity>);
		}

		protected override void OnUpdate()
		{
			if (_updatedRoadPrefabsQueue.Count == 0)
			{
				return;
			}

			do
			{
				if (roadGenerationDataSystem.RoadGenerationData is null)
				{
					Mod.Log.Warn("Generating roads before generation data was initialized");

					_updatedRoadPrefabsQueue.Clear();

					return;
				}

				var roadPrefab = _updatedRoadPrefabsQueue.Dequeue();
				var roadPrefabGeneration = new NetworkPrefabGenerationUtil(roadPrefab, roadGenerationDataSystem.RoadGenerationData);

				roadPrefabGeneration.GenerateRoad();

				roadPrefab.Prefab.name = roadPrefab.Config.ID;

				UpdatePrefab(roadPrefab.Prefab);
			}
			while (_updatedRoadPrefabsQueue.Count > 0);
		}

		protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
		{
			base.OnGameLoadingComplete(purpose, mode);

			if (mode == GameMode.Game)
			{
				GameManager.instance.localizationManager.ReloadActiveLocale();
			}
		}

		public void UpdateRoad(INetworkConfig config, Entity entity, bool createNewPrefab)
		{
			INetworkBuilderPrefab networkBuilderPrefab;

			if (entity == Entity.Null)
			{
				if (!Configurations.TryGetValue(config.ID, out networkBuilderPrefab))
				{
					return;
				}
			}
			else
			{
				if (!EntityManager.TryGetComponent<PrefabRef>(entity, out var prefabRef))
				{
					return;
				}

				if (createNewPrefab || !(prefabSystem.TryGetPrefab<NetGeometryPrefab>(prefabRef, out var netPrefab) && netPrefab is INetworkBuilderPrefab _networkBuilderPrefab))
				{
					CreateNewRoadPrefab(config, entity);

					return;
				}

				networkBuilderPrefab = _networkBuilderPrefab;
			}

			_updatedRoadPrefabsQueue.Enqueue(networkBuilderPrefab);
		}

		public INetworkConfig GetOrGenerateConfiguration(Entity entity)
		{
			if (!EntityManager.TryGetComponent<PrefabRef>(entity, out var prefabRef))
			{
				return null;
			}

			if (!prefabSystem.TryGetPrefab<NetGeometryPrefab>(prefabRef, out var roadPrefab))
			{
				return null;
			}

			if (roadPrefab is INetworkBuilderPrefab networkBuilderPrefab)
			{
				return networkBuilderPrefab.Config;
			}

			if (roadGenerationDataSystem.RoadGenerationData is null)
			{
				return null;
			}

			return new NetworkConfigGenerationUtil(roadPrefab, roadGenerationDataSystem.RoadGenerationData, prefabUISystem).GenerateConfiguration();
		}

		public INetworkConfig GenerateConfiguration(Entity entity)
		{
			if (!EntityManager.TryGetComponent<PrefabRef>(entity, out var prefabRef))
			{
				return null;
			}

			if (!prefabSystem.TryGetPrefab<NetGeometryPrefab>(prefabRef, out var roadPrefab))
			{
				return null;
			}

			if (roadGenerationDataSystem.RoadGenerationData is null)
			{
				return null;
			}

			return new NetworkConfigGenerationUtil(roadPrefab, roadGenerationDataSystem.RoadGenerationData, prefabUISystem).GenerateConfiguration();
		}

		private void CreateNewRoadPrefab(INetworkConfig config, Entity entity)
		{
			if (roadGenerationDataSystem.RoadGenerationData is null)
			{
				Mod.Log.Warn("Generating roads before generation data was initialized");

				return;
			}

			try
			{
				var roadPrefab = AddPrefab(config, generateId: true);

				if (roadPrefab is null)
				{
					return;
				}

				var prefabEntity = prefabSystem.GetEntity(roadPrefab.Prefab);
				var prefabRef = new PrefabRef { m_Prefab = prefabEntity };

				EntityManager.SetComponentData(entity, prefabRef);
				EntityManager.TryAddComponent<RoadBuilderNetwork>(entity);

				if (EntityManager.TryGetComponent<Edge>(entity, out var edge))
				{
					EntityManager.SetComponentData(edge.m_Start, prefabRef);
					EntityManager.SetComponentData(edge.m_End, prefabRef);
					EntityManager.TryAddComponent<RoadBuilderNetwork>(edge.m_Start);
					EntityManager.TryAddComponent<RoadBuilderNetwork>(edge.m_End);
				}
			}
			catch (Exception ex)
			{
				Mod.Log.Error(ex);
			}
		}

		public void UpdatePrefab(NetGeometryPrefab prefab)
		{
			var entity = prefabSystem.GetEntity(prefab);

			prefabSystem.UpdatePrefab(prefab, entity);

			foreach (var kvp in toolbarUISystemLastSelectedAssets)
			{
				if (kvp.Value == entity)
				{
					toolbarUISystemLastSelectedAssets.Remove(kvp.Key);
					break;
				}
			}

			var uIGroupElements = SystemAPI.QueryBuilder().WithAll<UIGroupElement>().Build().ToEntityArray(Allocator.Temp);

			for (var i = 0; i < uIGroupElements.Length; i++)
			{
				var buffer = EntityManager.GetBuffer<UIGroupElement>(uIGroupElements[i]);

				for (var j = 0; j < buffer.Length; j++)
				{
					if (buffer[j].m_Prefab == entity)
					{
						buffer.RemoveAt(j);
						return;
					}
				}
			}
		}

		public INetworkBuilderPrefab AddPrefab(INetworkConfig config, bool generateId = false, bool queueForUpdate = true)
		{
			try
			{
				var roadPrefab = NetworkPrefabGenerationUtil.CreatePrefab(config);

				if (prefabSystem.TryGetPrefab(roadPrefab.Prefab.GetPrefabID(), out _))
				{
					Mod.Log.Debug("Trying to add a road that already exists: " + config.ID);

					return null;
				}

				var roadPrefabGeneration = new NetworkPrefabGenerationUtil(roadPrefab, roadGenerationDataSystem.RoadGenerationData);

				roadPrefabGeneration.GenerateRoad(generateId);

				roadPrefab.Prefab.name = roadPrefab.Config.ID;

				if (!prefabSystem.AddPrefab(roadPrefab.Prefab))
				{
					Mod.Log.Error($"Unable to add prefab {roadPrefab.Prefab.name} config name: {roadPrefab.Config.Name}");
				}

				if (queueForUpdate)
				{
					_updatedRoadPrefabsQueue.Enqueue(roadPrefab);
				}

				return roadPrefab;
			}
			catch (Exception ex)
			{
				Mod.Log.Error(ex);

				return null;
			}
		}

		public void UpdateConfigurationList()
		{
			var roadBuilderConfigsQuery = SystemAPI.QueryBuilder().WithAll<RoadBuilderPrefabData>().Build();
			var prefabs = roadBuilderConfigsQuery.ToEntityArray(Allocator.Temp);

			Configurations.Clear();

			for (var i = 0; i < prefabs.Length; i++)
			{
				if (prefabSystem.GetPrefab<PrefabBase>(prefabs[i]) is INetworkBuilderPrefab prefab && !prefab.Deleted)
				{
					Configurations[prefab.Prefab.name] = prefab;

					Mod.Log.Debug("Configuration Found: " + prefab.Prefab.name);
				}
			}

			Mod.Log.Debug("Configuration Count: " + Configurations.Count);

			ConfigurationsUpdated?.Invoke();
		}
	}
}
