using System.Collections.Generic;
using RimWorld;
using Verse;

namespace DeckStoryteller
{
	/// <summary>
	/// Classic Cassandra-style OnOffCycle generation, used as fallback when the deck for
	/// this comp's category is disabled in mod settings. When the deck is enabled, this
	/// comp yields nothing and the deck comp handles the category.
	/// </summary>
	public class StorytellerComp_FallbackOnOffCycle : StorytellerComp_OnOffCycle
	{
		public override IEnumerable<FiringIncident> MakeIntervalIncidents(IIncidentTarget target)
		{
			IncidentCategoryDef cat = ((StorytellerCompProperties_OnOffCycle)props).IncidentCategory;
			if (DeckStorytellerMod.IsDeckEnabled(cat))
			{
				yield break;
			}
			foreach (FiringIncident fi in base.MakeIntervalIncidents(target))
			{
				yield return fi;
			}
		}
	}

	public class StorytellerCompProperties_FallbackOnOffCycle : StorytellerCompProperties_OnOffCycle
	{
		public StorytellerCompProperties_FallbackOnOffCycle()
		{
			compClass = typeof(StorytellerComp_FallbackOnOffCycle);
		}
	}

	/// <summary>Same fallback gating, for random quest generation.</summary>
	public class StorytellerComp_FallbackRandomQuest : StorytellerComp_RandomQuest
	{
		public override IEnumerable<FiringIncident> MakeIntervalIncidents(IIncidentTarget target)
		{
			IncidentCategoryDef cat = ((StorytellerCompProperties_OnOffCycle)props).IncidentCategory;
			if (DeckStorytellerMod.IsDeckEnabled(cat))
			{
				yield break;
			}
			foreach (FiringIncident fi in base.MakeIntervalIncidents(target))
			{
				yield return fi;
			}
		}
	}

	public class StorytellerCompProperties_FallbackRandomQuest : StorytellerCompProperties_RandomQuest
	{
		public StorytellerCompProperties_FallbackRandomQuest()
		{
			compClass = typeof(StorytellerComp_FallbackRandomQuest);
		}
	}
}
