using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace DeckStoryteller
{
	/// <summary>
	/// Passive status alert (the alert stack on the right screen edge) for deck debugging
	/// and tuning. The label shows the soonest upcoming card across all decks; the tooltip
	/// lists every deck's state: remaining cards with their ETAs and the next reshuffle.
	///
	/// Only active while dev mode (options > developer mode) is on, the current
	/// storyteller def actually runs deck comps and the mod setting is on - this is a
	/// debugging tool, not a gameplay alert. Auto-registered by AlertsReadout,
	/// which instantiates every Alert leaf subclass found in loaded assemblies.
	///
	/// Medium priority is the lowest vanilla priority and does not play the alert bell.
	/// </summary>
	public class Alert_DeckStatus : Alert
	{
		/// <summary>At most this many remaining card ETAs are listed per deck line.</summary>
		private const int MaxShownCardsPerDeck = 4;

		public Alert_DeckStatus()
		{
			defaultPriority = AlertPriority.Medium;
		}

		private static bool ShouldShow
		{
			get
			{
				if (!Prefs.DevMode || !DeckStorytellerMod.ShowDeckStatusAlert || Current.ProgramState != ProgramState.Playing)
				{
					return false;
				}
				// Runtime-derived instead of comparing storyteller defNames: any def that
				// runs deck comps (ours or a third-party pairing) gets the readout.
				List<StorytellerCompProperties> comps = Find.Storyteller?.def?.comps;
				if (comps == null)
				{
					return false;
				}
				for (int i = 0; i < comps.Count; i++)
				{
					if (comps[i] is StorytellerCompProperties_DeckCycle)
					{
						return true;
					}
				}
				return false;
			}
		}

		public override AlertReport GetReport()
		{
			return ShouldShow ? AlertReport.Active : AlertReport.Inactive;
		}

		public override string GetLabel()
		{
			if (!ShouldShow)
			{
				return "DeckStoryteller_AlertLabelIdle".Translate();
			}
			DeckStorytellerGameComp gameComp = DeckStorytellerGameComp.Get();
			int now = Find.TickManager.TicksGame;
			int soonestCard = int.MaxValue;
			int soonestReshuffle = int.MaxValue;
			foreach (KeyValuePair<string, StorytellerCompProperties_DeckCycle> pair in DeckStorytellerMod.RegisteredDecks)
			{
				IncidentCategoryDef cat = pair.Value.IncidentCategory;
				if (cat == null || !DeckStorytellerMod.IsDeckEnabled(cat))
				{
					continue;
				}
				DeckState deck = gameComp?.GetDeckOrNull(cat);
				if (deck == null || deck.cycleStartTick < 0)
				{
					continue;
				}
				if (deck.consumedCount < deck.pendingOffsets.Count)
				{
					soonestCard = Mathf.Min(soonestCard, deck.cycleStartTick + deck.pendingOffsets[deck.consumedCount]);
				}
				soonestReshuffle = Mathf.Min(soonestReshuffle, deck.cycleStartTick + deck.cycleDurationTicks);
			}
			if (soonestCard != int.MaxValue)
			{
				return "DeckStoryteller_AlertLabel".Translate(TimeUntilString(soonestCard - now));
			}
			if (soonestReshuffle != int.MaxValue)
			{
				return "DeckStoryteller_AlertLabelReshuffle".Translate(TimeUntilString(soonestReshuffle - now));
			}
			return "DeckStoryteller_AlertLabelIdle".Translate();
		}

		public override TaggedString GetExplanation()
		{
			StringBuilder sb = new StringBuilder();
			// Vanilla convention: dates/times in DateTimeColor, secondary text in gray,
			// imminent events in RedReadable. Keeps the values readable at a glance.
			sb.Append("DeckStoryteller_AlertHeader".Translate().Colorize(ColoredText.SubtleGrayColor));
			DeckStorytellerGameComp gameComp = DeckStorytellerGameComp.Get();
			int now = Find.TickManager.TicksGame;
			foreach (KeyValuePair<string, StorytellerCompProperties_DeckCycle> pair in
			         DeckStorytellerMod.RegisteredDecks.OrderBy((KeyValuePair<string, StorytellerCompProperties_DeckCycle> p) => p.Key))
			{
				IncidentCategoryDef cat = pair.Value.IncidentCategory;
				if (cat == null)
				{
					continue;
				}
				sb.AppendLine();
				sb.Append(CategoryLabel(cat));
				if (!DeckStorytellerMod.IsDeckEnabled(cat))
				{
					sb.Append(": ").Append("DeckStoryteller_AlertDeckOff".Translate().Colorize(ColoredText.SubtleGrayColor));
					continue;
				}
				DeckState deck = gameComp?.GetDeckOrNull(cat);
				if (deck == null || deck.cycleStartTick < 0)
				{
					sb.Append(": ").Append("DeckStoryteller_AlertDeckWaiting".Translate().Colorize(ColoredText.SubtleGrayColor));
					continue;
				}
				int reshuffleTick = deck.cycleStartTick + deck.cycleDurationTicks;
				int cardsLeft = deck.pendingOffsets.Count - deck.consumedCount;
				if (cardsLeft > 0)
				{
					StringBuilder times = new StringBuilder();
					int shown = 0;
					for (int i = deck.consumedCount; i < deck.pendingOffsets.Count && shown < MaxShownCardsPerDeck; i++, shown++)
					{
						if (shown > 0)
						{
							times.Append(", ");
						}
						times.Append(TimeColored(deck.cycleStartTick + deck.pendingOffsets[i] - now));
					}
					if (cardsLeft > shown)
					{
						times.Append(" (+").Append(cardsLeft - shown).Append(")");
					}
					sb.Append(": ").AppendTagged("DeckStoryteller_AlertDeckCards".Translate(
						cardsLeft, times.ToString(),
						TimeColored(reshuffleTick - now), DateColored(reshuffleTick)));
				}
				else
				{
					sb.Append(": ").AppendTagged("DeckStoryteller_AlertDeckEmpty".Translate(
						TimeColored(reshuffleTick - now), DateColored(reshuffleTick)));
				}
			}
			return sb.ToString();
		}

		private static string CategoryLabel(IncidentCategoryDef cat)
		{
			string label = cat.LabelCap;
			return label.NullOrEmpty() ? cat.defName : label;
		}

		/// <summary>Compact duration string: hours below one day, days otherwise.</summary>
		private static string TimeUntilString(int ticks)
		{
			if (ticks <= 0)
			{
				return "now";
			}
			float days = ticks / 60000f;
			if (days >= 1f)
			{
				return days.ToString("F1") + "d";
			}
			return Mathf.CeilToInt(ticks / 2500f) + "h";
		}

		/// <summary>Duration until a tick, in the vanilla date/time highlight color;
		/// red when due immediately.</summary>
		private static string TimeColored(int ticks)
		{
			if (ticks <= 0)
			{
				return "now".Colorize(ColorLibrary.RedReadable);
			}
			return TimeUntilString(ticks).Colorize(ColoredText.DateTimeColor);
		}

		/// <summary>Game-calendar date in the vanilla date/time highlight color.</summary>
		private static string DateColored(int tick)
		{
			return StorytellerComp_DeckCycle.DateStringAtTick(tick).Colorize(ColoredText.DateTimeColor);
		}
	}
}
