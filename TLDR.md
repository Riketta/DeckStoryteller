# Deckie Dealer

A storyteller that mixes Cassandra's determinism with Randy's unpredictability: every event category gets its own deck of pre-planned event times, reshuffled each cycle - something is always coming, but never exactly when.

A great pick for players who got bored of Randy and have enough experience to read Cassandra - IMHO this storyteller should probably become the game's built-in default.

## The deck system

- Each cycle the deck plans the whole interval at once: guaranteed event cards plus a chance of extras are shuffled among blank cards - an attack is guaranteed to arrive within the interval, but its exact day is unpredictable.
- The reshuffle schedule can't be learned from the calendar: cycles start at a random phase, every cycle length gets a random jitter, and the first cycle drops a random portion of its cards.
- Only the timing is pre-planned; the exact incident, its target and its strength are decided at the moment the card fires. Threat strength gets a fresh random multiplier per event (tunable, mean exactly vanilla).
- Big threats: 1 guaranteed + 2 possible at 65% per 15 days - Cassandra's pressure on Randy's timing. Small threats match Cassandra's rate, quests and orbital traders run slightly more often, and everything else uses Cassandra's own code unchanged.
- Every deck is individually tunable, and any category can be switched off entirely, falling back to classic Cassandra-style generation. Safe to switch to mid-save: decks live in the save, and switching away and back never dumps a pile of back events on one tick.

Steam Workshop: [Deckie Dealer](https://steamcommunity.com/sharedfiles/filedetails/?id=3794755986).
