# Deck Storyteller (Deckie Dealer)

A RimWorld 1.6 mod adding a new storyteller: **Deckie Dealer** — a mixture of
Cassandra's determinism and Randy's unpredictability.

Events are dealt from pre-generated per-category **decks** (event queues). Every cycle,
a number of event "cards" is shuffled among blank cards across the timeline: something
is always coming within the cycle, but you never know exactly when - or how many.
Default tuning matches full-DLC Cassandra's threat
pressure while redistributing it: big threats swing between 1 and 3 per cycle with a
guaranteed floor and minor threats match her rate. Quests match her Royalty rate and
orbital traders arrive half again as often as hers - more trade and more opt-in quest
offers to compensate the sharper threat spikes.

## The deck model

Each deck-driven incident category has its own deck. A deck cycle works like this:

1. At cycle start (a "reshuffle"), the deck generates a queue of event times for the
   whole interval: `eventsPerInterval` guaranteed cards, plus up to
   `possibleEventsPerInterval` extra cards that are each real with
   `chanceOfPossibleEvent` (rolled at generation time, so the queue is fully
   pre-planned).
2. Cards are placed at uniformly random times inside the interval, respecting
   `minSpacingDays` between cards of the same cycle. Clumping across cycle borders is
   possible and intentional — that is the "unexpected crushing combination" factor.
3. Cards fire at the storyteller's regular 1000-tick interval checks. A card that cannot
   fire anywhere (no valid target, incident conditions unmet) is burned — exactly how
   vanilla storytellers lose un-fireable incidents.
4. When the cycle ends, the deck reshuffles.

Unpredictability measures (so the reshuffle grid can never be learned from the calendar):

- the first cycle of a deck starts at a **random phase**, and every cycle length gets a
  random **±jitter** (`reshuffleJitterDays`);
- the first cycle **drops a random (less than half) portion of its cards**;
- optional `safeDaysAfterReshuffle` pads blank cards at the start of every fresh deck
  (a guaranteed lull after a reshuffle).

**The deck pre-plans only *when* cards fire and how many a cycle holds.** The exact
incident, its target, and its strength are all resolved at the fire moment — the save
file contains timing only, never what is coming.

### Mapping to the original `Deck<T>` sketch

| Sketch                          | This implementation                                    |
| ------------------------------- | ------------------------------------------------------ |
| `DeckSize`                      | `intervalDays` (slots are the 1000-tick draws)         |
| `NumberOfDecks`                 | `eventsPerInterval` scaling (3 decks = 3x cards, 3x interval length keeps the same rate) |
| `ExtraSafeCards`                | `safeDaysAfterReshuffle`                               |
| `CurrentCard`                   | `consumedCount` / `cycleStartTick`                     |
| `specialCardIndexes`            | `pendingOffsets` (sorted tick offsets)                 |
| `SpecialCards`                  | resolved at deal time: weighted pick inside the category (same picker Cassandra uses) |

Time-based queues were chosen over literal card lists because the storyteller ticks in
1000-tick intervals — the queue is the natural representation, and it persists cleanly
in savegames.

### Raid strength

Event strength is decided entirely at the fire moment - never pre-planned, never
persisted. When a card fires, its threat points are multiplied by a single continuous
random jitter: a uniform value in `1 ± pointsRandomFactor` (default ±25% for big
threats, mean exactly 1.0), like Randy's point randomness but tunable per category.
Draws are independent: a run of strong attacks is possible, and over many events the
mean stays at the vanilla base. `0` disables the jitter entirely.

The global **double-roll strength jitter** setting (on by default) multiplies in a
second independent jitter roll, reproducing the more centered (convolved) strength
distribution of the mod's earlier two-factor scaling - mid-range results become more
likely, extremes slightly less, and total variance roughly doubles. At the shipped
±25% the double roll yields a standard deviation of ~0.20, nearly identical to the old
two-factor system's ~0.23 (single roll: ~0.14). To widen the spread instead, raise the
per-category jitter.

## Categories & fallbacks

Four categories are deck-driven by default (each can be toggled off in mod settings,
falling back to classic Cassandra-style generation for that category):

