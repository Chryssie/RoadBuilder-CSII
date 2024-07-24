﻿using Colossal.Entities;

using Game;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Game.Tools;

using RoadBuilder.Domain.Components;
using RoadBuilder.Utilities;

using System.Collections.Generic;
using System.Linq;

using Unity.Collections;
using Unity.Entities;

namespace RoadBuilder.Systems
{
	public partial class RoadBuilderUpdateSystem : GameSystemBase
	{
		private EntityQuery query;
		private EntityQuery prefabRefQuery;

		protected override void OnCreate()
		{
			base.OnCreate();

			query = SystemAPI.QueryBuilder().WithAll<RoadBuilderPrefabData, Updated>().Build();
			prefabRefQuery = SystemAPI.QueryBuilder()
				.WithAll<RoadBuilderNetwork, PrefabRef, Edge>()
				.WithNone<RoadBuilderUpdateFlagComponent, Temp>()
				.Build();

			RequireForUpdate(query);
		}

		protected override void OnUpdate()
		{
			var prefabs = query.ToEntityArray(Allocator.Temp);
			var edgeEntities = prefabRefQuery.ToEntityArray(Allocator.Temp);

			var edgeList = new HashSet<Entity>();

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
					yield return connectedEdges1[i].m_Edge;
				}
			}
		}

		private void UpdateEdge(Entity entity)
		{
			EntityManager.AddComponent<Updated>(entity);

			if (EntityManager.TryGetComponent<Composition>(entity, out var comp))
			{
				EntityManager.AddComponent<Updated>(comp.m_StartNode);
				EntityManager.AddComponent<Updated>(comp.m_EndNode);
			}

			if (EntityManager.TryGetComponent<Edge>(entity, out var edge))
			{
				EntityManager.AddComponent<Updated>(edge.m_Start);
				EntityManager.AddComponent<Updated>(edge.m_End);
			}

			if (EntityManager.TryGetBuffer<Game.Net.SubLane>(entity, true, out var subLanes))
			{
				for (var j = 0; j < subLanes.Length; j++)
				{
					EntityManager.AddComponent<Updated>(subLanes[j].m_SubLane);
				}
			}

			if (EntityManager.TryGetBuffer<Game.Objects.SubObject>(entity, true, out var subObjects))
			{
				for (var j = 0; j < subObjects.Length; j++)
				{
					EntityManager.AddComponent<Updated>(subObjects[j].m_SubObject);
				}
			}
		}
	}
}
