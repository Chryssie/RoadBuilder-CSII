﻿using Game.Prefabs;

using System;
using System.Collections.Generic;

using Unity.Entities;

namespace RoadBuilder.Domain.Components.Prefabs
{
	[ComponentMenu("RoadBuilder/", new Type[] { typeof(NetSectionPrefab) })]
	public class RoadBuilderEdgeLaneInfo : ComponentBase
	{
		public NetSectionPrefab? SidePrefab;
		public bool AddSidewalkStateOnNode;
		public bool DoNotRequireBeingOnEdge;

		public override void GetArchetypeComponents(HashSet<ComponentType> components)
		{ }

		public override void GetPrefabComponents(HashSet<ComponentType> components)
		{ }
	}
}
