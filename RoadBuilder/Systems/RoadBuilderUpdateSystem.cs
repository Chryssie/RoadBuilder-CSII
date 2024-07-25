﻿using Colossal.Entities;
using Colossal.Serialization.Entities;

using Game;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Game.Tools;

using RoadBuilder.Domain.Components;

using System.Collections.Generic;

using Unity.Collections;
using Unity.Entities;

namespace RoadBuilder.Systems
{
	public partial class RoadBuilderUpdateSystem : GameSystemBase
	{
		private EntityQuery query;
		private EntityQuery queryAll;
		private EntityQuery prefabRefQuery;

		protected override void OnCreate()
		{
			base.OnCreate();

			query = SystemAPI.QueryBuilder().WithAll<RoadBuilderPrefabData, Updated>().Build();
			queryAll = SystemAPI.QueryBuilder().WithAll<RoadBuilderPrefabData>().Build();
			prefabRefQuery = SystemAPI.QueryBuilder()
				.WithAll<RoadBuilderNetwork, PrefabRef, Edge>()
				.WithNone<RoadBuilderUpdateFlagComponent, Temp>()
				.Build();

			RequireForUpdate(query);
		}

		protected override void OnGamePreload(Purpose purpose, GameMode mode)
		{
			base.OnGamePreload(purpose, mode);

			Enabled = false;
		}

		protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
		{
			base.OnGameLoadingComplete(purpose, mode);

			Enabled = true;

			Update(in queryAll);
		}

		protected override void OnUpdate()
		{
			Update(in query);
		}

		protected void Update(in EntityQuery prefabQuery)
		{
			var prefabs = prefabQuery.ToEntityArray(Allocator.Temp);
			var edgeEntities = prefabRefQuery.ToEntityArray(Allocator.Temp);

			var edgeList = new HashSet<Entity>(edgeEntities);

			for (var j = 0; j < prefabs.Length; j++)
			{
				for (var i = 0; i < edgeEntities.Length; i++)
				{
					if (EntityManager.TryGetComponent<PrefabRef>(edgeEntities[i], out var prefabRef) && prefabRef.m_Prefab == prefabs[j])
					{
						foreach (var edge in GetEdges(edgeEntities[i]))
						{
							edgeList.Add(edge);
						}
					}
				}

				EntityManager.RemoveComponent<RoadBuilderUpdateFlagComponent>(prefabs[j]);
			}

			foreach (var entity in edgeList)
			{
				UpdateEdge(entity);
			}
		}

		private IEnumerable<Entity> GetEdges(Entity entity)
		{
			var edge = EntityManager.GetComponentData<Edge>(entity);

			if (EntityManager.TryGetBuffer<ConnectedEdge>(edge.m_Start, true, out var connectedEdges1))
			{
				for (var i = 0; i < connectedEdges1.Length; i++)
				{
					yield return connectedEdges1[i].m_Edge;
				}
			}

			if (EntityManager.TryGetBuffer<ConnectedEdge>(edge.m_End, true, out var connectedEdges2))
			{
				for (var i = 0; i < connectedEdges2.Length; i++)
				{
					yield return connectedEdges2[i].m_Edge;
				}
			}
		}

		private void UpdateEdge(Entity entity)
		{
			UpdateEntity(entity);

			if (EntityManager.TryGetComponent<Composition>(entity, out var comp))
			{
				UpdateEntity(comp.m_StartNode);
				UpdateEntity(comp.m_EndNode);
			}

			if (EntityManager.TryGetComponent<Edge>(entity, out var edge))
			{
				UpdateEntity(edge.m_Start);
				UpdateEntity(edge.m_End);
			}

			if (EntityManager.TryGetBuffer<Game.Net.SubLane>(entity, true, out var subLanes))
			{
				for (var j = 0; j < subLanes.Length; j++)
				{
					UpdateEntity(subLanes[j].m_SubLane);
				}
			}

			if (EntityManager.TryGetBuffer<Game.Objects.SubObject>(entity, true, out var subObjects))
			{
				for (var j = 0; j < subObjects.Length; j++)
				{
					UpdateEntity(subObjects[j].m_SubObject);
				}
			}
		}

		private void UpdateEntity(Entity entity)
		{
			if (entity != Entity.Null)
			{
				EntityManager.AddComponent<Updated>(entity);
			}
		}
	}
}
