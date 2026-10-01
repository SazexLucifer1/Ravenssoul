# Soul energy (planned)

`GameSession.soul_energy_spent` already exists and is saved. Spending goes
through a single function here (to be added) that increments the tally and
emits a signal. Story branching in the first build is one threshold check
(`soul_energy_spent >= CORRUPTION_THRESHOLD`) evaluated at story beats; no
quest trees yet. The threshold lives in a Resource so design can tune it.