| Category        | Deck default                                   | Cassandra reference        |
| --------------- | ---------------------------------------------- | -------------------------- |
| ThreatBig       | 1 + 2 possible @ 65% / 15 days (12%/46%/42% for 1/2/3), ±25% points jitter | ~2.1 events / 15 days |
| ThreatSmall     | 2 possible cards / 15 days @ 45%, wealth decay curve    | ~0.85 events / 15 days (decaying) |
| GiveQuest       | 1 + 2 possible @ 75% / 15 days (= 2.5, Royalty parity; 6%/38%/56% for 1/2/3), day 8-15 ramp | 1.5/15d without Royalty, 2.5/15d with |
| OrbitalVisitor  | 1 + 1 possible @ 50% / 15 days (~1.5, one per ~10 days), random eligible map | 1 / 15 days per map       |

Note on quests: the deck runs the same rate regardless of DLC. Cassandra's two quest
comps are mutually exclusive - without Royalty she runs 1 per 10 days (1.5/15d), with
Royalty 2 per 12 days (2.5/15d). The deck's 2.5/15d default matches her Royalty rate
exactly; for a non-Royalty feel, lower `chanceOfPossibleEvent` to ~0.25 (1 + 2 @ 0.25
= 1.5/15d). Repeat-quest spam is handled by the chooser's own recent-quest
downweighting, same as vanilla.

Note on traders (and multi-map in general): decks are global per category, so the deck
fires one trader per 15 days total, dealt to a random tag-eligible map - not one per
map like Cassandra's per-map cycles. With several colonies running simultaneously,
raise `eventsPerInterval` roughly proportional to the map count if you want per-map
parity.

Everything else runs exactly as Cassandra: diseases, faction interactions, caravans
threats, world misc, ship chunks, the raid-beacon generator, plus everything inherited
from Core's `BaseStoryteller` (endgame quests, DLC content like Royalty intros,
Ideology relics, Biotech mechlink, Anomaly monolith, Odyssey quests...). Because the
storyteller def inherits `BaseStoryteller` and loads category comps at runtime, future
game/DLC updates to the shared base are picked up automatically.

## Tuning guide

- If it feels too easy or too hard, adjust the interval (15 days by default; a
  reasonable window is 10-20 days).
- 1 attack every 15 days and 3 attacks every 45 days are the same average rate, but in
  practice they feel fundamentally different: the first is measured and predictable, the
  second carries real randomness — those 3 events may be spread evenly or arrive in
  succession. Worst case (nearly impossible), the second yields clusters of up to six
  attacks around a cycle border, while the first yields at most two in a row.
- When increasing both the interval and the number of events, watch out for event
  concentration: there can be boring event-free windows as well as periods of extreme
  stress. Prefer regulating dynamics via the number of events per interval while keeping
  the interval reasonably small (10-20 days). Example: interval 60 days with 8 threats
  can, in the worst case, put all events in the last days of one cycle and the first
  days of the next — 16 days of back-to-back attacks. Extremely unlikely, but keep it in
  mind.
- The default threat settings trade between Cassandra and Randy: high attack regularity
  combined with high attack dynamics.

Settings changes apply immediately; the current cycle keeps its already-dealt cards, and
new values are used from the next reshuffle. One exception: cards that came due while
the deck was disabled (or while another storyteller was active) are **burned**, not
fired in a burst — re-enabling a deck never dumps a pile of back events on one tick.

## Debugging

The mod settings have an **Enable debug logging** toggle. When on, every deck shuffle
logs its cycle length, start date, and each placed card's fire date with days-until;
every card fire logs the chosen incident, target, final threat points and the jitter
multiplier that produced them (e.g. `RaidEnemy [845 pts x1.13]` = base points x 1.13);
stale-burned, un-fireable and rollover-discarded cards are logged too. Output goes to
the normal game log (dev-mode console), prefixed `[DeckStoryteller]`.

