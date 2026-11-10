using System.Collections.Generic;

namespace AuroraThemeManager
{
    internal static class AuroraDefaults
    {
        internal const string DefaultName = "Aurora Default";
        internal const string AlternateName = "ATM Alternate";

        internal static readonly string[] FontFallbacks = { "Microsoft Sans Serif", "Segoe UI", "Arial", "Calibri", "Verdana" };
        internal static readonly string[] ExcludedFonts = { "Standard Symbols PS" };

        /// <summary>
        /// The themes that ship with ATM.  Aurora Default is Aurora's own config, embedded from
        /// Resources/Themes and also used to create a missing AuroraConfig.xml; ATM Alternate is below.
        /// Both are AuroraConfig.xml text, the same format as saved themes.
        /// </summary>
        internal static readonly string[] BuiltInThemes = { DefaultName, AlternateName };

        internal static string ThemeResource(string name)
        {
            return "AuroraThemeManager.Resources.Themes." + name + ".xml";
        }

        private static Dictionary<string, Dictionary<string, string>> presets;

        internal static Dictionary<string, Dictionary<string, string>> Presets
        {
            get
            {
                if (presets == null)
                {
                    var loaded = new Dictionary<string, Dictionary<string, string>>();
                    loaded[DefaultName] = AuroraConfig.LoadResource(ThemeResource(DefaultName)).ToPreset();
                    loaded[AlternateName] = AuroraConfig.FromText(AlternateXml).ToPreset();
                    presets = loaded;
                }
                return presets;
            }
        }

        private const string AlternateXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<AuroraConfig>
  <Colours>

    <!-- General Colours -->
    <Background R=""18"" G=""22"" B=""31"" A=""255""/>
    <ButtonColour R=""28"" G=""37"" B=""48"" A=""255""/>
    <StandardTextColour R=""200"" G=""205"" B=""212"" A=""255""/>
    <TextEditColour R=""130"" G=""240"" B=""190""/>
    <PositiveColour R=""90"" G=""200"" B=""90""/>
    <TextHighlightColour R=""0"" G=""230"" B=""230""/>
    <IconColour R=""184"" G=""190"" B=""199"" A=""255""/>
    <ToolbarButtonColour R=""28"" G=""37"" B=""48"" A=""255""/>

    <!-- Tactical Map Bodies -->
    <TacMapSystemBodyColour R=""58"" G=""124"" B=""58"" A=""255""/>
    <TacMapBodyNameColour R=""212"" G=""168"" B=""87""/>
    <GasGiantColour R=""180"" G=""100"" B=""200""/>
    <CometColour R=""25"" G=""45"" B=""217"" A=""255""/>
    <TacMapOrbitColour R=""46"" G=""53"" B=""64"" A=""128""/>
    <ColonyCostLessThanTwoColour R=""76"" G=""175"" B=""110""/>
    <ColonyCostLessThanThreeColour R=""232"" G=""197"" B=""71""/>
    <ColonyCostLessThanFiveColour R=""224"" G=""108"" B=""62""/>
    <TacMapCometOutlineColour R=""100"" G=""180"" B=""180""/>

    <!-- Tactical Map Other Astronomical -->
    <JumpPointColour R=""142"" G=""68"" B=""173""/>
    <TacMapLagrangePointColour R=""142"" G=""68"" B=""173""/>
    <TacMapSurveyLocationColour R=""245"" G=""222"" B=""179""/>
    <TacMapAetherGateColour R=""220"" G=""20"" B=""60"" A=""255""/>
    <TacMapRiftOutlineColour R=""153"" G=""50"" B=""204""/>
    <TacMapRiftCentreColour R=""0"" G=""0"" B=""0""/>

    <!-- Tactical Map Artificial Objects -->
    <TacMapDeepSpacePopColour R=""255"" G=""255"" B=""0""/>
    <TacMapFleetColour R=""242"" G=""242"" B=""30""/>
    <TacMapSalvoColour R=""164"" G=""200"" B=""225""/>
    <TacMapCivilianColour R=""59"" G=""214"" B=""214""/>
    <WreckColour R=""214"" G=""99"" B=""107""/>
    <TacMapWaypointColour R=""238"" G=""130"" B=""238""/>
    <TacMapMDPacketColour R=""220"" G=""220"" B=""220""/>
    <TacMapLifepodColour R=""255"" G=""215"" B=""0""/>

