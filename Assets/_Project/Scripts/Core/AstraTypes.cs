using UnityEngine;

namespace Astra
{
    public enum AstraMissionId { FirstLight = 0, AirAndWater = 1, ShadowScout = 2, StormWatch = 3, FoodForTheLongStay = 4 }
    public enum AstraDestinationId { Moon = 0, Mars = 1, Europa = 2 }

    public enum AstraObjectiveId
    {
        Communications, Solar, LifeSupport, Shelter, CropHarvest, StormResponse, RoverSample, OxygenRestored, WaterRestored,
        WeatherReportReviewed, ProtectedAreaReadiness, PostDrillInspection,
        EuropaRoutePlanned, EuropaSiteInspected
    }

    public sealed class AstraMissionDefinition
    {
        public readonly AstraMissionId Id;
        public readonly AstraDestinationId Destination;
        public readonly string Title, Subtitle, Briefing, Lesson, SourceLabel, SourceUrl;
        public readonly AstraObjectiveId[] Objectives;
        public readonly int CropHarvestsRequired;

        public AstraMissionDefinition(AstraMissionId id, AstraDestinationId destination, string title, string subtitle,
            string briefing, string lesson, string sourceLabel, string sourceUrl, int cropHarvestsRequired,
            params AstraObjectiveId[] objectives)
        {
            Id = id; Destination = destination; Title = title; Subtitle = subtitle; Briefing = briefing;
            Lesson = lesson; SourceLabel = sourceLabel; SourceUrl = sourceUrl;
            CropHarvestsRequired = cropHarvestsRequired; Objectives = objectives;
        }
    }

    public static class AstraMissionCatalog
    {
        private static readonly AstraMissionDefinition[] Missions =
        {
            new AstraMissionDefinition(AstraMissionId.FirstLight, AstraDestinationId.Moon, "First Light", "Stabilize the lunar outpost",
                "Commission the communications station, compare solar power before and after repositioning the array, and inspect the habitat life-support readings to bring the lunar outpost online.",
                "NASA's Artemis program is developing sustained lunar exploration. Real habitats depend on communications, power, and life support working together. ASTRA's outpost readings and equipment are simplified teaching models, not flight specifications.",
                "NASA Artemis + ECLSS", "https://www.nasa.gov/reference/environmental-control-and-life-support-systems-eclss/", 1,
                AstraObjectiveId.Communications, AstraObjectiveId.Solar, AstraObjectiveId.LifeSupport),
            new AstraMissionDefinition(AstraMissionId.AirAndWater, AstraDestinationId.Moon, "Air & Water", "Recover clean water, then generate oxygen",
                "Read the habitat water and oxygen reserves. Run the water recycler first, then use recovered water in the oxygen generator and compare the before-and-after readings.",
                "NASA's Environmental Control and Life Support System recovers water and uses electrolysis to make oxygen. ASTRA models the order of those processes with fictional game values; it is not an operating procedure.",
                "NASA ECLSS", "https://www.nasa.gov/reference/environmental-control-and-life-support-systems-eclss/", 1,
                AstraObjectiveId.WaterRestored, AstraObjectiveId.OxygenRestored),
            new AstraMissionDefinition(AstraMissionId.ShadowScout, AstraDestinationId.Europa, "Shadow Scout", "Survey an icy moon",
                "Compare two rover routes around icy terrain, choose a route, travel to and inspect the marked site, collect a virtual surface sample, and classify what the evidence can—and cannot—show.",
                "Europa is an icy moon with a global ocean beneath its crust. The ASTRA rover route and sample are fictional; a surface sample cannot be treated as a sample of the subsurface ocean.",
                "NASA Europa", "https://science.nasa.gov/jupiter/moons/europa/", 1,
                AstraObjectiveId.EuropaRoutePlanned, AstraObjectiveId.EuropaSiteInspected, AstraObjectiveId.RoverSample),
            new AstraMissionDefinition(AstraMissionId.StormWatch, AstraDestinationId.Mars, "Storm Watch", "Protect the crew during a Mars dust-storm drill",
                "Review NASA DONKI space-weather context and a separate, clearly labeled Mars dust-storm training scenario. Check essential equipment, choose when to pause the rover traverse, guide the astronaut to the protected area, then inspect the outpost and compare mission time and battery readings.",
                "DONKI catalogs solar energetic particle events; it does not forecast Martian dust storms or provide a Mars surface dose. The Mars dust-storm drill and its time and battery effects are fictional ASTRA training values.",
                "NASA DONKI + Mars exploration", "https://ccmc.gsfc.nasa.gov/DONKI/", 1,
                AstraObjectiveId.WeatherReportReviewed, AstraObjectiveId.ProtectedAreaReadiness,
                AstraObjectiveId.StormResponse, AstraObjectiveId.PostDrillInspection),
            new AstraMissionDefinition(AstraMissionId.FoodForTheLongStay, AstraDestinationId.Moon, "Food for the Long Stay", "Grow crops and compare two harvests",
                "Choose a crop and its light-and-water plan, watch two shortened growth cycles, harvest both crops, and compare recorded yields with the resources each cycle used.",
                "NASA studies plant growth as a way to provide fresh food and support future long-duration missions. ASTRA's crop choices, light settings, times, yields, and costs are simplified fictional game values, not horticulture instructions.",
                "NASA Space Crops", "https://science.nasa.gov/biological-physical/space-crops/", 2,
                AstraObjectiveId.CropHarvest)
        };

