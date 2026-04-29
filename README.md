# Merchant's Mandate: Express Shipyard

An economy-balancing utility mod for **High Seas, High Profits** that introduces a "Buy-on-Demand" system for ship construction. 

## 🛠 Features

* **Express Procurement:** Build any ship even if your warehouse is empty.
* **Economic Balance:** Materials purchased through the shipyard cost **4x the base market rate**, ensuring manual resource gathering remains the most profitable strategy.
* **Integrated UI:** Adds a toggle in the General Settings menu to switch between "Manual" and "Express" modes.
* **Real-time Cost Calculation:** The Shipyard UI dynamically updates to show the total gold required when Express mode is active.

## 🚀 Installation

1.  Ensure [MelonLoader](https://melonwiki.xyz/) is installed.
2.  Download the latest `Utility Pack Shipyard & Economy.dll`.
3.  Place the DLL into your game's `Mods` folder.
4.  Launch the game and navigate to **Settings > General** to enable "Express Shipyard".

## ⚙️ How it Works

The mod hooks into the `TradingSimulationManager` and `ShipyardBuildNewWindow` classes. 
- When **Express Mode** is **ON**:
    - The Build button ignores "Insufficient Goods" errors.
    - All required materials are calculated at 400% of their base value.
    - Gold is deducted directly from the player's balance upon starting the build.
- When **Express Mode** is **OFF**:
    - The game functions with its original vanilla logic.

## 📜 Requirements

* MelonLoader v0.6.1 or higher.
* Unity 2021.3.45f2 (Game Version).
