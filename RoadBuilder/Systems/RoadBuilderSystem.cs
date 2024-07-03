﻿using Colossal.Entities;
using Colossal.Serialization.Entities;

using Game;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Game.Tools;
using Game.UI.InGame;

using RoadBuilder.Domain;
using RoadBuilder.Domain.Components;
using RoadBuilder.Domain.Configuration;
using RoadBuilder.Domain.Prefabs;
using RoadBuilder.Utilities;

using System.Collections.Generic;
using System.Linq;

using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;

namespace RoadBuilder.Systems
{
    public partial class RoadBuilderSystem : GameSystemBase, ISerializable, IDefaultSerializable
	{
		public const ushort CURRENT_VERSION = 1;

		private readonly Queue<INetworkBuilderPrefab> _updatedRoadPrefabsQueue = new();

		private PrefabSystem prefabSystem;
		private PrefabUISystem prefabUISystem;
		private ModificationBarrier1 modificationBarrier;
		private EntityQuery prefabRefQuery;
		private RoadGenerationData roadGenerationData;

		public List<INetworkBuilderPrefab> Configurations { get; } = new();
		public RoadGenerationData RoadGenerationData => roadGenerationData;

		protected override void OnCreate()
		{
			base.OnCreate();

			prefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
			prefabUISystem = World.GetOrCreateSystemManaged<PrefabUISystem>();
			modificationBarrier = World.GetOrCreateSystemManaged<ModificationBarrier1>();
			prefabRefQuery = SystemAPI.QueryBuilder()
				.WithAll<RoadBuilderNetwork, PrefabRef>()
				.WithNone<Updated, Temp>()
				.Build();
		}

		protected override void OnUpdate()
		{
			while (_updatedRoadPrefabsQueue.Count > 0)
			{
				if (roadGenerationData is null)
				{
					Mod.Log.Warn("Generating roads before generation data was initialized");
				}

				var roadPrefab = _updatedRoadPrefabsQueue.Dequeue();
				var roadPrefabGeneration = new RoadPrefabGenerationUtil(roadPrefab, roadGenerationData ?? new());

				roadPrefabGeneration.GenerateRoad();

				if (!roadPrefab.WasGenerated)
				{
					roadPrefab.WasGenerated = true;

					Configurations.Add(roadPrefab);
					prefabSystem.AddPrefab(roadPrefab.Prefab);

					continue;
				}

				roadPrefab.Prefab.name = roadPrefab.Config.ID;

				prefabSystem.UpdatePrefab(roadPrefab.Prefab);

				// Update all existing roads that use this road configuration
				var job = new ApplyUpdatedJob()
				{
					Prefab = prefabSystem.GetEntity(roadPrefab.Prefab),
					EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
					PrefabRefTypeHandle = SystemAPI.GetComponentTypeHandle<PrefabRef>(true),
					CommandBuffer = modificationBarrier.CreateCommandBuffer().AsParallelWriter()
				};

				JobChunkExtensions.ScheduleParallel(job, prefabRefQuery, Dependency);
			}
		}

		protected override void OnGamePreload(Purpose purpose, GameMode mode)
		{
			base.OnGamePreload(purpose, mode);

			FillRoadGenerationData();
		}

		public void Deserialize<TReader>(TReader reader) where TReader : IReader
		{
			Mod.Log.Info(nameof(Deserialize));

			Configurations.Clear();

			reader.Read(out ushort version);
			reader.Read(out int length);

			var configs = new List<INetworkConfig>();

			//for (var i = 0; i < length; i++)
			//{
			//	var config = new RoadConfig();

			//	reader.Read(config);

			//	configs.Add(config);
			//}

			foreach (var config in LoadLocalConfigs())
			{
				if (!configs.Any(x => x.ID == config.ID))
				{
					configs.Add(config);
				}
			}

			Mod.Log.Info($"{configs.Count} configurations loaded");

			InitializeExistingRoadPrefabs(configs);
		}

		public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
		{
			Mod.Log.Info(nameof(Serialize));

			writer.Write(CURRENT_VERSION);
			writer.Write(Configurations.Count);

			foreach (var roadConfig in Configurations)
			{
				//writer.Write(roadConfig);

				if (roadConfig.Config.OriginalID != roadConfig.Config.ID)
				{
					LocalSaveUtil.Save(roadConfig.Config);
				}
			}

			Mod.Log.Info(nameof(Serialize) + " End");
		}

