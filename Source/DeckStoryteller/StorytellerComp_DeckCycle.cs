using System.Collections.Generic;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace DeckStoryteller
{
	/// <summary>
	/// Deck-based replacement for Cassandra's cyclic incident generation.
	///
	/// Every cycle ("interval") the comp pre-generates a queue of event times for its
	/// category: N guaranteed cards plus up to M "possible" cards (each real with a set
	/// chance) are shuffled across the timeline like special cards in a deck of blanks.
	/// The queue is stored in a GameComponent, so it survives save/load and storyteller
	/// switches. Cards are dealt at the storyteller's 1000-tick interval checks; a card
	/// that cannot fire anywhere is burned (like vanilla losing an un-fireable incident).
	///
	/// Unpredictability measures:
	/// - the very first cycle of a deck starts at a random phase, and every cycle length
	///   gets a random ±jitter, so reshuffle dates can never be predicted from the calendar;
	/// - the first cycle drops a random (less than half) portion of its cards.
	/// </summary>
	public class StorytellerComp_DeckCycle : RimWorld.StorytellerComp
	{
		protected StorytellerCompProperties_DeckCycle Props => (StorytellerCompProperties_DeckCycle)props;

		/// <summary>
		/// A due card is normally fired within one 1000-tick storyteller check. Cards that came
		/// due longer ago than this were blocked (deck disabled, storyteller switched away) and
		/// are burned instead of fired, so re-enabling never dumps a burst of back events.
		/// </summary>
		private const int StaleCardGraceTicks = 2000;

		public override IEnumerable<FiringIncident> MakeIntervalIncidents(IIncidentTarget target)
		{
			IncidentCategoryDef cat = Props.IncidentCategory;
			if (cat == null)
			{
				yield break;
			}
			DeckCategorySettings cfg = DeckStorytellerMod.GetEffectiveSettings(cat);
			if (cfg == null || !cfg.useDeck)
			{
				yield break;
			}
			DeckStorytellerGameComp gameComp = DeckStorytellerGameComp.Get();
			if (gameComp == null)
			{
				yield break;
			}

			DeckState deck = gameComp.GetOrCreateDeck(cat);
			int now = Find.TickManager.TicksGame;
			// Game time regressed below our last processing tick. This happens when dev tools
			// simulate the future (StorytellerUtility.DebugGetFutureIncidents) and then restore
			// the game state - our DeckState is not part of that restore. Treat the deck as
			// brand new so it regenerates cleanly instead of running on simulated-forward state.
			if (deck.lastProcessTick > now)
			{
				deck.cycleStartTick = -1;
				deck.firstCycleDone = false;
			}
			// Decks are global per category: process at most once per 1000-tick interval,
			// no matter how many incident targets the storyteller iterates.
			if (deck.lastProcessTick == now)
			{
				yield break;
			}
			deck.lastProcessTick = now;

			EnsureCurrentCycle(deck, cfg, now);

			while (deck.consumedCount < deck.pendingOffsets.Count &&
			       deck.cycleStartTick + deck.pendingOffsets[deck.consumedCount] <= now)
			{
				int dueTick = deck.cycleStartTick + deck.pendingOffsets[deck.consumedCount];
				deck.consumedCount++;
				if (dueTick < now - StaleCardGraceTicks)
				{
					if (DeckStorytellerMod.DebugLogging)
					{
						Log.Message("[DeckStoryteller] " + cat.defName + " card burned (stale) at " +
							DateStringAtTick(now));
					}
					continue;
				}
				// Deal the card: the incident is chosen first, then a random target that can
				// actually accept it (see GenerateIncident) - cards never pile onto Maps[0].
				FiringIncident fi = GenerateIncident(target, cfg, out float pointsMultiplier);
				if (fi != null)
				{
					if (DeckStorytellerMod.DebugLogging)
					{
						string detail = fi.def.defName;
						// Random quests all share the GiveQuest_Random incident def; the actual
						// quest script is what distinguishes them.
						if (fi.parms.questScriptDef != null)
						{
							detail = detail + " (" + fi.parms.questScriptDef.defName + ")";
						}
						if (fi.parms.points > 0f)
						{
							detail = detail + " [" + fi.parms.points.ToString("F0") + " pts x" +
								pointsMultiplier.ToString("F2") + "]";
						}
						Log.Message("[DeckStoryteller] " + cat.defName + " card fired at " +
							DateStringAtTick(now) + ": " + detail + " -> " + fi.parms.target);
					}
					yield return fi;
				}
				else if (DeckStorytellerMod.DebugLogging)
				{
					Log.Message("[DeckStoryteller] " + cat.defName + " card burned (nothing could fire it) at " +
						DateStringAtTick(now));
				}
			}
		}

		private void EnsureCurrentCycle(DeckState deck, DeckCategorySettings cfg, int now)
		{
			if (deck.cycleStartTick < 0)
			{
				GenerateCycle(deck, cfg, now);
				return;
			}
			int guard = 0;
			while (now >= deck.cycleStartTick + deck.cycleDurationTicks)
			{
				if (DeckStorytellerMod.DebugLogging && deck.consumedCount < deck.pendingOffsets.Count)
				{
					Log.Message("[DeckStoryteller] " + Props.IncidentCategory.defName + ": discarding " +
						(deck.pendingOffsets.Count - deck.consumedCount) + " unconsumed card(s) at cycle rollover");
				}
				deck.cycleStartTick += deck.cycleDurationTicks;
				GenerateCycle(deck, cfg, deck.cycleStartTick);
				if (++guard > 1000)
				{
					// Pathological catch-up (e.g. long stint with no eligible targets): hard reset.
					Log.WarningOnce("DeckStoryteller: excessive cycle catch-up for " + Props.IncidentCategory.defName +
						"; deck hard-reset to now.", 73910203 + Props.IncidentCategory.shortHash);
					deck.cycleStartTick = now;
					GenerateCycle(deck, cfg, now);
					break;
				}
			}
		}

		/// <summary>Shuffles a fresh deck: rolls the card count, places the cards on the timeline.</summary>
		private void GenerateCycle(DeckState deck, DeckCategorySettings cfg, int referenceTick)
		{
			// "Brand new" (random phase shift + first-cycle card drop) means this deck has
			// never completed a generation. Invariant: cycleStartTick >= 0 implies firstCycleDone
			// (every exit of this method sets it), so reading the flag here is equivalent to the
			// former call-site parameter.
			bool brandNewDeck = !deck.firstCycleDone;
			int intervalTicks = Mathf.Max(1000, Mathf.RoundToInt(cfg.intervalDays * 60000f));
			// Jitter is capped at half the interval so a cycle can never collapse to the
			// 1000-tick floor (e.g. interval 1 day with a nonsensical 30-day jitter).
			float jitterDays = Mathf.Min(cfg.reshuffleJitterDays, cfg.intervalDays * 0.5f);
			int jitterTicks = Mathf.RoundToInt(Rand.Range(-jitterDays, jitterDays) * 60000f);
			// A brand-new deck starts at a random phase (pulled into the past), so the
			// reshuffle grid is unknown even to a player who knows the settle date.
			int phaseShiftTicks = brandNewDeck ? Rand.Range(0, intervalTicks / 4) : 0;

			deck.cycleStartTick = referenceTick - phaseShiftTicks;
			deck.cycleDurationTicks = Mathf.Max(1000, intervalTicks + jitterTicks);
			deck.pendingOffsets.Clear();
			deck.consumedCount = 0;

			// Roll the card count: guaranteed cards, then possible cards.
			int count = cfg.eventsPerInterval;
			for (int i = 0; i < cfg.possibleEventsPerInterval; i++)
			{
				if (Rand.Chance(cfg.chanceOfPossibleEvent))
				{
					count++;
				}
			}

			// The first cycle drops a random (less than half) portion of its cards.
			int firstCycleConfigured = count;
			if (brandNewDeck && count > 0)
			{
				count = Mathf.Clamp(GenMath.RoundRandom((float)count * Rand.Range(0.5f, 1f)), 1, count);
			}

			// Acceptance curves split by knowability. The days curve is deterministic: it is
			// evaluated per card at its planned day (loop below), preserving ramp shapes
			// (e.g. quests ramping 0->1 over days 8-15) exactly as Cassandra produces them.
			// The threat-points curve depends on future wealth AND on the fire target, neither
			// of which exists at planning time - it is rolled at fire time in GenerateIncident
			// against the actual target, which is also how vanilla behaves in practice.

			// Never place cards in the past: after catch-up (deck disabled for a while),
			// clamping to 'now' spreads the remaining cards over the rest of the cycle
			// instead of dumping them all on the reactivation tick. This also subsumes the
			// brand-new deck's phase shift: now - cycleStartTick >= phaseShiftTicks always.
			int safeTicks = Mathf.RoundToInt(cfg.safeDaysAfterReshuffle * 60000f);
			int minOffset = Mathf.Max(safeTicks,
				Find.TickManager.TicksGame - deck.cycleStartTick);
			// The storyteller checks every 1000 ticks, and EnsureCurrentCycle regenerates the
			// schedule (discarding unconsumed cards) before consumption once the cycle is due
			// to roll over. A card placed in the final 1000 ticks could have its covering check
			// land at/after the rollover and be silently lost - so placement stops one check
			// interval short of the boundary, guaranteeing every card a pre-rollover check.
			int maxOffset = deck.cycleDurationTicks - 1000;
			if (maxOffset <= minOffset)
			{
				// Log only when the configuration alone makes placement impossible. A
				// minOffset pushed past maxOffset by the catch-up clamp is normal operation
				// (a fully-past cycle during re-enable catch-up holds no cards by design).
				if (count > 0 && safeTicks > maxOffset)
				{
					Log.ErrorOnce("DeckStoryteller: no room to place cards for " + Props.IncidentCategory.defName +
						". safeDaysAfterReshuffle is too high for the current interval; this cycle fires nothing.",
						48271301 + Props.IncidentCategory.shortHash);
				}
				deck.firstCycleDone = true;
				return;
			}

			if (count > 0)
			{
				int minSpacing = Mathf.Max(1, Mathf.RoundToInt(cfg.minSpacingDays * 60000f));
				List<int> offsets = new List<int>(count);
				bool spacingOk = false;
				for (int attempt = 0; attempt < 100; attempt++)
				{
					offsets.Clear();
					for (int i = 0; i < count; i++)
					{
						offsets.Add(Rand.Range(minOffset, maxOffset + 1));
					}
					offsets.Sort();
					if (RelaxToSatisfyMinDiff(offsets, minSpacing, maxOffset))
					{
						spacingOk = true;
						break;
					}
				}
				if (!spacingOk)
				{
					// Mirrors vanilla IncidentCycleUtility's error for the same condition;
					// out-of-range cards are simply dropped at the next reshuffle.
					Log.ErrorOnce("DeckStoryteller: too many tries placing cards for " + Props.IncidentCategory.defName +
						". minSpacingDays is too high for the current interval and card count; some cards may be dropped.",
						12612131 + Props.IncidentCategory.shortHash);
				}
				// Per-card acceptance: the days curve is evaluated at each card's actual
				// planned fire day, so ramps keep their shape instead of being applied
				// uniformly. Anchored on cycleStartTick (which can be in the past for a
				// brand-new deck's phase shift or during catch-up), not on 'now'.
				float cycleStartDayFloat = GenDate.DaysPassedSinceSettleFloat -
					(float)(Find.TickManager.TicksGame - deck.cycleStartTick) / 60000f;
				for (int i = offsets.Count - 1; i >= 0; i--)
				{
					float accept = 1f;
					if (Props.acceptFractionByDaysPassedCurve != null)
					{
						float cardDay = cycleStartDayFloat + (float)offsets[i] / 60000f;
						accept *= Props.acceptFractionByDaysPassedCurve.Evaluate(cardDay);
					}
					if (!Rand.Chance(accept))
					{
						offsets.RemoveAt(i);
					}
				}
				deck.pendingOffsets.AddRange(offsets);
				if (DeckStorytellerMod.DebugLogging)
				{
					LogDeckGenerated(deck, count, offsets.Count, brandNewDeck ? firstCycleConfigured : -1);
				}
			}
			else if (DeckStorytellerMod.DebugLogging)
			{
				Log.Message("[DeckStoryteller] " + Props.IncidentCategory.defName + " cycle shuffled empty at " +
					DateStringAtTick(Find.TickManager.TicksGame) + " (" + cfg.eventsPerInterval + " guaranteed + " +
					cfg.possibleEventsPerInterval + " possible at " + cfg.chanceOfPossibleEvent.ToString("P0") +
					", none became real)");
			}
			deck.firstCycleDone = true;
		}

		/// <summary>Dev aid: human-readable dump of a freshly generated cycle (see DebugLogging).</summary>
		private void LogDeckGenerated(DeckState deck, int rolledCount, int placedCount, int firstCycleConfigured)
		{
			StringBuilder sb = new StringBuilder();
			sb.Append("[DeckStoryteller] ").Append(Props.IncidentCategory.defName).Append(" deck shuffled: ")
				.Append(placedCount).Append(" card(s) placed, ").Append(rolledCount).Append(" rolled");
			if (firstCycleConfigured > rolledCount)
			{
				sb.Append(" (first cycle drop: discarded ").Append(firstCycleConfigured - rolledCount)
					.Append(" of ").Append(firstCycleConfigured).Append(")");
			}
			sb.Append(", cycle ").Append((deck.cycleDurationTicks / 60000f).ToString("F1")).Append(" days from ")
				.Append(DateStringAtTick(deck.cycleStartTick));
			int now = Find.TickManager.TicksGame;
			for (int i = 0; i < deck.pendingOffsets.Count; i++)
			{
				int dueTick = deck.cycleStartTick + deck.pendingOffsets[i];
				sb.AppendLine();
				sb.Append("  card ").Append(i + 1).Append(" of ").Append(deck.pendingOffsets.Count)
					.Append(": ").Append(DateStringAtTick(dueTick))
					.Append(" (in ").Append(((float)(dueTick - now) / 60000f).ToString("F1")).Append(" days)");
			}
			Log.Message(sb.ToString());
		}

		/// <summary>Game-calendar date string for a game tick, using the player home map's location.</summary>
		internal static string DateStringAtTick(int gameTick)
		{
			Vector2 longLat = Vector2.zero;
			Map homeMap = Find.AnyPlayerHomeMap;
			if (homeMap != null && homeMap.Tile.Valid)
			{
				longLat = Find.WorldGrid.LongLatOf(homeMap.Tile);
			}
			return GenDate.DateFullStringAt(GenDate.TickGameToAbs(gameTick), longLat);
		}

		/// <summary>Adaptation of vanilla IncidentCycleUtility.RelaxToSatisfyMinDiff.</summary>
		private static bool RelaxToSatisfyMinDiff(List<int> values, int minDiff, int max)
		{
			if (values.Count == 0)
			{
				return true;
			}
			for (int i = 1; i < values.Count; i++)
			{
				if (values[i] - values[i - 1] >= minDiff)
				{
					continue;
				}
				values[i] = values[i - 1] + minDiff;
				for (int j = i + 1; j < values.Count; j++)
				{
					int minPosition = values[j - 1] + minDiff;
					if (values[j] < minPosition)
					{
						values[j] = minPosition;
					}
				}
			}
			return values[values.Count - 1] <= max;
		}

		private FiringIncident GenerateIncident(IIncidentTarget target, DeckCategorySettings cfg, out float pointsMultiplier)
		{
			pointsMultiplier = 1f;
			IncidentCategoryDef cat = Props.IncidentCategory;
			// Selection-time parms. Vanilla OnOffCycle also selects the incident against the
			// target's plain storyteller points; strength scaling is applied only after the
			// fire target is known (below), so nothing about strength is decided early.
			IncidentParms parms = BuildParms(cat, target, cfg, out float selectionMultiplier);

			IncidentDef incidentDef;
			// Note: vanilla OnOffCycle compares integer DaysPassedSinceSettle here; the float
			// comparison is identical for whole-day thresholds (all current XML) and only
			// diverges for fractional ones.
			if (GenDate.DaysPassedSinceSettleFloat < Props.forceRaidEnemyBeforeDaysPassed)
			{
				incidentDef = IncidentDefOf.RaidEnemy;
			}
			else if (Props.incident != null)
			{
				incidentDef = Props.incident;
			}
			else if (!UsableIncidentsInCategory(cat, parms)
				.TryRandomElementByWeight((IncidentDef d) => IncidentChanceFinal(d, target), out incidentDef))
			{
				return null;
			}

			// Deal to a random target that can actually accept the card. Candidates must pass
			// both the comp's target-tag filters and the incident's own targetTags; the first
			// CanFireNow winner in shuffled order is uniform across all fire-able targets, so
			// cards distribute evenly across eligible maps instead of piling onto Maps[0].
			List<IIncidentTarget> candidates = new List<IIncidentTarget>();
			foreach (IIncidentTarget t in AllIncidentTargets())
			{
				if (TargetAllowedByCompTags(t) && incidentDef.TargetAllowed(t))
				{
					candidates.Add(t);
				}
			}
			candidates.Shuffle();
			foreach (IIncidentTarget t in candidates)
			{
				IncidentParms tParms;
				float mult;
				if (ReferenceEquals(t, target))
				{
					tParms = parms;
					mult = selectionMultiplier;
				}
				else
				{
					tParms = BuildParms(cat, t, cfg, out mult);
				}
				if (!incidentDef.Worker.CanFireNow(tParms))
				{
					continue;
				}
				// Wealth-based acceptance, rolled at fire time against the actual fire target's
				// raw threat points (the same input vanilla OnOffCycle feeds this curve; parms.points
				// would double-count the strength jitter). A failed roll burns the card,
				// matching vanilla's hit-list thinning. Do NOT retry the roll on the next candidate:
				// with k fire-able maps the success chance would become 1-(1-p)^k, weakening the
				// decay multiplicatively with map count. One roll keeps the rate at exactly p.
				if (Props.acceptPercentFactorPerThreatPointsCurve != null &&
					!Rand.Chance(Props.acceptPercentFactorPerThreatPointsCurve.Evaluate(
						StorytellerUtility.DefaultThreatPointsNow(t))))
				{
					return null;
				}
				// Fire moment: incident kind and target are both resolved at the instant the
				// card fires; strength randomization was rolled with the parms in BuildParms.
				pointsMultiplier = mult;
				return new FiringIncident(incidentDef, this, tParms);
			}
			return null;
		}

		private IncidentParms BuildParms(IncidentCategoryDef cat, IIncidentTarget target, DeckCategorySettings cfg,
			out float jitterMultiplier)
		{
			IncidentParms parms = StorytellerUtility.DefaultParmsNow(cat, target);
			jitterMultiplier = 1f;
			if (cat == IncidentCategoryDefOf.GiveQuest)
			{
				// Pick the quest script from the raw points, exactly like vanilla RandomQuest -
				// the strength jitter below only scales the final threat, it must not bias
				// which quest gets selected.
				parms.questScriptDef = NaturalRandomQuestChooser.ChooseNaturalRandomQuest(parms.points, target);
			}
			// The single strength randomizer: a continuous uniform multiplier with mean 1,
			// rolled fresh per dealt card (this runs at the fire moment). Like Randy's point
			// randomness but tunable; 0 disables. The global "double roll" setting multiplies
			// in a second independent roll, restoring the more centered (convolved) strength
			// distribution of the old two-factor scaling at the price of higher variance.
			if (parms.points > 0f)
			{
				float jitter = Mathf.Clamp(cfg.pointsRandomFactor, 0f, 0.95f);
				float roll = Rand.Range(1f - jitter, 1f + jitter);
				if (DeckStorytellerMod.DoubleRollJitter)
				{
					roll *= Rand.Range(1f - jitter, 1f + jitter);
				}
				parms.points *= roll;
				jitterMultiplier = roll;
			}
			return parms;
		}

		/// <summary>Reimplementation of the vanilla comp-props target-tag filter.</summary>
		private bool TargetAllowedByCompTags(IIncidentTarget t)
		{
			IEnumerable<IncidentTargetTagDef> tags = t.IncidentTargetTags();
			if (!props.disallowedTargetTags.NullOrEmpty())
			{
				foreach (IncidentTargetTagDef tag in tags)
				{
					if (props.disallowedTargetTags.Contains(tag))
					{
						return false;
					}
				}
			}
			if (!props.allowedTargetTags.NullOrEmpty())
			{
				bool any = false;
				foreach (IncidentTargetTagDef tag in tags)
				{
					if (props.allowedTargetTags.Contains(tag))
					{
						any = true;
						break;
					}
				}
				if (!any)
				{
					return false;
				}
			}
			return true;
		}

		private static IEnumerable<IIncidentTarget> AllIncidentTargets()
		{
			// Mirrors vanilla Storyteller.AllIncidentTargets: every map plus player caravans
			// plus the world. Non-player maps are filtered out later by TargetAllowed/CanFireNow.
			List<Map> maps = Find.Maps;
			for (int i = 0; i < maps.Count; i++)
			{
				yield return maps[i];
			}
			List<Caravan> caravans = Find.WorldObjects.Caravans;
			for (int i = 0; i < caravans.Count; i++)
			{
				if (caravans[i].IsPlayerControlled)
				{
					yield return caravans[i];
				}
			}
			yield return Find.World;
		}

		public override string ToString()
		{
			if (Props.incident == null && Props.IncidentCategory == null)
			{
				return "Deck";
			}
			return "Deck (" + (Props.incident != null ? Props.incident.defName : Props.IncidentCategory.defName) + ")";
		}
	}
}
