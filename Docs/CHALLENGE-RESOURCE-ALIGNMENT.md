# Challenge Resource Alignment

Target challenge: **Build a Junior Astronaut Mission Trainer** (NASA Space Apps Challenge 2026).

Official challenge: https://www.spaceappschallenge.org/2026/challenges/junior-astronaut-mission-trainer/

## What the challenge asks for

An interactive game or app for students to manage a lunar or Martian outpost and experience meaningful trade-offs among life support, radiation shielding, power, and food production. A decision should have consequences over time and help a learner understand why a mission succeeds or fails.

## Current ASTRA alignment

| Challenge element | Current prototype | Gap to close |
|---|---|---|
| Lunar or Martian outpost | Fictional lunar outpost | Clearly label the location and environment as fictional/simplified; connect lessons to real mission context. |
| Life support | Oxygen, water, food, and habitat meters | Existing simplified systems; values remain fictional and labeled. |
| Power | Solar array, power use, greenhouse/shelter costs | Players now spend power on food production and shelter decisions. Values are fictional game units. |
| Radiation shielding | Radiation shelter facility and event decision | Prepare shelter for 8 game power. A solar-particle training event offers shelter (6 power if prepared, 18 if not) or continued outside operations. Outcome uses a fictional exposure index, not real dose. |
| Food production | Multi-step greenhouse crop cycle | Starting a crop uses 8 game water + 8 game power; after 20 real-time game seconds, the player returns to harvest +18 game food. These are intentionally shortened/fictitious balancing values. |
| Data-informed learning | Live NASA DONKI SEP query; CSA food toolkit linked in science panel | DONKI event records from the latest 30 days appear as context. Offline/error state is explicit. No SEP-to-dose conversion. CSA resource anchors the food lesson. |
| Learning for young players | Accessible onboarding, expanded objectives, science/source panel, and debrief | The mission requires shelter preparation, food growth/harvest, and a response. The briefing distinguishes real source data from fictional game mechanics and links directly to DONKI and CSA. |

## Challenge resources to evaluate

The challenge resource list surfaced for this challenge includes:

- [NASA Open APIs](https://api.nasa.gov/) — API catalog, including the DONKI space-weather API.
- [DONKI space-weather database](https://ccmc.gsfc.nasa.gov/tools/DONKI/) — solar flares and coronal mass ejections; a potential source for radiation-risk/communications event context.
- [NASA Open Science Data Repository](https://osdr.nasa.gov/) — space-biology and astronaut-health studies.
- [OSDR RadLab](https://visualization.osdr.nasa.gov/radlab/) — ISS radiation dosimetry data; useful for teaching radiation exposure and shielding concepts, with careful limits on applying ISS measurements to a lunar surface.
- [Mars Trek](https://trek.nasa.gov/mars/) — Mars imagery, elevation, and terrain data if ASTRA adds a Mars scenario.
- [Canadian Space Agency: Space resources for STEM and youth organizations](https://www.asc-csa.gc.ca/eng/youth-educators/resources-stem-and-youth-organizations.asp)
- [Canadian Space Agency: Food production on the Moon and in remote areas](https://www.asc-csa.gc.ca/eng/youth-educators/toolkits/food-production/)

These are candidate resources from the challenge resource tab, not a claim that all are required or already used by ASTRA. The official challenge page governs if its resource tab changes.

## Current source-use disclosure

ASTRA has cited NASA material for lunar South Pole conditions, Shackleton context, Moon Base systems, and lunar gravity in `SCIENCE-SOURCES.md`. The current environment contains no imported NASA terrain, image, model, or challenge dataset. Resource values, event timing, and outcome formulas are fictional game mechanics. No challenge-listed data resource is currently integrated into gameplay.

## Acceptance checklist for resource-aligned learning

- [ ] A learner can explain a life-support trade-off after playing.
- [x] Shelter preparation and shelter-use decisions spend power and change a clearly labeled fictional exposure index.
- [x] The greenhouse activity spends water and power, waits for a crop cycle, then rewards a harvest.
- [x] The science panel queries NASA DONKI SEP records for its latest 30-day date window and labels them as event context rather than lunar dose.
- [x] The greenhouse science panel links to the challenge-listed CSA youth food-production toolkit.
- [x] Real observations, simplified context, and fictional game values are distinguished in the event, science panel, debrief, and source notes.
- [x] The mission debrief connects the shelter and food decisions to the cited resources.

