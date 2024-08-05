﻿using Game.Prefabs;

using RoadBuilder.Domain.Components.Prefabs;
using RoadBuilder.Domain.Enums;

using System.Collections.Generic;

namespace RoadBuilder.LaneGroups
{
	public class TramGroupPrefab : BaseLaneGroupPrefab
	{
		public override void Initialize(Dictionary<string, NetSectionPrefab> sections)
		{
			DisplayName = "Tram Track";
			Options = new RoadBuilderLaneOption[0];

			AddComponent<RoadBuilderLaneInfo>()
				.WithExcluded(RoadCategory.Gravel | RoadCategory.Pathway | RoadCategory.Fence | RoadCategory.Subway)
				.WithFrontThumbnail("coui://roadbuildericons/RB_TramFront.svg")
				.WithBackThumbnail("coui://roadbuildericons/RB_TramRear.svg")
				.AddLaneThumbnail("coui://roadbuildericons/Thumb_TramLane.svg");

			AddComponent<UIObject>().m_Icon = "coui://roadbuildericons/RB_TramFront.svg";

			var laneInfo = sections["Tram Track Section 3"].AddComponent<RoadBuilderLaneGroup>();
			laneInfo.GroupPrefab = this;
			laneInfo.Combination = new LaneOptionCombination[0];

			LinkedSections.Add(sections["Tram Track Section 3"]);
		}
	}
}
