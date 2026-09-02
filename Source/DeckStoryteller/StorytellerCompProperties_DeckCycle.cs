using System.Collections.Generic;
using RimWorld;
using Verse;

namespace DeckStoryteller
{
	/// <summary>
	/// XML properties of a deck-driven storyteller comp. All numeric fields here are
	/// only DEFAULTS: they seed the mod settings entry for the category when the def is
	/// loaded, and the player-tuned settings values are what actually runs the game.
	/// </summary>
	public class StorytellerCompProperties_DeckCycle : StorytellerCompProperties
	{
		public IncidentCategoryDef category;

		/// <summary>Optional single incident this deck deals (e.g. OrbitalTraderArrival).</summary>
		public IncidentDef incident;

		/// <summary>Length of one deck cycle in days.</summary>
		public float intervalDays = 15f;

		/// <summary>Guaranteed event cards per cycle.</summary>
		public int eventsPerInterval = 1;

		/// <summary>Extra possible cards per cycle, each real with chanceOfPossibleEvent.</summary>
		public int possibleEventsPerInterval;

		public float chanceOfPossibleEvent = 0.5f;

		/// <summary>Minimum spacing between two dealt cards of the same cycle.</summary>
		public float minSpacingDays;

		/// <summary>Blank cards at the start of every freshly shuffled deck (days).</summary>
		public float safeDaysAfterReshuffle;

		/// <summary>Random ±jitter of each cycle length (days), making reshuffles unpredictable.</summary>
		public float reshuffleJitterDays = 1f;

		/// <summary>Strength randomization: threat points are multiplied by a random value in
		/// [1-x, 1+x], rolled fresh when the card fires. 0 disables.</summary>
		public float pointsRandomFactor;

		/// <summary>Mirror of Cassandra's early-game behavior: force plain enemy raids before this day.</summary>
		public float forceRaidEnemyBeforeDaysPassed;

		/// <summary>Per-card acceptance chance curve by days passed (e.g. quest ramp-up).</summary>
		public SimpleCurve acceptFractionByDaysPassedCurve;

		/// <summary>Per-card acceptance chance curve by current threat points (e.g. small threat decay).</summary>
		public SimpleCurve acceptPercentFactorPerThreatPointsCurve;

		public IncidentCategoryDef IncidentCategory => incident != null ? incident.category : category;

		public StorytellerCompProperties_DeckCycle()
		{
			compClass = typeof(StorytellerComp_DeckCycle);
		}

		public override IEnumerable<string> ConfigErrors(StorytellerDef parentDef)
		{
			foreach (string error in base.ConfigErrors(parentDef))
			{
				yield return error;
			}
			if (incident != null && category != null)
			{
				yield return "incident and category should not both be defined";
			}
			if (incident == null && category == null)
			{
				yield return "either incident or category must be defined";
			}
			if (intervalDays <= 0f)
			{
				yield return "intervalDays must be above zero";
			}
			if (eventsPerInterval < 0 || possibleEventsPerInterval < 0)
			{
				yield return "event counts must not be negative";
			}
			if (chanceOfPossibleEvent < 0f || chanceOfPossibleEvent > 1f)
			{
				yield return "chanceOfPossibleEvent must be within 0-1";
			}
			if (eventsPerInterval + possibleEventsPerInterval <= 0)
			{
				yield return "deck has no cards: eventsPerInterval + possibleEventsPerInterval must be above zero";
			}
			if (minSpacingDays > 0f && minSpacingDays * (eventsPerInterval + possibleEventsPerInterval) > intervalDays * 0.9f)
			{
				yield return "minSpacingDays too high compared to interval and max number of cards";
			}
			if (safeDaysAfterReshuffle >= intervalDays)
			{
				yield return "safeDaysAfterReshuffle must be smaller than intervalDays, otherwise no card can ever be placed";
			}
		}

		public override void ResolveReferences(StorytellerDef parentDef)
		{
			base.ResolveReferences(parentDef);
			// Register this deck so it shows up in mod settings. Mod classes are created
			// before def resolution, so the settings store already exists here.
			DeckStorytellerMod.RegisterDeck(this);
		}
	}
}
