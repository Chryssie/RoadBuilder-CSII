﻿using Colossal.UI;

using Game.Prefabs;

using RoadBuilder.Domain;
using RoadBuilder.Domain.Components.Prefabs;
using RoadBuilder.Domain.Configurations;
using RoadBuilder.Domain.Enums;
using RoadBuilder.Domain.Prefabs;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace RoadBuilder.Utilities
{
	public class ThumbnailGenerationUtil
	{
		private readonly INetworkConfig _config;
		private readonly RoadGenerationData _roadGenerationData;
		private readonly List<LaneConfig> _lanes;
		private static SvgItem? _arrowForward;
		private static SvgItem? _arrowBackward;
		private static SvgItem? _arrowBoth;
		private static SvgItem? _markingYellow;
		private static SvgItem? _markingWhite;
		private static SvgItem? _markingDashed;
		private readonly List<(NetSectionPrefab section, LaneGroupPrefab? groupPrefab)?> _sections;

		public INetworkBuilderPrefab NetworkPrefab { get; }

		public ThumbnailGenerationUtil(INetworkBuilderPrefab prefab, RoadGenerationData roadGenerationData)
		{
			if (prefab.Config is null)
			{
				throw new NullReferenceException($"NULL CONFIG WHILE GENERATING '{prefab.Prefab.name}'");
			}

			NetworkPrefab = prefab;
			_config = prefab.Config;
			_roadGenerationData = roadGenerationData;
			_lanes = prefab.Config.Lanes.ToList();

			if (roadGenerationData.LeftHandTraffic)
			{
				_lanes.Reverse();
			}

			_arrowForward ??= CreateSvg(GetFileName("coui://roadbuildericons/Thumb_ArrowForward.svg"));
			_arrowBackward ??= CreateSvg(GetFileName("coui://roadbuildericons/Thumb_ArrowBackward.svg"));
			_arrowBoth ??= CreateSvg(GetFileName("coui://roadbuildericons/Thumb_ArrowBoth.svg"));
			_markingYellow ??= CreateSvg(GetFileName("coui://roadbuildericons/Thumb_LineYellowSolid.svg"));
			_markingWhite ??= CreateSvg(GetFileName("coui://roadbuildericons/Thumb_LineWhiteSolid.svg"));
			_markingDashed ??= CreateSvg(GetFileName("coui://roadbuildericons/Thumb_LineWhiteDotted.svg"));

			_sections = _lanes.Select<LaneConfig, (NetSectionPrefab section, LaneGroupPrefab? groupPrefab)?>(lane => NetworkPrefabGenerationUtil.GetNetSection(_roadGenerationData, _config, lane, out var section, out var groupPrefab) ? (section!, groupPrefab) : null).ToList();
		}

		private SvgItem? CreateSvg(string? file)
		{
			if (file is null)
			{
				return null;
			}

			return new SvgItem(file);
		}

		public string? GenerateThumbnail()
		{
			var combinedXml = GenerateSvg();

			if (combinedXml is null)
			{
				return null;
			}

			combinedXml.Save(Path.Combine(FoldersUtil.TempFolder, _config.ID + ".svg"));

			return $"coui://roadbuilderthumbnails/{_config.ID}.svg";
		}

		public XDocument? GenerateSvg()
		{
			try
			{
				var svgs = GetSvgItems();

				if (svgs.Count == 0)
				{
					return null;
				}

				var first = svgs.First().Value.First();
				var last = svgs.Last().Value.Last();
				var totalWidth = first.ExtentsRect.Width /*- first.PositionRect.Width*/ + svgs.Sum(x => x.Value.Sum(y => y.PositionRect.Width));
				var totalHeight = last.ExtentsRect.Height - last.PositionRect.Height + svgs.Sum(x => x.Value.Sum(y => y.PositionRect.Height));
				var totalSize = Math.Max(totalWidth, totalHeight) /*+ 10*/;

				var elements = new List<XElement>();

				var currentX = (totalSize - totalWidth) / 4;
				var currentY = ((totalSize - totalHeight) / 2) + totalHeight - 50;
				var startingX = currentX;
				var startingY = currentY;

				foreach (var lane in svgs)
				{
					var bounds = (currentX, currentY);

					foreach (var svg in lane.Value)
					{
						elements.Insert(0, svg.SetBounds(currentX, currentY));

						currentX += svg.PositionRect.Width;
						currentY -= svg.PositionRect.Height;
					}

					if (GetArrowIcon(lane.Key, out var arrow))
					{
						elements.Insert(lane.Value.Count, arrow!.SetBounds(bounds.currentX + ((currentX - bounds.currentX - arrow.PositionRect.Width) / 2), bounds.currentY - ((bounds.currentY - currentY - arrow.PositionRect.Height) / 2)));
					}

					if (GetMarkingIcon(lane.Key, out var marking))
					{
						elements.Add(marking!.SetBounds(bounds.currentX - (marking.PositionRect.Width / 2), bounds.currentY + (marking.PositionRect.Height / 2)));
					}
				}

				string? pipesFile = null;
				if (_config.Addons.HasFlag(RoadAddons.HasUndergroundElectricityCable) && !_config.Addons.HasFlag(RoadAddons.RequiresUpgradeForElectricity))
				{
					if (_config.Addons.HasFlag(RoadAddons.HasUndergroundWaterPipes))
					{
						pipesFile = totalSize > 115 ? "Thumb_PipesPower" : "Thumb_PipesPowerSmall";
					}
					else
					{
						pipesFile = totalSize > 115 ? "Thumb_Power" : "Thumb_PowerSmall";
					}
				}
				else if (_config.Addons.HasFlag(RoadAddons.HasUndergroundWaterPipes))
				{
					pipesFile = totalSize > 115 ? "Thumb_Pipes" : "Thumb_PipesSmall";
				}

				if (pipesFile is not null)
				{
					var pipe = CreateSvg(GetFileName($"coui://roadbuildericons/{pipesFile}.svg"));

					if (pipe != null)
					{
						elements.Insert(0, pipe.SetBounds((totalSize - 100) / 2, (totalSize - 100) / 2, false));
					}
				}

				XNamespace aw = "http://www.w3.org/2000/svg";
				var combinedSvg = new XElement(aw + "svg",
					new XAttribute("width", "100%"),
					new XAttribute("height", "100%"),
					new XAttribute("viewBox", $"0 0 {totalSize.ToString(CultureInfo.InvariantCulture)} {totalSize.ToString(CultureInfo.InvariantCulture)}"),
					new XAttribute("version", "1.1"),
					new XAttribute(XNamespace.Xmlns + "xlink", "http://www.w3.org/1999/xlink"),
					new XAttribute(XNamespace.Xmlns + "serif", "http://www.serif.com/"),
					new XAttribute(XNamespace.Xmlns + "space", "preserve"),
					new XAttribute("xmlns", "http://www.w3.org/2000/svg"),
					new XAttribute("style", "fill-rule:evenodd;clip-rule:evenodd;stroke-linejoin:round;stroke-miterlimit:2;"),
					elements
				);

				var combinedXml = new XDocument(
					new XDeclaration("1.0", "UTF-8", "no"),
					new XDocumentType("svg", "-//W3C//DTD SVG 1.1//EN", "http://www.w3.org/Graphics/SVG/1.1/DTD/svg11.dtd", null),
					combinedSvg
				);

				return combinedXml;
			}
			catch (Exception ex)
			{
				Mod.Log.Warn(ex, "Error during thumbnail generation");

				return null;
			}
		}

		private Dictionary<int, List<SvgItem>> GetSvgItems()
		{
			var svgs = new Dictionary<int, List<SvgItem>>();

			for (var i = 0; i < _lanes.Count; i++)
			{
				var laneConfig = _lanes[i];
				var laneSvgs = new List<SvgItem>();

				foreach (var lane in GetLaneIcons(laneConfig, _sections[i]))
				{
					var file = GetFileName(lane);

					if (file is null)
					{
						continue;
					}

					try
					{
						laneSvgs.Add(new SvgItem(file));
					}
					catch (Exception ex)
					{
						Mod.Log.Warn(ex, "Failed to get SVG info from file: " + file);
					}
				}

				if (laneSvgs.Count > 0)
				{
					svgs[i] = laneSvgs;
				}
			}

			return svgs;
		}

		private bool GetArrowIcon(int index, out SvgItem? arrow)
		{
			if (_sections[index] is null || Mod.Settings!.HideArrowsOnThumbnails)
			{
				arrow = null;
				return false;
			}

			var sectionTuple = _sections[index];

			if (sectionTuple.HasValue)
			{
				var section = sectionTuple.Value;

				if (!section.section.FindLanes<CarLane>().Any() && !section.section.FindLanes<TrackLane>().Any())
				{
					arrow = null;
					return false;
				}

				if (section.section.SupportsTwoWay())
				{
					arrow = _arrowBoth;
					return true;
				}
			}

			arrow = _lanes[index].Invert ? _arrowBackward : _arrowForward;
			return true;
		}

		private bool GetMarkingIcon(int index, out SvgItem? arrow)
		{
			if (_sections[index] is null || (_config.Category & (RoadCategory.Gravel | RoadCategory.Pathway | RoadCategory.Fence | RoadCategory.Tiled)) != 0)
			{
				arrow = null;
				return false;
			}

			var section = _sections[index];
			var previous = index > 0 ? _sections[index - 1] : null;

			var isCurrentCar = section?.section.FindLanes<CarLane>().Any() ?? false;
			var isPreviousCar = previous.HasValue && previous.Value.section.FindLanes<CarLane>().Any();

			if (isCurrentCar && isPreviousCar)
			{
				if (_lanes[index].Invert == _lanes[index - 1].Invert)
				{
					arrow = _markingDashed;
				}
				else
				{
					arrow = _roadGenerationData.YellowDivider ? _markingYellow : _markingWhite;
				}

				return true;
			}

			arrow = null;
			return false;
		}

		private IEnumerable<string> GetLaneIcons(LaneConfig lane, (NetSectionPrefab section, LaneGroupPrefab? groupPrefab)? sections)
		{
			if (sections is null)
			{
				yield break;
			}

			if (sections.Value.section.TryGet<RoadBuilderLaneDecorationInfo>(out var decorationInfo) && sections.Value.groupPrefab?.Options.FirstOrDefault(x => x.Type is LaneOptionType.Decoration) is RoadBuilderLaneOption decorationOption)
			{
				switch (LaneOptionsUtil.GetSelectedOptionValue(_config, lane, decorationOption))
				{
					case "G":
						if (decorationInfo.LaneGrassThumbnail?.Any() ?? false)
						{
							foreach (var item in lane.Invert ? decorationInfo.LaneGrassThumbnail.Reverse() : decorationInfo.LaneGrassThumbnail)
							{
								yield return item;
							}

							yield break;
						}

						break;
					case "T":
						if (decorationInfo.LaneTreeThumbnail?.Any() ?? false)
						{
							foreach (var item in lane.Invert ? decorationInfo.LaneTreeThumbnail.Reverse() : decorationInfo.LaneTreeThumbnail)
							{
								yield return item;
							}

							yield break;
						}

						break;
					case "GT":
						if (decorationInfo.LaneGrassAndTreeThumbnail?.Any() ?? false)
						{
							foreach (var item in lane.Invert ? decorationInfo.LaneGrassAndTreeThumbnail.Reverse() : decorationInfo.LaneGrassAndTreeThumbnail)
							{
								yield return item;
							}

							yield break;
						}

						break;
				}
			}

			if (sections.Value.section.TryGet<RoadBuilderLaneInfo>(out var sectionInfo) && (sectionInfo.LaneThumbnails?.Any() ?? false))
			{
				foreach (var item in lane.Invert ? sectionInfo.LaneThumbnails.Reverse() : sectionInfo.LaneThumbnails)
				{
					yield return item;
				}

				yield break;
			}

			if (sections.Value.groupPrefab != null && sections.Value.groupPrefab.TryGet<RoadBuilderLaneInfo>(out var groupInfo) && (groupInfo.LaneThumbnails?.Any() ?? false))
			{
				foreach (var item in lane.Invert ? groupInfo.LaneThumbnails.Reverse() : groupInfo.LaneThumbnails)
				{
					yield return item;
				}

				yield break;
			}

			yield return "coui://roadbuildericons/Thumb_CarLane.svg";
		}

		private static string? GetFileName(string icon)
		{
			if (string.IsNullOrEmpty(icon) || icon.StartsWith("thumbnail://"))
			{
				return null;
			}

			var regex = Regex.Match(icon, "coui://(.+?)/(.+)");

			if (!regex.Success)
			{
				return Path.Combine(FoldersUtil.GameUIPath, icon);
			}

			var hostMap = (IDictionary<string, List<(string path, int)>>)typeof(DefaultResourceHandler).GetField("m_HostLocationsMap", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(UIManager.defaultUISystem.resourceHandler);

			if (hostMap?.TryGetValue(regex.Groups[1].Value, out var paths) ?? false)
			{
				foreach (var item in paths.OrderBy(x => x.Item2))
				{
					if (File.Exists(Path.Combine(item.path, regex.Groups[2].Value)))
					{
						return Path.Combine(item.path, regex.Groups[2].Value);
					}
				}
			}

			return null;
		}

		public static void DeleteThumbnail(string id)
		{
			var path = Path.Combine(FoldersUtil.TempFolder, id + ".svg");

			if (File.Exists(path))
			{
				File.Delete(path);
			}
		}

		private class SvgItem
		{
			public SvgItem(string file)
			{
				var xml = XDocument.Load(file);

				Svg = xml.Root;
				PositionRect = GetRectValues(xml, "Position");
				ExtentsRect = GetRectValues(xml, "Extents");
			}

			public XElement Svg { get; }
			public Rectangle PositionRect { get; }
			public Rectangle ExtentsRect { get; }

			public XElement SetBounds(double offsetX, double offsetY, bool center = true)
			{
				if (center)
				{
					offsetX -= PositionRect.X;
					offsetY -= PositionRect.Y + PositionRect.Height;
				}

				return new XElement("g"
					, new XAttribute("transform", $"matrix(1,0,0,1,{offsetX.ToString(CultureInfo.InvariantCulture)},{offsetY.ToString(CultureInfo.InvariantCulture)})")
					, Svg.Elements());
			}

			private static XElement RemoveAllNamespaces(XElement xmlDocument)
			{
				if (!xmlDocument.HasElements)
				{
					var xElement = new XElement(xmlDocument.Name.LocalName)
					{
						Value = xmlDocument.Value
					};

					foreach (var attribute in xmlDocument.Attributes())
					{
						xElement.Add(attribute);
					}

					return xElement;
				}

				return new XElement(xmlDocument.Name.LocalName, xmlDocument.Elements().Select(el => RemoveAllNamespaces(el)));
			}

			private Rectangle GetRectValues(XDocument xmlDocument, string rectId)
			{
				var rectElement = xmlDocument
					.Descendants()
					.FirstOrDefault(e => e.Name.LocalName == "rect" && ((string)e.Attribute("id") == rectId || (string)e.Parent.Attribute("id") == rectId));

				if (rectElement == null)
				{
					throw new Exception($"Rect element with id '{rectId}' not found.");
				}

				double.TryParse(rectElement.Attribute("x").Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var x);
				double.TryParse(rectElement.Attribute("y").Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var y);
				double.TryParse(rectElement.Attribute("width").Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var width);
				double.TryParse(rectElement.Attribute("height").Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var height);

				return new Rectangle(x, y, width, height);
			}
		}

		private readonly struct Rectangle
		{
			public Rectangle(double x, double y, double width, double height)
			{
				X = x - 50;
				Y = y - 50;
				Width = width;
				Height = height;
			}

			public double X { get; }
			public double Y { get; }
			public double Width { get; }
			public double Height { get; }
			public bool Empty => X == 0 && Y == 0 && Width == 0 && Height == 0;

			public override readonly string ToString()
			{
				return $"({X}, {Y}, {Width}, {Height})";
			}
		}
	}
}
