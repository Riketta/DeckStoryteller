using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace DeckStoryteller
{
	public class DeckStorytellerSettings : ModSettings
	{
	public List<DeckCategorySettings> categories = new List<DeckCategorySettings>();

	/// <summary>Dev aid: logs each deck shuffle and every card fired/burned to the game log.</summary>
	public bool debugLogging;

	/// <summary>Dev aid: shows a passive alert with each deck's remaining cards and ETAs.</summary>
	public bool showDeckStatusAlert;

	/// <summary>Global: when on, threat strength is rolled twice (two independent mean-1
	/// multipliers), reproducing the more centered distribution of the old two-factor scaling.
	/// Default on: the shipped ±0.35 jitter is tuned for the convolved double-roll spread.</summary>
	public bool doubleRollJitter = true;

	public override void ExposeData()
	{
		base.ExposeData();
		Scribe_Collections.Look(ref categories, "categories", LookMode.Deep);
		Scribe_Values.Look(ref debugLogging, "debugLogging", false);
		Scribe_Values.Look(ref showDeckStatusAlert, "showDeckStatusAlert", false);
		Scribe_Values.Look(ref doubleRollJitter, "doubleRollJitter", true);
		if (Scribe.mode == LoadSaveMode.ResolvingCrossRefs && categories == null)
		{
			categories = new List<DeckCategorySettings>();
		}
	}

		public DeckCategorySettings Get(string categoryDefName)
		{
			if (categories == null)
			{
				return null;
			}
			for (int i = 0; i < categories.Count; i++)
			{
				if (categories[i].categoryDefName == categoryDefName)
				{
					return categories[i];
				}
			}
			return null;
		}
	}

	public class DeckStorytellerMod : Mod
	{
		public static DeckStorytellerMod Instance;

		public DeckStorytellerSettings settings;

		/// <summary>All deck comps discovered in loaded storyteller defs, keyed by category defName.</summary>
		private static readonly Dictionary<string, StorytellerCompProperties_DeckCycle> registeredDecks =
			new Dictionary<string, StorytellerCompProperties_DeckCycle>();

		private Vector2 scrollPosition = Vector2.zero;

		/// <summary>Content height of the settings list measured on the previous draw; the
		/// exact scroll size is only known after the listing has been rendered once.</summary>
		private float measuredListHeight;

		public DeckStorytellerMod(ModContentPack content) : base(content)
		{
			Instance = this;
			settings = GetSettings<DeckStorytellerSettings>();
			// Dev "reload mods" re-runs mod class creation and def resolution; clearing here
			// drops deck comps removed since the last load before ResolveReferences repopulates.
			registeredDecks.Clear();
		}

		public static void RegisterDeck(StorytellerCompProperties_DeckCycle props)
		{
			IncidentCategoryDef cat = props.IncidentCategory;
			if (cat == null)
			{
				return;
			}
			registeredDecks[cat.defName] = props;
			EnsureSettingsEntry(cat.defName, props);
		}

		/// <summary>Returns the persisted settings entry for a category, seeding it from the
		/// XML defaults on first sight so all callers share one code path and one object.</summary>
		private static DeckCategorySettings EnsureSettingsEntry(string categoryDefName,
			StorytellerCompProperties_DeckCycle props)
		{
			DeckStorytellerSettings store = Instance?.settings;
			if (store == null)
			{
				return null;
			}
			DeckCategorySettings entry = store.Get(categoryDefName);
			if (entry == null)
			{
				entry = DeckCategorySettings.FromProps(props);
				store.categories.Add(entry);
			}
			return entry;
		}

		/// <summary>Whether the deck comp should generate this category (false = run fallbacks).</summary>
		public static bool IsDeckEnabled(IncidentCategoryDef cat)
		{
			if (cat == null || !registeredDecks.ContainsKey(cat.defName))
			{
				// No deck comp registered for this category (e.g. a third-party fallback
				// pairing whose deck def was removed): a stale settings entry must not mute
				// the fallback forever.
				return false;
			}
			DeckCategorySettings entry = Instance?.settings?.Get(cat.defName);
			return entry == null || entry.useDeck;
		}

		/// <summary>Whether a deck comp is registered for this category defName.</summary>
		public static bool IsDeckRegistered(string categoryDefName)
		{
			return registeredDecks.ContainsKey(categoryDefName);
		}

		/// <summary>The live tuning for a category deck, or null if it doesn't exist.</summary>
		public static DeckCategorySettings GetEffectiveSettings(IncidentCategoryDef cat)
		{
			if (cat == null)
			{
				return null;
			}
			DeckCategorySettings entry = Instance?.settings?.Get(cat.defName);
			if (entry != null)
			{
				return entry;
			}
			// No persisted entry (e.g. settings file predates this category): seed one from
			// the XML defaults instead of building a throwaway copy on every call, so the
			// values persist and behave identically to registered decks.
			return registeredDecks.TryGetValue(cat.defName, out StorytellerCompProperties_DeckCycle props)
				? EnsureSettingsEntry(cat.defName, props)
				: null;
		}

		/// <summary>Whether dev logging of deck generation and card deals is enabled.</summary>
		public static bool DebugLogging => Instance?.settings?.debugLogging ?? false;

		/// <summary>Whether the passive deck status alert is enabled.</summary>
		public static bool ShowDeckStatusAlert => Instance?.settings?.showDeckStatusAlert ?? false;

		/// <summary>Registered deck comps by category defName, read-only view for the status alert.</summary>
		public static IReadOnlyDictionary<string, StorytellerCompProperties_DeckCycle> RegisteredDecks => registeredDecks;

		/// <summary>Whether threat strength is rolled with two independent jitter multipliers.</summary>
		public static bool DoubleRollJitter => Instance?.settings?.doubleRollJitter ?? false;

		public override string SettingsCategory()
		{
			return "DeckStoryteller_SettingsCategory".Translate();
		}

		public override void DoSettingsWindowContents(Rect inRect)
		{
			Text.Font = GameFont.Small;
			// Reset button + hint are pinned OUTSIDE the scroll view so they stay reachable
			// no matter how tall the deck list grows (long translations, third-party decks).
			float footerHeight = Text.LineHeight * 6f + 16f;
			Rect footerRect = inRect.BottomPartPixels(footerHeight);
			Rect scrollRect = inRect.TopPartPixels(inRect.height - footerHeight - 4f);

			// Height depends on how many decks are expanded (toggled on): collapsed decks only
			// need the checkbox + hint rows. The first frame estimates from the deck count
			// (padded generously to absorb label wrapping); later frames use the exact content
			// height measured from the previous draw, which stays correct however labels wrap.
			float listHeight = measuredListHeight;
			if (listHeight <= 0f)
			{
				int enabledDecks = 0;
				foreach (string key in registeredDecks.Keys)
				{
					DeckCategorySettings entry = settings.Get(key);
					if (entry?.useDeck ?? true)
					{
						enabledDecks++;
					}
				}
				listHeight = 190f + enabledDecks * 520f + (registeredDecks.Count - enabledDecks) * 160f;
			}
			Rect viewRect = new Rect(0f, 0f, scrollRect.width - 20f, listHeight);
			Widgets.BeginScrollView(scrollRect, ref scrollPosition, viewRect);
			Listing_Standard listing = new Listing_Standard();
			listing.Begin(viewRect);
			listing.Label("DeckStoryteller_Intro".Translate());
			listing.GapLine();

			foreach (string deckKey in registeredDecks.Keys.OrderBy((string k) => k))
			{
				StorytellerCompProperties_DeckCycle props = registeredDecks[deckKey];
				IncidentCategoryDef cat = props.IncidentCategory;
				DeckCategorySettings entry = EnsureSettingsEntry(deckKey, props);

				string catLabel = cat.LabelCap;
				if (catLabel.NullOrEmpty())
				{
					catLabel = cat.defName;
				}
				listing.CheckboxLabeled(
					"DeckStoryteller_UseDeck".Translate(catLabel), ref entry.useDeck);
				listing.Label("DeckStoryteller_ToggleHint".Translate());

				if (entry.useDeck)
				{
					listing.TextFieldNumericLabeled("DeckStoryteller_IntervalDays".Translate(),
						ref entry.intervalDays, ref entry.bufInterval, 1f, 360f);
					listing.TextFieldNumericLabeled("DeckStoryteller_EventsPerInterval".Translate(),
						ref entry.eventsPerInterval, ref entry.bufEvents, 0f, 500f);
					listing.TextFieldNumericLabeled("DeckStoryteller_PossibleEvents".Translate(),
						ref entry.possibleEventsPerInterval, ref entry.bufPossible, 0f, 500f);
					listing.TextFieldNumericLabeled("DeckStoryteller_ChanceOfPossible".Translate(),
						ref entry.chanceOfPossibleEvent, ref entry.bufChance, 0f, 1f);
					listing.TextFieldNumericLabeled("DeckStoryteller_MinSpacingDays".Translate(),
						ref entry.minSpacingDays, ref entry.bufSpacing, 0f, 30f);
					listing.TextFieldNumericLabeled("DeckStoryteller_SafeDays".Translate(),
						ref entry.safeDaysAfterReshuffle, ref entry.bufSafe, 0f, 30f);
					listing.TextFieldNumericLabeled("DeckStoryteller_ReshuffleJitterDays".Translate(),
						ref entry.reshuffleJitterDays, ref entry.bufJitter, 0f, 30f);
					// Scale/points fields only matter for categories whose incidents use threat points.
					if (cat.needsParmsPoints)
					{
						listing.TextFieldNumericLabeled("DeckStoryteller_PointsRandomFactor".Translate(),
							ref entry.pointsRandomFactor, ref entry.bufPointsFactor, 0f, 0.95f);
					}

					float perDay = entry.intervalDays > 0f ? entry.ExpectedEventsPerInterval() / entry.intervalDays : 0f;
					listing.Label("DeckStoryteller_ExpectedRate".Translate(
						entry.ExpectedEventsPerInterval().ToString("F2"),
						entry.intervalDays.ToString("F0"),
						perDay.ToString("F2")));
					if (entry.ExpectedEventsPerInterval() <= 0f)
					{
						GUI.color = Color.yellow;
						listing.Label("DeckStoryteller_EmptyDeckWarning".Translate());
						GUI.color = Color.white;
					}
					else if (entry.safeDaysAfterReshuffle >= entry.intervalDays)
					{
						GUI.color = Color.yellow;
						listing.Label("DeckStoryteller_SafeDaysTooHighWarning".Translate());
						GUI.color = Color.white;
					}
				}
				listing.GapLine();
			}

			measuredListHeight = listing.CurHeight + 8f;
			listing.End();
			Widgets.EndScrollView();

			Listing_Standard footer = new Listing_Standard();
			footer.Begin(footerRect);
			footer.CheckboxLabeled("DeckStoryteller_DoubleRollJitter".Translate(), ref settings.doubleRollJitter);
			footer.CheckboxLabeled("DeckStoryteller_DebugLogging".Translate(), ref settings.debugLogging);
			footer.CheckboxLabeled("DeckStoryteller_ShowDeckStatusAlert".Translate(), ref settings.showDeckStatusAlert);
			if (footer.ButtonText("DeckStoryteller_Reset".Translate()))
			{
				settings.categories.Clear();
				foreach (KeyValuePair<string, StorytellerCompProperties_DeckCycle> pair in registeredDecks.OrderBy((KeyValuePair<string, StorytellerCompProperties_DeckCycle> p) => p.Key))
				{
					settings.categories.Add(DeckCategorySettings.FromProps(pair.Value));
				}
			}
			footer.Label("DeckStoryteller_ResetHint".Translate());
			footer.End();
			// No settings.Write() here: Dialog_ModSettings.PreClose() calls WriteSettings()
			// when the window closes, which is the vanilla convention.
		}
	}
}
