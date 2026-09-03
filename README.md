# Deck Storyteller (Deckie Dealer)

A storyteller for RimWorld 1.6 that mixes Cassandra's determinism with Randy's
unpredictability.

Events are dealt from pre-generated per-category **decks** (event queues). Each cycle, a
number of event "cards" is shuffled among blank cards across the timeline: something is
always coming within the interval, but you never know exactly when - or how strong.
Default tuning matches full-DLC Cassandra's overall pressure while redistributing it:
big threats arrive 1-3 times per cycle with a guaranteed floor, small threats match her
rate, quests match her Royalty rate, and orbital traders arrive half again as often to
compensate the sharper threat spikes. Works with or without DLCs, and can be switched to
mid-save.

## How it works

Each deck-driven incident category has its own deck:

1. **Reshuffle** - the deck plans the whole cycle at once: `eventsPerInterval`
   guaranteed cards plus up to `possibleEventsPerInterval` extra cards, each real with
   `chanceOfPossibleEvent` (rolled once at generation, so the queue is fully fixed).
2. **Placement** - cards land at random times inside the interval, respecting
   `minSpacingDays`. Clumping across cycle borders is possible and intentional.
3. **Dealing** - cards fire at the storyteller's regular 1000-tick checks. A card that
   cannot fire anywhere is burned, exactly how vanilla storytellers lose un-fireable
   incidents.
4. When the cycle ends, the deck reshuffles.

The reshuffle schedule can never be learned from the calendar:

- the first cycle starts at a **random phase**, and every cycle length gets a random
  **±jitter**;
- the first cycle **drops a random (less than half) portion of its cards**;
- optional safe days pad the start of every fresh deck (a guaranteed lull).

**Only *when* is pre-planned.** The exact incident, its target, and its strength are all
resolved at the fire moment - the save file contains timing only, never what is coming.

### Event strength

Threat points are multiplied by a fresh uniform jitter in `1 ± pointsRandomFactor`
(±25% for big threats by default, mean exactly 1.0), rolled independently per event -
like Randy's point randomness, but tunable per category. The global **double-roll**
setting (on by default) multiplies in a second independent roll: a more centered
distribution with somewhat higher variance, and stronger runs of good or bad luck remain
possible while the average stays at the vanilla base. `0` disables the jitter entirely.

## Default decks

Each category can be toggled off in mod settings, falling back to classic
Cassandra-style generation for that category.

| Category       | Default                                        | Cassandra reference |
| -------------- | ---------------------------------------------- | ------------------- |
| Big threats    | 1 guaranteed + 2 possible @ 65% / 15 days      | ~2.1 events / 15 days |
| Small threats  | 2 possible @ 45% / 15 days, decays with wealth | ~0.85 events / 15 days |
| Quests         | 1 + 2 possible @ 75% / 15 days, ramp after day 8 | 1.5 / 15d without Royalty, 2.5 with |
| Orbital traders | 1 + 1 possible @ 50% / 15 days                | 1 / 15 days per map |

Two things worth knowing:

- **Quests** default to Cassandra's *with-Royalty* rate. Without Royalty she only runs
  1.5/15d - if you play without DLCs, lower `chanceOfPossibleEvent` to ~0.25 for the
  same feel.
- **Decks are global**, not per map: one trader per interval in total, dealt to a random
  eligible map. With several colonies running at once, raise `eventsPerInterval`
  roughly proportional to the map count.

Everything else runs exactly as Cassandra: diseases, faction interactions, caravans,
ship chunks, the raid-beacon generator, and all DLC content. The storyteller def
inherits Core's `BaseStoryteller` and reuses the shared comps, so future game updates
are picked up automatically.

## Tuning

- If it feels too easy or too hard, adjust the interval (15 days by default; a
  reasonable window is 10-20 days).
- 1 attack every 15 days and 3 attacks every 45 days are the same average rate, but
  feel fundamentally different: the first is measured, the second carries real
  randomness - and in the worst (nearly impossible) case can produce clusters of up to
  six attacks around a cycle border.
- When raising both interval and event count, watch out for concentration: extreme
  settings can create event-free windows as well as back-to-back stress (e.g. an
  interval of 60 days with 8 threats can put them all around one border - 16 days of
  consecutive attacks). Prefer tuning the number of events per interval and keeping the
  interval small.

Settings apply immediately; the current cycle keeps its already-dealt cards and new
values are used from the next reshuffle. One exception: cards that came due while a deck
was disabled (or while another storyteller was active) are **burned**, not fired in a
burst - re-enabling a deck never dumps a pile of back events on one tick.

## Debugging

