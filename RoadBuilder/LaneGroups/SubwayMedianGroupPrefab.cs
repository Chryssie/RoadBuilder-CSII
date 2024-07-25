﻿using Game.Prefabs;

using RoadBuilder.Domain.Components.Prefabs;
using RoadBuilder.Domain.Enums;

using System.Collections.Generic;

namespace RoadBuilder.LaneGroups
{
	public class SubwayMedianGroupPrefab : BaseLaneGroupPrefab
	{
		private const string OptionName = "Style";

		public override void Initialize(Dictionary<string, NetSectionPrefab> sections)
		{
			DisplayName = "Platform";
			Options = new RoadBuilderLaneOption[]
			{
				new()
				{
					Name = OptionName,
					Type = LaneOptionType.SingleSelectionButtons,
					DefaultValue = "Raised",
					Options = new RoadBuilderLaneOptionValue[]
					{
						new()
						{
							Value = "Raised",
							ThumbnailUrl = "coui://roadbuildericons/RB_RaisedSidewalks.svg"
						},
						new()
						{
							Value = "Flat",
							ThumbnailUrl = "coui://roadbuildericons/RB_RaisedSidewalks.svg"
						}
					}
				}
			};

			AddComponent<RoadBuilderLaneInfo>()
				.WithAny(RoadCategory.Train | RoadCategory.Subway | RoadCategory.Tram);

			AddComponent<UIObject>().m_Icon = "coui://roadbuildericons/RB_TrainFront.svg";

			SetUp(sections["Subway Median 8"], "Raised");
			SetUp(sections["Subway Median 8 - Plain"], "Flat");
		}

		private void SetUp(NetSectionPrefab prefab, string value)
		{
			var laneInfo = prefab.AddComponent<RoadBuilderLaneGroup>();
			laneInfo.GroupPrefab = this;
			laneInfo.Combination = new LaneOptionCombination[]
			{
				new()
				{
					OptionName = OptionName,
					Value = value
				}
			};

			LinkedSections.Add(prefab);
		}
	}
}