        public static int Count => Missions.Length;
        public static AstraMissionDefinition Get(AstraMissionId id) => Missions[Mathf.Clamp((int)id, 0, Missions.Length - 1)];
        public static AstraMissionDefinition Get(int index) => Missions[Mathf.Clamp(index, 0, Missions.Length - 1)];

        public static string DestinationName(AstraDestinationId id)
        {
            switch (id) { case AstraDestinationId.Mars: return "MARS"; case AstraDestinationId.Europa: return "EUROPA"; default: return "MOON"; }
        }

        public static string ObjectiveTitle(AstraObjectiveId id)
        {
            switch (id)
            {
                case AstraObjectiveId.Communications: return "Activate the communications station";
                case AstraObjectiveId.Solar: return "Bring the solar array online";
                case AstraObjectiveId.LifeSupport: return "Inspect life-support systems";
                case AstraObjectiveId.Shelter: return "Prepare the radiation shelter";
                case AstraObjectiveId.CropHarvest: return "Complete the crop harvest";
                case AstraObjectiveId.StormResponse: return "Pause outdoor work and shelter during the Mars dust-storm drill";
                case AstraObjectiveId.RoverSample: return "Collect a virtual rover sample and record its evidence";
                case AstraObjectiveId.OxygenRestored: return "Generate breathing oxygen";
                case AstraObjectiveId.WaterRestored: return "Recover clean water";
                case AstraObjectiveId.WeatherReportReviewed: return "Review NASA DONKI space-weather context";
                case AstraObjectiveId.ProtectedAreaReadiness: return "Prepare Mars storm equipment and the protected area";
                case AstraObjectiveId.PostDrillInspection: return "Inspect the outpost and submit the post-drill report";
                case AstraObjectiveId.EuropaRoutePlanned: return "Plan a rover route around Europa's icy terrain";
                case AstraObjectiveId.EuropaSiteInspected: return "Drive to and inspect the Europa sample site";
                default: return id.ToString();
            }
        }
    }

    public static class AstraCampaignProgress
    {
        private const string UnlockedKey = "ASTRA_CAMPAIGN_UNLOCKED_V3";
        private const string DestinationKey = "ASTRA_DESTINATION_REACHED_V3";
        // Keep the player's earned campaign progress when the mission catalog is reordered.
        public static int HighestUnlocked => Mathf.Clamp(PlayerPrefs.GetInt(UnlockedKey, PlayerPrefs.GetInt("ASTRA_CAMPAIGN_UNLOCKED_V2", 0)), 0, AstraMissionCatalog.Count - 1);
        public static AstraDestinationId HighestDestinationReached => (AstraDestinationId)Mathf.Clamp(
            PlayerPrefs.GetInt(DestinationKey, PlayerPrefs.GetInt("ASTRA_DESTINATION_REACHED_V2", 0)), 0, 2);
        public static int BestScore(AstraMissionId mission)
        {
            int previousId = mission == AstraMissionId.FirstLight ? 0 :
                mission == AstraMissionId.ShadowScout ? 3 :
                mission == AstraMissionId.StormWatch ? 2 :
                mission == AstraMissionId.FoodForTheLongStay ? 1 : -1;
            return PlayerPrefs.GetInt("ASTRA_BEST_SCORE_V3_" + (int)mission,
                previousId >= 0 ? PlayerPrefs.GetInt("ASTRA_BEST_SCORE_V2_" + previousId, 0) : 0);
        }
        public static bool IsUnlocked(AstraMissionId mission) => (int)mission <= HighestUnlocked;
        public static bool IsMissionAvailable(AstraMissionId mission)
        {
            AstraMissionDefinition definition = AstraMissionCatalog.Get(mission);
            return IsUnlocked(mission) && (int)definition.Destination <= (int)HighestDestinationReached;
        }

        public static void RecordSuccess(AstraMissionId mission, int score)
        {
            string scoreKey = "ASTRA_BEST_SCORE_V3_" + (int)mission;
            if (score > PlayerPrefs.GetInt(scoreKey, 0)) PlayerPrefs.SetInt(scoreKey, score);
            int nextUnlocked = Mathf.Min(AstraMissionCatalog.Count - 1, (int)mission + 1);
            if (nextUnlocked > HighestUnlocked) PlayerPrefs.SetInt(UnlockedKey, nextUnlocked);
            PlayerPrefs.Save();
        }

        public static void MarkDestinationReached(AstraDestinationId destination)
        {
            if ((int)destination > (int)HighestDestinationReached)
                PlayerPrefs.SetInt(DestinationKey, (int)destination);
            PlayerPrefs.Save();
        }
    }

    public enum FacilityType
    {
        Communications, SolarArray, LifeSupport, Habitat, Greenhouse, OxygenProcessor,
        WaterRecycler, ResearchLab, Rover, RadiationShelter, Spacecraft, SpaceWeatherStation
    }

    [System.Serializable]
    public class MissionObjective
    {
        public AstraObjectiveId id;
        public string title;
        public bool complete;
        public MissionObjective(AstraObjectiveId id) { this.id = id; title = AstraMissionCatalog.ObjectiveTitle(id); complete = false; }
    }
}
