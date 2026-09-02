using System.Collections.Generic;
using Verse;

namespace DeckStoryteller
{
	/// <summary>
	/// Holds one DeckState per incident category. Instantiated automatically by the game
	/// for every new/loaded game (Game.FillComponents picks up all GameComponent subclasses).
	/// </summary>
	public class DeckStorytellerGameComp : GameComponent
	{
		private Dictionary<RimWorld.IncidentCategoryDef, DeckState> decks =
			new Dictionary<RimWorld.IncidentCategoryDef, DeckState>();

		public DeckStorytellerGameComp()
		{
		}

		public DeckStorytellerGameComp(Game game)
		{
		}

		public static DeckStorytellerGameComp Get()
		{
			Game game = Current.Game;
			if (game == null)
			{
				return null;
			}
			return game.GetComponent<DeckStorytellerGameComp>();
		}

		public DeckState GetOrCreateDeck(RimWorld.IncidentCategoryDef category)
		{
			if (decks == null)
			{
				decks = new Dictionary<RimWorld.IncidentCategoryDef, DeckState>();
			}
			if (decks.TryGetValue(category, out DeckState deck) && deck != null)
			{
				return deck;
			}
			deck = new DeckState();
			deck.category = category;
			decks[category] = deck;
			return deck;
		}

		/// <summary>The category's deck state, or null if it has none yet (status alert).</summary>
		public DeckState GetDeckOrNull(RimWorld.IncidentCategoryDef category)
		{
			return decks != null && decks.TryGetValue(category, out DeckState deck) ? deck : null;
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Collections.Look(ref decks, "decks", LookMode.Def, LookMode.Deep);
			if (Scribe.mode == LoadSaveMode.ResolvingCrossRefs)
			{
				if (decks == null)
				{
					decks = new Dictionary<RimWorld.IncidentCategoryDef, DeckState>();
				}
				else
				{
					// Drop entries whose state failed to load (e.g. corrupted sub-object).
					// Null keys cannot occur: vanilla BuildDictionary skips them.
					List<RimWorld.IncidentCategoryDef> deadKeys = null;
					foreach (KeyValuePair<RimWorld.IncidentCategoryDef, DeckState> pair in decks)
					{
						// Also prune orphaned states: categories whose deck comps no longer exist
						// in any loaded def (e.g. a third-party deck pairing that was removed).
						// Def registration happens at mod load, before any savegame loads.
						if (pair.Value == null || !DeckStorytellerMod.IsDeckRegistered(pair.Key.defName))
						{
							(deadKeys ?? (deadKeys = new List<RimWorld.IncidentCategoryDef>())).Add(pair.Key);
						}
					}
					if (deadKeys != null)
					{
						for (int i = 0; i < deadKeys.Count; i++)
						{
							decks.Remove(deadKeys[i]);
						}
					}
					// category is not serialized (the key carries it); restore it so the field
					// is never a stale null for code that reads it after a load.
					foreach (KeyValuePair<RimWorld.IncidentCategoryDef, DeckState> pair in decks)
					{
						pair.Value.category = pair.Key;
					}
				}
			}
		}
	}
}