    <!-- Tactical Map Range Circles -->
    <TacMapActiveSensorColour R=""159"" G=""197"" B=""232"" A=""255""/>
    <TacMapThermalSensorColour R=""232"" G=""155"" B=""143"" A=""255""/>
    <TacMapEMSensorColour R=""201"" G=""143"" B=""201"" A=""255""/>
    <TacMapELINTSensorColour R=""240"" G=""248"" B=""255"" A=""255""/>
    <TacMapFireControlColour R=""232"" G=""201"" B=""122"" A=""255""/>
    <TacMapWeaponRangeColour R=""224"" G=""165"" B=""74"" A=""255""/>
    <TacMapAlienSensorColour R=""255"" G=""0"" B=""0"" A=""255""/>

    <!-- Tactical Map Body Circles and Lines -->
    <TacMapSurveyedColour R=""180"" G=""180"" B=""180""/>
    <TacMapMineralsColour R=""255"" G=""255"" B=""0""/>
    <TacMapColonyColour R=""80"" G=""150"" B=""80""/>
    <TacMapDistanceLineColour R=""0"" G=""128"" B=""0""/>
    <TacticalScaleColour R=""130"" G=""240"" B=""190""/>

    <!-- Galactic Map System Icons -->
    <GalMapSystemIconColour R=""58"" G=""124"" B=""58""/>
    <GalMapSystemOutlineColour R=""87"" G=""186"" B=""87"" A=""255""/>
    <GalMapSystemSelectedColour R=""0"" G=""255"" B=""255""/>
    <GalMapSupernovaColour R=""255"" G=""255"" B=""255"" A=""255""/>
    <GalMapBlackHoleColour R=""128"" G=""0"" B=""128"" A=""255""/>
    <GalMapCometsColour R=""255"" G=""248"" B=""220""/>

    <!-- Galactic Map Circles -->
    <GalMapNavalHQColour R=""255"" G=""255"" B=""0""/>
    <GalMapSurveyIncompleteColour R=""255"" G=""0"" B=""0""/>
    <GalMapMaintenanceLocationColour R=""0"" G=""191"" B=""255""/>
    <GalMapRestrictedColour R=""214"" G=""59"" B=""59""/>
    <GalMapGroundSurveyColour R=""176"" G=""196"" B=""222""/>
    <GalMapMineralSearchColour R=""143"" G=""188"" B=""139"" A=""255""/>
    <GalMapDormantJPColour R=""255"" G=""255"" B=""0"" A=""255""/>

    <!-- Galactic Map Lines -->
    <GalMapJumpPointConnectionsColour R=""46"" G=""53"" B=""64"" A=""96""/>
    <GalMapStabilisedConnectionsColour R=""142"" G=""68"" B=""173"" A=""255""/>
    <GalMapMultiSystemSelectColour R=""255"" G=""0"" B=""0"" A=""255""/>

    <!-- Non-Hostile Relations -->
    <NeutralColour R=""136"" G=""59"" B=""214""/>
    <FriendlyColour R=""242"" G=""242"" B=""30""/>
    <AlliedColour R=""34"" G=""139"" B=""34""/>

    <!-- original red (255, 0, 0) -->
    <HostileColour R=""214"" G=""59"" B=""59""/>
    <CriticalColour R=""220"" G=""0"" B=""0""/>
    <ArmourDamagedColour R=""220"" G=""0"" B=""0""/>
    <AlienComponentColour R=""220"" G=""0"" B=""0""/>
    <JumpPointAlienColour R=""220"" G=""0"" B=""0""/>

    <!-- original orange or close (255, 165, 0) -->
    <WarningColour R=""235"" G=""140"" B=""0""/>
    <ObsoleteColour R=""200"" G=""111"" B=""5""/>
    <MatchingComponentColour R=""235"" G=""140"" B=""0""/>
    <NoResearchColour R=""235"" G=""140"" B=""0""/>
    <SupportedFormationColour R=""235"" G=""140"" B=""0""/>

