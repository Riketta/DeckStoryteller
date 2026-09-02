using System.Collections.Generic;
using RimWorld;
using Verse;

namespace DeckStoryteller
{
	/// <summary>
	/// Persistent state of one category's deck. Stored in a GameComponent, keyed by
	/// incident category, so it survives save/load even though storyteller comps are
	/// recreated on every load.
	///
	/// A "deck" here is a pre-generated queue of event times for the current cycle:
	/// `pendingOffsets` holds tick offsets relative to `cycleStartTick` at which a card
	/// will be dealt. Blank cards are simply the gaps between offsets.
	/// </summary>
	public class DeckState : IExposable
	{
		public IncidentCategoryDef category;

		/// <summary>Tick (game time) at which the current cycle started. -1 = never generated.</summary>
		public int cycleStartTick = -1;

		/// <summary>Duration of the current cycle in ticks (interval + random reshuffle jitter).</summary>
		public int cycleDurationTicks;

		/// <summary>Sorted tick offsets (after cycleStartTick) at which cards fire.</summary>
		public List<int> pendingOffsets = new List<int>();

		/// <summary>How many cards of the current cycle were already dealt or burned.</summary>
		public int consumedCount;

		/// <summary>Whether the very first cycle of this deck has already been generated.</summary>
		public bool firstCycleDone;

		/// <summary>Last 1000-tick storyteller interval at which this deck was processed.</summary>
		public int lastProcessTick = -1;

		public void ExposeData()
		{
			// category is not persisted: the dictionary key (LookMode.Def) already carries it.
			Scribe_Values.Look(ref cycleStartTick, "cycleStartTick", -1);
			Scribe_Values.Look(ref cycleDurationTicks, "cycleDurationTicks", 0);
			Scribe_Collections.Look(ref pendingOffsets, "pendingOffsets", LookMode.Value);
			if (Scribe.mode == LoadSaveMode.LoadingVars && pendingOffsets == null)
			{
				// A missing/corrupt save node must not leave a null list: card processing indexes
				// pendingOffsets unconditionally (null DeckStates are pruned by the GameComp,
				// null lists inside a live state are not).
				pendingOffsets = new List<int>();
			}
			Scribe_Values.Look(ref consumedCount, "consumedCount", 0);
			Scribe_Values.Look(ref firstCycleDone, "firstCycleDone", false);
			Scribe_Values.Look(ref lastProcessTick, "lastProcessTick", -1);
		}
	}
}
