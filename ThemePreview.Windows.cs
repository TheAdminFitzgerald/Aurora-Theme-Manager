using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AuroraThemeManager
{
    public sealed partial class ThemePreview
    {
        private static readonly Dictionary<string, string[]> ColorInfo = new Dictionary<string, string[]>
        {
            ["Background"] = new[] { "Window background", "The background of windows, lists and maps." },
            ["StandardTextColour"] = new[] { "Standard text", "Most text in windows and lists." },
            ["TextEditColour"] = new[] { "Text edit", "Values and text boxes." },
            ["TextHighlightColour"] = new[] { "Text highlight", "Highlighted and selected items." },
            ["ButtonColour"] = new[] { "Button", "Button faces." },
            ["ToolbarButtonColour"] = new[] { "Toolbar button", "Toolbar button faces." },
            ["IconColour"] = new[] { "Icon", "Toolbar icons." },
            ["PositiveColour"] = new[] { "Positive", "Good or safe states." },
            ["WarningColour"] = new[] { "Warning", "Minor problems, such as low supplies." },
            ["CriticalColour"] = new[] { "Critical", "Serious problems, such as damage or empty supplies." },
            ["ObsoleteColour"] = new[] { "Obsolete", "Obsolete designs and components." },
            ["ArmourColour"] = new[] { "Armour", "Intact armour." },
            ["ArmourDamagedColour"] = new[] { "Armour damaged", "Destroyed armour." },
            ["PrisonerColour"] = new[] { "Prisoner", "Captured commanders." },
            ["AlienComponentColour"] = new[] { "Alien component", "Components that use alien technology." },
            ["PrototypeComponentColour"] = new[] { "Prototype component", "Prototype components." },
            ["MatchingComponentColour"] = new[] { "Matching component", "Alien components that match your own." },
            ["ComponentTechDataColour"] = new[] { "Component tech data", "Components with technology you can learn." },
            ["ComponentResearchColour"] = new[] { "Component research", "Alien components you can research." },
            ["OrdnanceSeriesColour"] = new[] { "Ordnance series", "Missile series." },
            ["NoResearchColour"] = new[] { "No research", "Technologies you cannot research yet." },
            ["PopulatedColonyColour"] = new[] { "Populated colony", "Colonies with population." },
            ["MiningColonyColour"] = new[] { "Mining colony", "Colonies with installations but no population." },
            ["OtherColonyColour"] = new[] { "Other colony", "Other colonies." },
            ["CMCPurchaseColonyColour"] = new[] { "CMC purchase", "Colonies whose civilian minerals you buy." },
            ["NavalAdminCommandColour"] = new[] { "Naval admin command", "Naval admin commands." },
            ["NavalSubFleetColour"] = new[] { "Sub-fleet", "Sub-fleets." },
            ["NavalSquadronColour"] = new[] { "Squadron", "Squadrons." },
            ["CivilianShippingLineColour"] = new[] { "Civilian shipping line", "Civilian shipping lines." },
            ["GroundForcesOrganisationColour"] = new[] { "Ground forces organisation", "Ground force organizations and headings." },
            ["SupportingFormationColour"] = new[] { "Supporting formation", "Formations supporting another formation." },
            ["SupportedFormationColour"] = new[] { "Supported formation", "Formations being supported." },
            ["HostileColour"] = new[] { "Hostile", "Hostile contacts." },
            ["NeutralColour"] = new[] { "Neutral", "Neutral contacts." },
            ["FriendlyColour"] = new[] { "Friendly", "Friendly contacts." },
            ["AlliedColour"] = new[] { "Allied", "Allied contacts." },
            ["JumpPointColour"] = new[] { "Jump point", "Jump points and jump gates." },
            ["TacMapWaypointColour"] = new[] { "Waypoint", "Waypoints." },
            ["ColonyCostLessThanTwoColour"] = new[] { "Colony cost under 2", "Bodies with a colony cost under 2." },
            ["ColonyCostLessThanThreeColour"] = new[] { "Colony cost under 3", "Bodies with a colony cost under 3." },
            ["ColonyCostLessThanFiveColour"] = new[] { "Colony cost under 5", "Bodies with a colony cost under 5." },
            ["GasGiantColour"] = new[] { "Gas giant", "Gas giants." },
            ["CometColour"] = new[] { "Comet", "Comets." },
            ["TacMapSystemBodyColour"] = new[] { "System body", "System bodies." },
            ["TacMapBodyNameColour"] = new[] { "Body name", "Names on the tactical map." },
            ["TacMapFleetColour"] = new[] { "Fleet", "Your fleets." },
            ["TacMapCivilianColour"] = new[] { "Civilian fleet", "Civilian fleets." },
            ["TacMapSalvoColour"] = new[] { "Missile salvo", "Your missile salvos." },
            ["WreckColour"] = new[] { "Wreck", "Wrecks." },
            ["TacMapLifepodColour"] = new[] { "Life pod", "Life pods." },
            ["TacMapMDPacketColour"] = new[] { "Mineral packet", "Mass driver packets." },
            ["TacMapDeepSpacePopColour"] = new[] { "Deep space population", "Populations in deep space." },
            ["TacMapDistanceLineColour"] = new[] { "Distance line", "The distance measuring line." },
            ["TacticalScaleColour"] = new[] { "Scale bar", "The map scale bar." },
            ["TacMapOrbitColour"] = new[] { "Orbit", "Orbits." },
            ["JumpPointAlienColour"] = new[] { "Alien jump gate", "Alien jump gates." },
            ["TacMapLagrangePointColour"] = new[] { "Lagrange point", "Lagrange points." },
            ["TacMapSurveyLocationColour"] = new[] { "Survey location", "Survey locations." },
            ["TacMapAetherGateColour"] = new[] { "Aether gate", "Aether gates." },
            ["TacMapRiftOutlineColour"] = new[] { "Rift outline", "Aether rift outlines." },
            ["TacMapRiftCentreColour"] = new[] { "Rift centre", "Aether rift centres." },
            ["TacMapCometOutlineColour"] = new[] { "Comet outline", "Comet outlines." },
            ["TacMapActiveSensorColour"] = new[] { "Active sensor range", "Active sensor range." },
            ["TacMapThermalSensorColour"] = new[] { "Thermal sensor range", "Thermal sensor range." },
            ["TacMapEMSensorColour"] = new[] { "EM sensor range", "EM sensor range." },
            ["TacMapELINTSensorColour"] = new[] { "ELINT sensor range", "ELINT sensor range." },
            ["TacMapFireControlColour"] = new[] { "Fire control range", "Fire control range." },
            ["TacMapWeaponRangeColour"] = new[] { "Weapons range", "Weapons range." },
            ["TacMapAlienSensorColour"] = new[] { "Alien sensor range", "Known alien sensor range." },
            ["TacMapSurveyedColour"] = new[] { "Surveyed", "Surveyed bodies." },
            ["TacMapMineralsColour"] = new[] { "Minerals", "Bodies with known minerals." },
            ["TacMapColonyColour"] = new[] { "Colony", "Bodies with your colonies." },
            ["GalMapSystemIconColour"] = new[] { "System icon", "System icons." },
            ["GalMapSystemOutlineColour"] = new[] { "System outline", "System icon outlines." },
            ["GalMapSystemSelectedColour"] = new[] { "Selected system", "The selected system." },
            ["GalMapMultiSystemSelectColour"] = new[] { "Multi-system select", "The multi-system selection box." },
            ["GalMapBlackHoleColour"] = new[] { "Black hole", "Black hole systems." },
            ["GalMapSupernovaColour"] = new[] { "Supernova", "Supernova systems." },
            ["GalMapJumpPointConnectionsColour"] = new[] { "Jump link", "Jump links between systems." },
            ["GalMapStabilisedConnectionsColour"] = new[] { "Stabilised link", "Jump links gated at both ends." },
            ["GalMapRestrictedColour"] = new[] { "Restricted", "Restricted links and systems." },
            ["GalMapSurveyIncompleteColour"] = new[] { "Survey incomplete", "Systems not fully surveyed." },
            ["GalMapDormantJPColour"] = new[] { "Dormant jump point", "Possible dormant jump points." },
            ["GalMapNavalHQColour"] = new[] { "Naval HQ", "Naval headquarters." },
            ["GalMapMaintenanceLocationColour"] = new[] { "Maintenance location", "Maintenance locations." },
            ["GalMapGroundSurveyColour"] = new[] { "Ground survey", "Ground survey locations." },
            ["GalMapMineralSearchColour"] = new[] { "Mineral search", "Mineral search locations." },
            ["GalMapCometsColour"] = new[] { "Comets", "Comet counts." },
            ["Map3DSystemIconColour"] = new[] { "3D map system icon", "System icons on the 3D galactic map." },
            ["OrgChartInfantryColour"] = new[] { "Infantry", "Infantry units on Order of Battle charts." },
            ["OrgChartMechanisedColour"] = new[] { "Mechanised", "Mechanised units on Order of Battle charts." },
            ["OrgChartArmourColour"] = new[] { "Armour", "Armour units on Order of Battle charts." },
            ["OrgChartReconnaissanceColour"] = new[] { "Reconnaissance", "Reconnaissance units on Order of Battle charts." },
            ["OrgChartArtilleryColour"] = new[] { "Artillery", "Artillery units on Order of Battle charts." },
            ["OrgChartAirDefenceColour"] = new[] { "Air defence", "Air defence units on Order of Battle charts." },
            ["OrgChartOrbitalColour"] = new[] { "Orbital", "Orbital units on Order of Battle charts." },
            ["OrgChartEngineerColour"] = new[] { "Engineer", "Engineer units on Order of Battle charts." },
            ["OrgChartSpecialistColour"] = new[] { "Specialist", "Specialist units on Order of Battle charts." },
            ["OrgChartSupportColour"] = new[] { "Support", "Support units on Order of Battle charts." },
            ["OrgChartSupplyColour"] = new[] { "Supply", "Supply units on Order of Battle charts." },
            ["OrgChartGenericColour"] = new[] { "Generic", "Other units on Order of Battle charts." },
            ["OrgChartIconColour"] = new[] { "Order of Battle icon", "Symbols on Order of Battle charts." },
            ["NavalFleetBoxColour"] = new[] { "Fleet box", "Fleets on the naval Order of Battle chart." },
            ["NavalSubFleetBoxColour"] = new[] { "Sub-fleet box", "Sub-fleets on the naval Order of Battle chart." },
        };

        private static readonly string[][] TextColumns =
        {
            new[] { "#General", "StandardTextColour", "TextEditColour", "TextHighlightColour", "PositiveColour", "WarningColour", "CriticalColour", "ObsoleteColour", "ArmourColour", "ArmourDamagedColour", "PrisonerColour",
                "", "#Research", "AlienComponentColour", "PrototypeComponentColour", "MatchingComponentColour", "ComponentTechDataColour", "ComponentResearchColour", "OrdnanceSeriesColour", "NoResearchColour" },
            new[] { "#Colonies and Fleets", "PopulatedColonyColour", "MiningColonyColour", "OtherColonyColour", "CMCPurchaseColonyColour", "NavalAdminCommandColour", "NavalSubFleetColour", "NavalSquadronColour", "CivilianShippingLineColour",
                "", "#Ground Forces", "GroundForcesOrganisationColour", "SupportingFormationColour", "SupportedFormationColour",
                "", "#System Bodies", "TacMapSystemBodyColour", "TacMapBodyNameColour", "ColonyCostLessThanTwoColour", "ColonyCostLessThanThreeColour", "ColonyCostLessThanFiveColour", "GasGiantColour", "CometColour" },
            new[] { "#Tactical Contacts", "HostileColour", "NeutralColour", "FriendlyColour", "AlliedColour", "TacMapFleetColour", "TacMapCivilianColour", "TacMapSalvoColour", "WreckColour", "TacMapLifepodColour", "TacMapMDPacketColour", "TacMapDeepSpacePopColour",
                "", "#Tactical Markers", "TacMapWaypointColour", "TacMapDistanceLineColour", "TacticalScaleColour", "TacMapOrbitColour", "JumpPointColour", "JumpPointAlienColour", "TacMapLagrangePointColour", "TacMapSurveyLocationColour", "TacMapAetherGateColour", "TacMapRiftOutlineColour", "TacMapRiftCentreColour", "TacMapCometOutlineColour" },
            new[] { "#Tactical Rings", "TacMapWeaponRangeColour", "TacMapFireControlColour", "TacMapActiveSensorColour", "TacMapThermalSensorColour", "TacMapEMSensorColour", "TacMapELINTSensorColour", "TacMapAlienSensorColour", "TacMapSurveyedColour", "TacMapMineralsColour", "TacMapColonyColour" },
            new[] { "#Galactic Map", "GalMapSystemIconColour", "GalMapSystemOutlineColour", "GalMapSystemSelectedColour", "GalMapMultiSystemSelectColour", "GalMapBlackHoleColour", "GalMapSupernovaColour", "GalMapJumpPointConnectionsColour", "GalMapStabilisedConnectionsColour", "GalMapRestrictedColour",
                "GalMapSurveyIncompleteColour", "GalMapDormantJPColour", "GalMapNavalHQColour", "GalMapMaintenanceLocationColour", "GalMapGroundSurveyColour", "GalMapMineralSearchColour", "GalMapCometsColour", "Map3DSystemIconColour" },
        };

        private static readonly KeyValuePair<OOBSymbol, string>[] GroundBoxes =
        {
            new KeyValuePair<OOBSymbol, string>(OOBSymbol.Infantry, "OrgChartInfantryColour"),
            new KeyValuePair<OOBSymbol, string>(OOBSymbol.Mechanised, "OrgChartMechanisedColour"),
            new KeyValuePair<OOBSymbol, string>(OOBSymbol.Armour, "OrgChartArmourColour"),
            new KeyValuePair<OOBSymbol, string>(OOBSymbol.Reconnaissance, "OrgChartReconnaissanceColour"),
            new KeyValuePair<OOBSymbol, string>(OOBSymbol.Artillery, "OrgChartArtilleryColour"),
            new KeyValuePair<OOBSymbol, string>(OOBSymbol.AirDefence, "OrgChartAirDefenceColour"),
            new KeyValuePair<OOBSymbol, string>(OOBSymbol.Orbital, "OrgChartOrbitalColour"),
            new KeyValuePair<OOBSymbol, string>(OOBSymbol.Engineer, "OrgChartEngineerColour"),
            new KeyValuePair<OOBSymbol, string>(OOBSymbol.Specialist, "OrgChartSpecialistColour"),
            new KeyValuePair<OOBSymbol, string>(OOBSymbol.Support, "OrgChartSupportColour"),
            new KeyValuePair<OOBSymbol, string>(OOBSymbol.Supply, "OrgChartSupplyColour"),
            new KeyValuePair<OOBSymbol, string>(OOBSymbol.Generic, "OrgChartGenericColour"),
        };

        private static readonly string[] NavalBoxes = { "NavalAdminCommandColour", "NavalFleetBoxColour", "NavalSubFleetBoxColour" };

        private const int Gutter = 8;
        private const int RowHeight = 16;
        private static readonly Size BoxSize = new Size(36, 25);

        private readonly OOBSymbols oobSymbols = new OOBSymbols();

        private Color TextColorOf(string name)
        {
            return ColorOf(name, Color.White);
        }

        private string[] Info(string color)
        {
            string[] info;
            return ColorInfo.TryGetValue(color, out info) ? info : new[] { color, "" };
        }

        private RectangleF Say(Graphics g, Font font, string text, string color, float x, float y)
        {
            RectangleF r = DrawLabel(g, text, font, TextColorOf(color), x, y);
            string[] info = Info(color);
            HitRect(color, info[0], info[1], r);
            return r;
        }

        private const int GroupPad = 10;

        private float GroupTop(Font heading, float y)
        {
            return y + heading.GetHeight() + 4;
        }

        private float Group(Graphics g, Font heading, string title, float x, float y, float width, float bottom)
        {
            float mid = y + heading.GetHeight() / 2;
            float textLeft = x + 8;
            RectangleF caption = Say(g, heading, title, "StandardTextColour", textLeft + 2, y);
            using (var pen = new Pen(Color.FromArgb(110, TextColorOf("StandardTextColour"))))
            {
                g.DrawLine(pen, x, mid, textLeft, mid);
                g.DrawLine(pen, caption.Right + 2, mid, x + width, mid);
                g.DrawLine(pen, x + width, mid, x + width, bottom);
                g.DrawLine(pen, x + width, bottom, x, bottom);
                g.DrawLine(pen, x, bottom, x, mid);
            }
            return bottom + 6;
        }

        private float GroupBottom(float contentBottom)
        {
            return contentBottom + GroupPad - 4;
        }

        private void DrawGameWindows(Graphics g, Font small, Font bold, Rectangle area)
        {
            using (Font text = SampleFont(9f))
            using (Font heading = SampleFont(9f, FontStyle.Bold))
            {
                float left = area.X + Gutter;
                float width = area.Width - Gutter * 2 - 1;
                float inner = left + GroupPad;
                float innerWidth = width - GroupPad * 2;

                float y = area.Y + 4;
                float top = GroupTop(heading, y);
                float right = DrawToolbar(g, small, inner, top);
                float bottom = DrawButtonBar(g, text, new RectangleF(right + 16, top + 4, inner + innerWidth - right - 16, 28)) + 4;
                y = Group(g, heading, "Buttons", left, y, width, GroupBottom(bottom));

                top = GroupTop(heading, y);
                bottom = DrawTextColors(g, text, heading, new RectangleF(inner, top + 2, innerWidth, area.Bottom - top));
                y = Group(g, heading, "Text", left, y, width, GroupBottom(bottom));

                top = GroupTop(heading, y);
                bottom = DrawOrderOfBattle(g, text, inner, top, innerWidth);
                Group(g, heading, "Order of Battle", left, y, width, GroupBottom(bottom));
            }
        }

        private float DrawToolbar(Graphics g, Font font, float x, float y)
        {
            AuroraConfig.ButtonStyleEntry ts = config.FindStyle("ToolbarButtonStyle");
            Color iconColor = TextColorOf("IconColour");
            Color face = ColorOf("ToolbarButtonColour", Color.Gray);
            string[] buttonInfo = Info("ToolbarButtonColour"), iconInfo = Info("IconColour");
            string[] paths = ToolbarIconPaths();
            for (int i = 0; i < ToolbarIconNames.Length; i++)
            {
                var r = new Rectangle((int)x + i * 38, (int)y, 36, 36);
                Bitmap icon = paths[i] == null ? null : ToolbarIcon(paths[i], i, iconColor, 24);
                DrawButton(g, font, r, face, TextColorOf("StandardTextColour"), icon == null ? "?" : "", icon, ts);
                HitRect("ToolbarButtonColour", buttonInfo[0], buttonInfo[1], r);
                var inner = new RectangleF(r.X + 8, r.Y + 8, r.Width - 16, r.Height - 16);
                Hit("IconColour", iconInfo[0], iconInfo[1], p => inner.Contains(p));
            }
            return x + ToolbarIconNames.Length * 38 - 2;
        }

        private float DrawButtonBar(Graphics g, Font font, RectangleF r)
        {
            AuroraConfig.ButtonStyleEntry bs = config.FindStyle("ButtonStyle");
            Color face = ColorOf("ButtonColour", Color.Gray);
            Color caption = TextColorOf("StandardTextColour");
            string[] buttonInfo = Info("ButtonColour");
            string[] captions = { "New Ship Class", "Rename Class", "Lock Design", "Refresh Tech", "5 Seconds" };
            float w = Math.Min(130, (r.Width - (captions.Length - 1) * 4) / captions.Length);
            for (int i = 0; i < captions.Length; i++)
            {
                var b = new Rectangle((int)(r.X + i * (w + 4)), (int)r.Y, (int)w, (int)r.Height);
                DrawButton(g, font, b, face, caption, captions[i], null, bs);
                HitRect("ButtonColour", buttonInfo[0], buttonInfo[1], b);
                SizeF size = g.MeasureString(captions[i], font);
                var text = new RectangleF(b.X + (b.Width - size.Width) / 2, b.Y + (b.Height - size.Height) / 2, size.Width, size.Height);
                Hit("StandardTextColour", "Button text", "Button captions.", p => text.Contains(p));
            }
            return r.Bottom;
        }

        private static List<List<string>> SplitGroups(string[] column)
        {
            var groups = new List<List<string>>();
            foreach (string entry in column)
            {
                if (entry.Length == 0)
                    continue;
                if (entry[0] == '#')
                    groups.Add(new List<string> { entry.Substring(1) });
                else
                    groups[groups.Count - 1].Add(entry);
            }
            return groups;
        }

        private float TextGroupHeight(Font heading, int items)
        {
            return GroupBottom(GroupTop(heading, 0) + items * RowHeight);
        }

        private float DrawTextColors(Graphics g, Font font, Font heading, RectangleF area)
        {
            const float gap = 8;
            var columns = new List<List<List<string>>>();
            float height = 0;
            foreach (string[] column in TextColumns)
            {
                List<List<string>> groups = SplitGroups(column);
                columns.Add(groups);
                float h = 0;
                foreach (List<string> group in groups)
                    h += TextGroupHeight(heading, group.Count - 1) + 6;
                height = Math.Max(height, h - 6);
            }
            float bottom = area.Y + height;

            string[] back = Info("Background");
            HitRect("Background", back[0], back[1], new RectangleF(area.X, area.Y, area.Width, height));

            float frameWidth = (area.Width - gap * (columns.Count - 1)) / columns.Count;
            for (int c = 0; c < columns.Count; c++)
            {
                float x = area.X + c * (frameWidth + gap);
                float y = area.Y;
                for (int i = 0; i < columns[c].Count; i++)
                {
                    List<string> group = columns[c][i];
                    float top = GroupTop(heading, y);
                    for (int j = 1; j < group.Count; j++)
                        Say(g, font, Info(group[j])[0], group[j], x + GroupPad, top + (j - 1) * RowHeight);
                    float groupBottom = i == columns[c].Count - 1 ? bottom : y + TextGroupHeight(heading, group.Count - 1);
                    y = Group(g, heading, group[0], x, y, frameWidth, groupBottom);
                }
            }
            return bottom;
        }

        private float DrawOrderOfBattle(Graphics g, Font font, float left, float y, float width)
        {
            const int columns = 4;
            float cellWidth = Math.Min(170, width / (columns + 1));
            float cellHeight = BoxSize.Height + 6;
            for (int i = 0; i < GroundBoxes.Length; i++)
            {
                float x = left + (i % columns) * cellWidth;
                float top = y + (i / columns) * cellHeight;
                OOBBox(g, font, GroundBoxes[i].Key, GroundBoxes[i].Value, x, top, i == 0);
            }

            float naval = left + columns * cellWidth;
            int row = 0;
            foreach (string name in NavalBoxes)
            {
                if (config.Find(name) == null)
                    continue;
                OOBBox(g, font, OOBSymbol.Generic, name, naval, y + row * cellHeight, false);
                row++;
            }
            int rows = Math.Max(row, (GroundBoxes.Length + columns - 1) / columns);
            return y + rows * cellHeight;
        }

        private void OOBBox(Graphics g, Font font, OOBSymbol symbol, string color, float x, float y, bool selected)
        {
            Color fill = ColorOf(color, Color.Gray);
            var box = new Rectangle((int)x + 2, (int)y + 2, BoxSize.Width, BoxSize.Height);
            using (var brush = new SolidBrush(fill))
                g.FillRectangle(brush, box);
            Bitmap bmp = oobSymbols.Get(symbol, ColorOf(OOBSymbols.IconColour, Color.Black), fill, box.Size);
            if (bmp != null)
                g.DrawImage(bmp, box);

            string[] info = Info(color);
            HitRect(color, info[0], info[1], box);
            var symbolArea = new RectangleF(box.X + box.Width * 0.3f, box.Y + box.Height * 0.3f, box.Width * 0.4f, box.Height * 0.4f);
            string[] iconInfo = Info(OOBSymbols.IconColour);
            Hit(OOBSymbols.IconColour, iconInfo[0], iconInfo[1], p => symbolArea.Contains(p));

            if (selected)
            {
                var outline = new RectangleF(box.X - 2, box.Y - 2, box.Width + 3, box.Height + 3);
                using (var pen = new Pen(TextColorOf("TextHighlightColour"), 2))
                    g.DrawRectangle(pen, outline.X, outline.Y, outline.Width, outline.Height);
                RectangleF inner = RectangleF.Inflate(outline, -3, -3);
                Hit("TextHighlightColour", "Selected element", "The selected element on Order of Battle charts.", p => outline.Contains(p) && !inner.Contains(p));
            }

            float textTop = box.Y + (box.Height - g.MeasureString(info[0], font).Height) / 2;
            Say(g, font, info[0], "StandardTextColour", box.Right + 6, textTop);
        }
    }
}