    <!-- original light green (128, 255, 128) -->
    <NavalAdminCommandColour R=""90"" G=""200"" B=""90""/>
    <ComponentTechDataColour R=""90"" G=""200"" B=""90""/>
    <GroundForcesOrganisationColour R=""90"" G=""200"" B=""90""/>
    <CivilianShippingLineColour R=""90"" G=""200"" B=""90""/>
    <OrdnanceSeriesColour R=""90"" G=""200"" B=""90""/>

    <!-- original lime green (50, 205, 50) -->
    <PopulatedColonyColour R=""0"" G=""255"" B=""160"" A=""255""/>

    <!-- original cyan (0, 255, 255) -->
    <NavalSubFleetColour R=""0"" G=""220"" B=""220""/>
    <NavalSquadronColour R=""0"" G=""220"" B=""220""/>
    <OtherColonyColour R=""0"" G=""220"" B=""220""/>
    <CMCPurchaseColonyColour R=""0"" G=""220"" B=""220""/>
    <ComponentResearchColour R=""0"" G=""220"" B=""220""/>
    <PrototypeComponentColour R=""0"" G=""220"" B=""220""/>

    <!-- original magenta (255, 0, 255) -->
    <PrisonerColour R=""220"" G=""0"" B=""220""/>

    <!-- original white or close (255, 255, 255) -->
    <ArmourColour R=""245"" G=""245"" B=""245""/>

    <MiningColonyColour R=""60"" G=""179"" B=""113""/>
    <Map3DSystemIconColour R=""220"" G=""220"" B=""255""/>
    <SupportingFormationColour R=""135"" G=""206"" B=""250""/>

    <!-- Order of Battle Chart -->
    <OrgChartInfantryColour R=""70"" G=""160"" B=""70"" A=""255""/>
    <OrgChartMechanisedColour R=""60"" G=""135"" B=""95"" A=""255""/>
    <OrgChartArmourColour R=""240"" G=""220"" B=""0"" A=""255""/>
    <OrgChartReconnaissanceColour R=""60"" G=""160"" B=""170"" A=""255""/>
    <OrgChartArtilleryColour R=""210"" G=""30"" B=""30"" A=""255""/>
    <OrgChartAirDefenceColour R=""210"" G=""90"" B=""30"" A=""255""/>
    <OrgChartOrbitalColour R=""90"" G=""120"" B=""200"" A=""255""/>
    <OrgChartEngineerColour R=""120"" G=""120"" B=""120"" A=""255""/>
    <OrgChartSpecialistColour R=""140"" G=""100"" B=""190""/>
    <OrgChartSupportColour R=""200"" G=""200"" B=""200""/>
    <OrgChartSupplyColour R=""150"" G=""90"" B=""30""/>
    <OrgChartGenericColour R=""180"" G=""180"" B=""180""/>
    <OrgChartIconColour R=""0"" G=""0"" B=""0""/>

    <NavalFleetBoxColour R=""0"" G=""255"" B=""255""/>
    <NavalSubFleetBoxColour R=""80"" G=""160"" B=""200""/>

  </Colours>
  <ButtonStyle>
    <FlatStyle>Standard</FlatStyle>
    <BorderSize>0</BorderSize>
  </ButtonStyle>
  <ToolbarButtonStyle>
    <FlatStyle>Flat</FlatStyle>
    <BorderSize>1</BorderSize>
  </ToolbarButtonStyle>
  <MapDisplay>
      <FontFamily>Microsoft Sans Serif</FontFamily>
      <!-- <FontFamily>Segoe UI</FontFamily> -->
      <!-- <FontFamily>Arial</FontFamily> -->
      <!-- <FontFamily>Calibri</FontFamily> -->
      <!-- <FontFamily>Verdana</FontFamily> -->
  </MapDisplay>
  <EventLog>
    <!-- Events from this many recent days are always saved. -->
    <!-- Older events are saved, newest first, up to a total of 10,000. -->
    <MinimumSaveDays>90</MinimumSaveDays>
  </EventLog>
</AuroraConfig>
";
    }
}