Mod settings have two debug toggles:

- **Enable debug logging** - every deck shuffle logs its cycle length and each placed
  card's fire date; every card fire logs the chosen incident, target, final threat
  points and the jitter multiplier (e.g. `RaidEnemy [845 pts x1.13]`). Burned cards are
  logged too. Output goes to the normal game log, prefixed `[DeckStoryteller]`.
- **(Debug) Show deck status alert** - requires **dev mode** (options > developer
  mode). A passive alert (like "minor break risk") shows in the alert stack: its label
  shows the soonest upcoming card across all decks, its tooltip lists every deck's
  state - remaining cards with their ETAs and the next reshuffle. Pure readout, no
  bell sound.

## Technical notes

The storyteller def (`Defs/Storytellers_Dealer.xml`) is assembled from three kinds of
comps:

- **Deck comps** (`StorytellerCompProperties_DeckCycle` → `StorytellerComp_DeckCycle`) -
  one per deck-driven category.
- **Fallback comps** (`StorytellerCompProperties_Fallback*`) - verbatim Cassandra comps
  that run only while the category's deck is disabled in settings.
- **Verbatim Cassandra comps** for everything else. The def inherits Core's
  `BaseStoryteller`, so DLC and endgame comps come from the shared base and follow game
  updates automatically.

State lives in a `GameComponent` (`DeckStorytellerGameComp`): one `DeckState` per
`IncidentCategoryDef`, holding timing only - `cycleStartTick`, `cycleDurationTicks`,
sorted `pendingOffsets` and `consumedCount`. It survives save/load and storyteller
switches; states whose deck comps no longer exist are pruned on load.

Per category, once per 1000-tick storyteller interval:

1. `MakeIntervalIncidents` runs a `lastProcessTick` guard so the global deck is
   processed once no matter how many incident targets the pipeline iterates.
2. `EnsureCurrentCycle` shuffles a new cycle when due (interval length + reshuffle
   jitter), placing cards with min-spacing relaxation; a fresh deck's first cycle drops
   a random (< half) portion of its cards. If game time ever regresses (e.g. the dev
   "view future incidents" simulation), the deck resets itself.
3. Due cards are dealt: the incident is picked with the vanilla chooser, then a random
   target that passes both the comp's target-tag filters and the incident's
   `targetTags`/`CanFireNow`. If none accepts, the card is burned. Wealth-based
   acceptance rolls at fire time against the actual target's threat points; a failed
   roll burns the card rather than retrying (retrying would scale the acceptance rate
   with map count).
4. Strength jitter is applied in `BuildParms` (uniform `1 ± pointsRandomFactor`,
   squared when the global double-roll setting is on) and reported for logging.

Cards that came due longer than a grace interval ago (deck was disabled, another
storyteller was active) are burned instead of fired as a burst.

`DeckStorytellerMod.RegisterDeck` runs at def-resolve time and seeds a persistent
`DeckCategorySettings` from the XML defaults - XML numbers are defaults only, the
settings entry is what the comp actually reads. The status alert (`Alert_DeckStatus`)
is auto-registered by `AlertsReadout` reflecting over all `Alert` subclasses, gated on
dev mode + its setting + a deck comp present in the current storyteller def.

### Adding decks to other storytellers

Any mod or XML patch can add a `StorytellerCompProperties_DeckCycle` comp to any
storyteller def. It automatically gets a settings entry seeded from the XML defaults;
pair it with a `StorytellerCompProperties_Fallback*` comp for a toggleable fallback.
Deck state is keyed by incident category - two deck comps resolving to the same
category would share one deck, so don't define duplicates.

## Building from source

Requires the .NET SDK. The project targets `netstandard2.1` and references the game's
managed assemblies from the install path (default `E:\SteamLibrary\steamapps\common\RimWorld`).
Build the Release configuration for the dll you ship - a plain `dotnet build` defaults
to Debug:

```
cd Source/DeckStoryteller
dotnet build -c Release -p:RimWorldDir="C:\Path\To\RimWorld"
```

Output lands in `Assemblies/DeckStoryteller.dll`. The compiled mod is the whole mod
folder (`About/`, `Defs/`, `Languages/`, `Source/`, `Textures/`, `Assemblies/`) - copy
or symlink it into the game's `Mods` directory to install.

## Known limitations

- `Misc` home events and faction interactions are not deck-driven - their MTB-style
  generation already works well, and they can be added via XML as described above.
- Dev tools that simulate the future (e.g. "view future incidents") run the decks
  forward for real; when time is restored, affected decks are regenerated from scratch
  (self-healing, but the shown schedule is not preserved).
