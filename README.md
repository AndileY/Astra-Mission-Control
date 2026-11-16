# ASTRA: Mission Control

ASTRA is a playable Unity 6.6 URP prototype for a junior astronaut mission trainer. Explore a fictional lunar outpost, manage its resources, complete mission activities, and travel through a campaign from the Moon to Mars and Europa. An offline browser demo is included.

> **For learning only.** ASTRA’s procedures, resource values, terrain, and event outcomes are fictional game mechanics. They are not real astronaut procedures or operational guidance. See [the science notes](Docs/SCIENCE-SOURCES.md) for sources and limitations.

## Play in Unity

1. Install Unity Editor **6000.6.9f1** with Unity Hub.
2. In Unity Hub, add or open this repository folder—the one containing `Assets`, `Packages`, and `ProjectSettings`.
3. Let Unity finish its first import and script compilation.
4. Open `Assets/_Project/Scenes/ASTRA_Bootstrap.unity` and press **Play**.

If the bootstrap scene is missing, choose **ASTRA → Build First Playable Scene** in the Editor, then open the scene and press **Play**. The same menu has **Build Windows Player** for creating a Windows build.

On Editor installations with **8 GB of memory or less**, ASTRA skips optional high detail NASA GLB models to keep Play Mode within the available memory. The built in scene models remain available for gameplay.

## Offline browser demo

Open [`Prototype/index.html`](Prototype/index.html) in a browser. The isometric demo runs offline without a server or downloads. At the welcome screen, choose **Show me how to play** for the optional guide or **I know how to play — skip guide** to start. Replay the guide from the mission briefing.

## Campaign and activities

The campaign order is **Moon First Light → Moon Greenhouse → Mars Storm Watch → Europa Shadow Scout**. Complete missions to unlock later destinations. Missions include outpost resource management, solar strategy, water and oxygen recovery, greenhouse crop cycles, rover driving and sample classification, and a Mars shelter and inspection drill. Spacecraft repair and launch activities connect the destinations.

The game can use the latest available NASA DONKI solar event record for the Mars drill; when that data is unavailable, it labels the fictional fallback. NASA event data is context for a learning activity, not a prediction or astronaut alert.

## Controls

| Action | Controls |
| --- | --- |
| Move | WASD |
| Sprint | Shift |
| Hop | Space |
| Look | Drag with the right mouse button |
| Interact | E |
| Pause | Escape |
| Rover drive / steer | W/S and A/D |
| Collect rover sample | E at the beacon |
| Exit rover | Q |

Walk up to a cyan, green, or orange facility beacon to see its interaction prompt.

## Project layout

- `Assets/_Project/Scripts/` — mission systems, player controls, UI, and world interactions
- `Assets/_Project/Scenes/ASTRA_Bootstrap.unity` — first playable Unity scene
- `Assets/Editor/AstraSceneBuilder.cs` — scene generation and build commands
- `Assets/StreamingAssets/NASA/` — NASA model files used by the runtime loader
- `Prototype/` — offline browser demo
- `Docs/SCIENCE-SOURCES.md` — science references and simulation limits

## Attribution and license

NASA model and science attributions are included with the project assets and in the science notes. ASTRA is an independent educational prototype; it is not endorsed by NASA. No software license is currently specified in this repository, so public availability alone does not grant reuse rights.