		public void SetDefaults(Context context)
		{
			Mod.Log.Info(nameof(SetDefaults));

			Configurations.Clear();

			InitializeExistingRoadPrefabs(LoadLocalConfigs().ToList());
		}

		public void UpdateRoad(INetworkConfig config, Entity entity, bool createNewPrefab)
		{
			if (!EntityManager.TryGetComponent<PrefabRef>(entity, out var prefabRef))
			{
				return;
			}

			if (createNewPrefab || !prefabSystem.TryGetPrefab<RoadBuilderPrefab>(prefabRef, out var roadPrefab))
			{
				CreateNewRoadPrefab(config, entity);

				return;
			}

			_updatedRoadPrefabsQueue.Enqueue(roadPrefab);
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

			if (roadPrefab is RoadBuilderPrefab roadBuilderPrefab)
			{
				return roadBuilderPrefab.Config;
			}

			return new RoadConfigGenerationUtil(roadPrefab, roadGenerationData, prefabUISystem).GenerateConfiguration();
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

			return new RoadConfigGenerationUtil(roadPrefab, roadGenerationData, prefabUISystem).GenerateConfiguration();
		}

		private void InitializeExistingRoadPrefabs(List<INetworkConfig> configs)
		{
			foreach (var config in configs)
			{
				config.ApplyVersionChanges();

				INetworkBuilderPrefab roadPrefab;

				if (prefabSystem.TryGetPrefab(new PrefabID(nameof(INetworkBuilderPrefab), config.ID), out var prefabBase))
				{
					roadPrefab = prefabBase as INetworkBuilderPrefab;

					roadPrefab.Config = config;
				}
				else
				{
					roadPrefab = RoadPrefabGenerationUtil.CreatePrefab(config);

					prefabSystem.AddPrefab(roadPrefab.Prefab);
				}

				var roadPrefabGeneration = new RoadPrefabGenerationUtil(roadPrefab, roadGenerationData ?? new());

				roadPrefabGeneration.GenerateRoad();
				roadPrefab.WasGenerated = true;
				roadPrefab.Prefab.name = roadPrefab.Config.ID;

				Configurations.Add(roadPrefab);

				prefabSystem.UpdatePrefab(roadPrefab.Prefab);
			}
		}

		private void CreateNewRoadPrefab(INetworkConfig config, Entity entity)
		{
			var roadPrefab = RoadPrefabGenerationUtil.CreatePrefab(config);
			var roadPrefabGeneration = new RoadPrefabGenerationUtil(roadPrefab, roadGenerationData ?? new());

			roadPrefab.WasGenerated = true;

			roadPrefabGeneration.GenerateRoad();

			roadPrefab.Prefab.name = roadPrefab.Config.ID;

			prefabSystem.AddPrefab(roadPrefab.Prefab);

			Configurations.Add(roadPrefab);

			EntityManager.AddComponent<Updated>(entity);
			EntityManager.AddComponent<RoadBuilderNetwork>(entity);
			EntityManager.SetComponentData(entity, new PrefabRef
			{
				m_Prefab = prefabSystem.GetEntity(roadPrefab.Prefab)
			});
		}