The settings also have a **(Debug) Show deck status alert** toggle. It only does
anything with **dev mode enabled** (options > developer mode) and while a deck-driven
storyteller is active: a passive alert
(like "minor break risk") sits in the right-edge alert stack while a deck-driven
storyteller is active: its label shows the soonest upcoming card across all decks, and
its tooltip lists every deck's state — whether it runs the deck or the Cassandra
fallback, the remaining cards with their ETAs (what fires is not decided yet, only
when), and when the deck reshuffles. No bell sound; it is pure readout.

## Files

```
About/                 mod metadata + Preview.png
Textures/DeckieDealer.png, DeckieDealerTiny.png   storyteller portraits (large + tiny)
Defs/Storytellers_Dealer.xml   the storyteller def (deck comps + Cassandra fallbacks + verbatim comps)
Languages/English/     translations for the settings UI
Source/DeckStoryteller  C# source + csproj
Assemblies/            compiled DeckStoryteller.dll
```

## Building

Requires the .NET SDK. The csproj defaults to
`E:\SteamLibrary\steamapps\common\RimWorld`; override with your install path:

```
cd Source/DeckStoryteller
dotnet build -p:RimWorldDir="C:\Path\To\RimWorld"
```

Output lands in `Assemblies/DeckStoryteller.dll`. The whole `_Mods` folder can be
symlinked/copied into the game's `Mods` directory.

## Technical notes

- **Persistence**: deck queues live in a `GameComponent` (one `DeckState` per incident
  category), so they survive save/load and even switching away from the storyteller and
  back. Un-fireable cards are burned rather than queued forever.
- **Multi-map**: decks are global per category, not per map. When a card is due, the
  incident is chosen first, then dealt to a **random target that can actually accept
  it** (candidates must pass both the comp's target-tag filters and the incident's own
  target tags; the pick is uniform across fire-able targets). If nothing can accept the
  card, it is burned like any un-fireable vanilla incident. Wealth-based acceptance
  (the small-threat decay) is also rolled at fire time against the actual fire target's
  threat points - the same input and timing vanilla uses - so moving bases or keeping
  old maps around cannot skew it. Only the day-based ramps are fixed at planning time,
  which is exact since days are deterministic. Vanilla Cassandra instead runs a
  full independent cycle per target; the ambient caravan/temp-map threat comps are kept
  verbatim so caravans don't lose coverage. One consequence of the global design:
  *which* incident a card becomes is selected using the first tag-eligible target's
  story state (recent-incident weighting, population factors - normally Maps[0]),
  even though the card then fires at a random eligible map; with several maps the
  selection weighting reflects one arbitrary map while all fire-time records are
  per-target and exact.
- **Anomaly / difficulty interplay**: big-threat suppression (difficulty setting,
  metal-hell cooldown) and the anomaly incident split are handled by the vanilla
  storyteller pipeline this comp plugs into.
- **Adding more decks**: any mod or XML patch can add a
  `DeckStoryteller.StorytellerCompProperties_DeckCycle` comp to any storyteller def; it
  automatically gets a settings entry seeded from the XML defaults. Pair it with a
  `StorytellerCompProperties_Fallback*` comp if you want a toggleable fallback. Deck
  state is keyed by incident category, so **two deck comps resolving to the same
  category share one deck and one settings entry** — don't define duplicates. Also note
  the deck props do not mirror every vanilla `OnOffCycle` field:
  `onDaysNoTreeConnectors`/`offDaysNoTreeConnectors` and
  `acceptPercentFactorPerProgressScoreCurve` have no deck equivalent (none of the four
  decked categories uses them in Cassandra; patch the XML or extend the props class if
  you need them).

## Known limitations

- Custom portrait art (`Textures/DeckieDealer.png`, padded to the vanilla 580:620 portrait
  aspect) and mod preview (`About/Preview.png`) shipped with the mod.
- `Misc` home events and faction interactions are not deck-driven (they use MTB-style
  generation already; decking them adds little). They can be added via XML as described
  above.
- Dev tools that simulate the future (e.g. the debug "view future incidents" utility)
  run the deck forward for real; when game time is restored, the mod detects the
  regression and regenerates the affected decks from scratch. This is self-healing, but
  it means a freshly randomized schedule after using such tools - not a continuation of
  what the simulation showed.
