using System.Collections.Generic;
using RimWorld;
using Verse;

namespace DeckStoryteller
{
	/// <summary>
	/// Player-tunable parameters for one deck (per incident category).
	/// Entries are seeded from the XML defaults of the deck comp when it is first
	/// registered, then stored in mod settings. XML values are only defaults; the
	/// live values always come from here.
	/// </summary>
	public class DeckCategorySettings : IExposable
	{
		public string categoryDefName;

		/// <summary>If false, this category falls back to classic Cassandra-style generation.</summary>
		public bool useDeck = true;

		public float intervalDays = 15f;

		/// <summary>Guaranteed event cards per interval ("deck size" in event terms).</summary>
		public int eventsPerInterval = 1;

		/// <summary>Extra "possible" cards per interval; each is real with chanceOfPossibleEvent.</summary>
		public int possibleEventsPerInterval;

		public float chanceOfPossibleEvent = 0.5f;

		public float minSpacingDays;

		/// <summary>Blank cards padded at the start of every freshly shuffled deck (days).</summary>
		public float safeDaysAfterReshuffle;

		/// <summary>Random ±jitter of each cycle length, so reshuffle dates can't be predicted.</summary>
		public float reshuffleJitterDays = 1f;

		/// <summary>Strength randomization: threat points are multiplied by a random value in
		/// [1-x, 1+x], rolled fresh when the card fires. 0 disables.</summary>
		public float pointsRandomFactor;

		[Unsaved(false)] public string bufInterval;
		[Unsaved(false)] public string bufEvents;
		[Unsaved(false)] public string bufPossible;
		[Unsaved(false)] public string bufChance;
		[Unsaved(false)] public string bufSpacing;
		[Unsaved(false)] public string bufSafe;
		[Unsaved(false)] public string bufJitter;
		[Unsaved(false)] public string bufPointsFactor;

		public static DeckCategorySettings FromProps(StorytellerCompProperties_DeckCycle props)
		{
			DeckCategorySettings entry = new DeckCategorySettings();
			IncidentCategoryDef cat = props.IncidentCategory;
			entry.categoryDefName = cat?.defName ?? "Unknown";
			entry.useDeck = true;
			entry.intervalDays = props.intervalDays;
			entry.eventsPerInterval = props.eventsPerInterval;
			entry.possibleEventsPerInterval = props.possibleEventsPerInterval;
			entry.chanceOfPossibleEvent = props.chanceOfPossibleEvent;
			entry.minSpacingDays = props.minSpacingDays;
			entry.safeDaysAfterReshuffle = props.safeDaysAfterReshuffle;
			entry.reshuffleJitterDays = props.reshuffleJitterDays;
			entry.pointsRandomFactor = props.pointsRandomFactor;
			return entry;
		}

		public void ExposeData()
		{
			Scribe_Values.Look(ref categoryDefName, "categoryDefName");
			Scribe_Values.Look(ref useDeck, "useDeck", true);
			Scribe_Values.Look(ref intervalDays, "intervalDays", 15f);
			Scribe_Values.Look(ref eventsPerInterval, "eventsPerInterval", 1);
			Scribe_Values.Look(ref possibleEventsPerInterval, "possibleEventsPerInterval", 0);
			Scribe_Values.Look(ref chanceOfPossibleEvent, "chanceOfPossibleEvent", 0.5f);
			Scribe_Values.Look(ref minSpacingDays, "minSpacingDays", 0f);
			Scribe_Values.Look(ref safeDaysAfterReshuffle, "safeDaysAfterReshuffle", 0f);
			Scribe_Values.Look(ref reshuffleJitterDays, "reshuffleJitterDays", 1f);
			Scribe_Values.Look(ref pointsRandomFactor, "pointsRandomFactor", 0f);
		}

		/// <summary>Expected number of real events per cycle (before acceptance curves).</summary>
		public float ExpectedEventsPerInterval()
		{
			return eventsPerInterval + possibleEventsPerInterval * chanceOfPossibleEvent;
		}
	}
}