		private void FillRoadGenerationData()
		{
			roadGenerationData = new();

			var zoneBlockDataQuery = SystemAPI.QueryBuilder().WithAll<ZoneBlockData>().Build();
			var zoneBlockDataEntities = zoneBlockDataQuery.ToEntityArray(Allocator.Temp);

			for (var i = 0; i < zoneBlockDataEntities.Length; i++)
			{
				if (prefabSystem.TryGetPrefab<ZoneBlockPrefab>(zoneBlockDataEntities[i], out var prefab))
				{
					if (prefab.name == "Zone Block")
					{
						roadGenerationData.ZoneBlockPrefab = prefab;

						break;
					}
				}
			}

			var outsideConnectionDataQuery = SystemAPI.QueryBuilder().WithAll<OutsideConnectionData, TrafficSpawnerData>().Build();
			var outsideConnectionDataEntities = outsideConnectionDataQuery.ToEntityArray(Allocator.Temp);

			for (var i = 0; i < outsideConnectionDataEntities.Length; i++)
			{
				if (prefabSystem.TryGetPrefab<MarkerObjectPrefab>(outsideConnectionDataEntities[i], out var prefab))
				{
					if (prefab.name == "Road Outside Connection - Oneway")
					{
						roadGenerationData.OutsideConnectionOneWay = prefab;
					}

					if (prefab.name == "Road Outside Connection - Twoway")
					{
						roadGenerationData.OutsideConnectionTwoWay = prefab;
					}
				}
			}

			var aggregateNetDataQuery = SystemAPI.QueryBuilder().WithAll<AggregateNetData>().Build();
			var aggregateNetDataEntities = aggregateNetDataQuery.ToEntityArray(Allocator.Temp);

			for (var i = 0; i < aggregateNetDataEntities.Length; i++)
			{
				if (prefabSystem.TryGetPrefab<AggregateNetPrefab>(aggregateNetDataEntities[i], out var prefab))
				{
					roadGenerationData.AggregateNetPrefabs[prefab.name] = prefab;
				}
			}

			var netSectionDataQuery = SystemAPI.QueryBuilder().WithAll<NetSectionData>().Build();
			var netSectionDataEntities = netSectionDataQuery.ToEntityArray(Allocator.Temp);

			for (var i = 0; i < netSectionDataEntities.Length; i++)
			{
				if (prefabSystem.TryGetPrefab<NetSectionPrefab>(netSectionDataEntities[i], out var prefab))
				{
					roadGenerationData.NetSectionPrefabs[prefab.name] = prefab;
				}
			}

			var serviceObjectDataQuery = SystemAPI.QueryBuilder().WithAll<ServiceData>().Build();
			var serviceObjectDataEntities = serviceObjectDataQuery.ToEntityArray(Allocator.Temp);

			for (var i = 0; i < serviceObjectDataEntities.Length; i++)
			{
				if (prefabSystem.TryGetPrefab<ServicePrefab>(serviceObjectDataEntities[i], out var prefab))
				{
					roadGenerationData.ServicePrefabs[prefab.name] = prefab;
				}
			}

			var pillarDataQuery = SystemAPI.QueryBuilder().WithAll<PillarData>().Build();
			var pillarDataEntities = pillarDataQuery.ToEntityArray(Allocator.Temp);

			for (var i = 0; i < pillarDataEntities.Length; i++)
			{
				if (prefabSystem.TryGetPrefab<StaticObjectPrefab>(pillarDataEntities[i], out var prefab))
				{
					roadGenerationData.PillarPrefabs[prefab.name] = prefab;
				}
			}

			var uIGroupElementQuery = SystemAPI.QueryBuilder().WithAll<UIGroupElement>().Build();
			var uIGroupElementEntities = uIGroupElementQuery.ToEntityArray(Allocator.Temp);

			for (var i = 0; i < uIGroupElementEntities.Length; i++)
			{
				if (prefabSystem.TryGetPrefab<UIGroupPrefab>(uIGroupElementEntities[i], out var prefab))
				{
					roadGenerationData.UIGroupPrefabs[prefab.name] = prefab;
				}
			}
		}

		private IEnumerable<INetworkConfig> LoadLocalConfigs()
		{
			foreach (var config in LocalSaveUtil.LoadConfigs())
			{
				if (_updatedRoadPrefabsQueue.Any(x => x.Config.ID == config.ID))
				{
					continue;
				}

				yield return config;
			}
		}

		private struct ApplyUpdatedJob : IJobChunk
		{
			internal Entity Prefab;
			internal EntityTypeHandle EntityTypeHandle;
			internal ComponentTypeHandle<PrefabRef> PrefabRefTypeHandle;
			internal EntityCommandBuffer.ParallelWriter CommandBuffer;

			public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
			{
				var entities = chunk.GetNativeArray(EntityTypeHandle);
				var prefabRefs = chunk.GetNativeArray(ref PrefabRefTypeHandle);

				for (var i = 0; i < prefabRefs.Length; i++)
				{
					if (prefabRefs[i].m_Prefab == Prefab)
					{
						CommandBuffer.AddComponent(unfilteredChunkIndex, entities[i], default(Updated));
					}
				}
			}
		}
	}
}
