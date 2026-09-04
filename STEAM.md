# Steam Workshop description

Paste the text below into the Workshop item's description field when publishing
(the BBCode renders on Steam, but not in-game - `About/About.xml` carries its own
plain-text description).

```
[h3]Deckie Dealer[/h3]
A storyteller that mixes Cassandra's determinism with Randy's unpredictability: every event category gets its own deck of pre-planned event times, reshuffled each cycle - something is always coming, but never exactly when.

A great pick for players who got bored of Randy and have enough experience to read Cassandra - IMHO this storyteller should probably become the game's built-in default.

[h3]What it does[/h3]
[b]The deck system[/b]
[list][*]Each cycle the deck plans the whole interval at once: guaranteed event cards plus a chance of extras are shuffled among blank cards - an attack is guaranteed to arrive within the interval, but its exact day is unpredictable.
[*]The reshuffle schedule can't be learned from the calendar: cycles start at a random phase, every cycle length gets a random jitter, and the first cycle drops a random portion of its cards.
[*]Only the timing is pre-planned. The exact incident, its target and its strength are all decided at the moment the card fires.
[*]Threat strength gets a fresh random multiplier per event (tunable, mean exactly vanilla) - a run of strong attacks is possible, the average stays balanced.
[*]A card that cannot fire anywhere is burned, exactly how vanilla storytellers lose un-fireable incidents.[/list]

[b]Cassandra's pressure, redistributed[/b]
[list][*]Big threats: 1 guaranteed + 2 possible at 65% per 15 days - Cassandra's pressure on Randy's timing.
[*]Small threats: match Cassandra's rate, decaying with colony wealth like hers.
[*]Quests: at Cassandra's Royalty rate (2.5 per 15 days) - more opt-in content alongside the sharper threat spikes.
[*]Orbital traders: half again as often as Cassandra - more trade to compensate the pressure.
[*]Everything else - diseases, faction interactions, caravans, ship chunks, raid beacons, all DLC content - runs Cassandra's own code unchanged.[/list]

[h3]Settings[/h3]
Every deck is individually tunable: interval length, guaranteed and possible events per interval, their chance, minimum spacing between events, safe days after reshuffle, reshuffle date jitter and threat strength jitter. Any category can be switched off entirely, falling back to classic Cassandra-style generation for it. Global options: a double-roll strength distribution, debug logging (deck shuffles and card deals with points and multipliers) and a dev-mode status alert showing every deck's remaining cards and ETAs. Changes apply immediately; the current cycle keeps its already-dealt cards.

[h3]Things to keep in mind[/h3]
[list][*]Decks are global, not per map: one trader per interval in total, dealt to a random eligible map. With several colonies running at once, raise the events-per-interval roughly proportional to the map count.
[*]Quests default to Cassandra's with-Royalty rate; without Royalty she only runs 1.5 per 15 days - lower the quest chance to ~0.25 for that feel.
[*]Cards that come due while a deck is disabled (or while another storyteller is active) are burned, not fired in a burst when you re-enable it.
[*]Clumping across cycle borders is possible and intentional - that is the "unexpected crushing combination" factor. Keep the interval reasonably small (10-20 days) when tuning.[/list]

[h3]Compatibility[/h3]
Requires RimWorld 1.6; all DLCs are optional (defaults match full-DLC Cassandra).
Safe to switch to mid-save: decks live in the save, and switching away and back never dumps a pile of back events on one tick.

Source code and details: [url]https://github.com/Riketta/DeckStoryteller[/url]
```
