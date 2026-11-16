# 🚀 ASTRA: Mission Control

> A playable Unity 6.6 URP prototype — junior astronaut mission trainer.

Explore a fictional lunar outpost, manage its resources, complete mission activities, and travel through a campaign from the Moon to Mars and Europa. An offline browser demo is included.

> ⚠️ **For learning only.** ASTRA's procedures, resource values, terrain, and event outcomes are fictional game mechanics. They are **not** real astronaut procedures or operational guidance. See the science notes for sources and limitations.

---

## 🎮 Play in Unity

1. Install **Unity Editor 6000.6.9f1** with [Unity Hub](https://unity.com/download).
2. In Unity Hub, add or open this repository folder — the one containing `Assets`, `Packages`, and `ProjectSettings`.
3. Let Unity finish its first import and script compilation.
4. Open `Assets/_Project/Scenes/ASTRA_Bootstrap.unity` and press **Play**.

> 💡 **Missing bootstrap scene?** Choose `ASTRA → Build First Playable Scene` in the Editor, then open the scene and press Play. The same menu has **Build Windows Player** for creating a Windows build.

> 🧠 **Low memory (≤ 8 GB)?** ASTRA skips optional high-detail NASA GLB models to keep Play Mode within available memory. The built-in scene models remain available for gameplay.

---

## 🌐 Offline Browser Demo

Open `Prototype/index.html` in a browser. The isometric demo runs **offline** — no server or downloads required.

| Option | Action |
|--------|--------|
| 🎓 **Show me how to play** | Opens the optional guide |
| ⏭️ **I know how to play — skip guide** | Starts immediately |

> 🔁 Replay the guide anytime from the **mission briefing**.

---

## 🗺️ Campaign & Activities

The campaign order is:

🌕 Moon First Light → 🌱 Moon Greenhouse → 🔴 Mars Storm Watch → 🌑 Europa Shadow Scout

Complete missions to unlock later destinations. Missions include:

- 🛰️ Outpost resource management
- ☀️ Solar strategy
- 💧 Water and oxygen recovery
- 🌿 Greenhouse crop cycles
- 🚙 Rover driving and sample classification
- 🏗️ Mars shelter and inspection drill
- 🔧 Spacecraft repair and launch activities (connect the destinations)

> 📡 **NASA DONKI data:** The game can use the latest available NASA DONKI solar event record for the Mars drill. When that data is unavailable, it labels the fictional fallback. NASA event data is **context for a learning activity** — not a prediction or astronaut alert.

---

## 🎮 Controls

| Action | Controls |
|--------|----------|
| 🚶 **Move** | `W` `A` `S` `D` |
| 🏃 **Sprint** | `Shift` |
| ⬆️ **Hop** | `Space` |
| 👀 **Look** | Drag with the **right mouse button** |
| 🖐️ **Interact** | `E` |
| ⏸️ **Pause** | `Escape` |
| 🚙 **Rover drive / steer** | `W`/`S` and `A`/`D` |
| 📦 **Collect rover sample** | `E` at the beacon |
| 🚪 **Exit rover** | `Q` |

> 💡 Walk up to a **cyan**, **green**, or **orange** facility beacon to see its interaction prompt.

---

## 📁 Project Layout
