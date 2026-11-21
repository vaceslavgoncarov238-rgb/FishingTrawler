using FishingTrawler.GameLocations;
using FishingTrawler.Messages;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using System.Collections.Generic;
using System.Linq;
using xTile.Dimensions;

namespace FishingTrawler.Framework.Managers
{
    public class EventManager
    {
        private IMonitor _monitor;

        // Intervals
        private uint _fishUpdateInterval = 300;
        private uint _floodUpdateInterval = 180;
        private uint _fuelUpdateInterval = 300;
        private uint _hullEventInterval = 600;
        private uint _netEventInterval = 600;

        // Increment values
        private int _timerIncrement = -1000;
        private int _computerCooldownIncrement = -1000;

        // Split screen data
        private readonly PerScreen<int> _fishingTripTimer = new PerScreen<int>();

        public EventManager(IMonitor monitor)
        {
            _monitor = monitor;
        }

        private KeyValuePair<string, int> GetMessageEverythingFailing() => new KeyValuePair<string, int>(FishingTrawler.i18n.Get("status_message.ship_falling_apart"), 10);
        private KeyValuePair<string, int> GetMessageLosingFish() => new KeyValuePair<string, int>(FishingTrawler.i18n.Get("status_message.losing_fish"), 9);
        private KeyValuePair<string, int> GetMessageMaxLeaks() => new KeyValuePair<string, int>(FishingTrawler.i18n.Get("status_message.taking_on_water"), 8);
        private KeyValuePair<string, int> GetMessageMultiProblems() => new KeyValuePair<string, int>(FishingTrawler.i18n.Get("status_message.lots_of_problems"), 7);
        private KeyValuePair<string, int> GetMessageEngineProblem() => new KeyValuePair<string, int>(FishingTrawler.i18n.Get("status_message.engine_failing"), 7);
        private KeyValuePair<string, int> GetMessageNetProblem() => new KeyValuePair<string, int>(FishingTrawler.i18n.Get("status_message.nets_torn"), 6);
        private KeyValuePair<string, int> GetMessageLeakProblem() => new KeyValuePair<string, int>(FishingTrawler.i18n.Get("status_message.leak"), 5);

        internal void UpdateEvents(UpdateTickingEventArgs e, TrawlerCabin trawlerCabin, TrawlerSurface trawlerSurface, TrawlerHull trawlerHull)
        {
            // Every second, update the required timers
            if (e.IsOneSecond)
            {
                // Decrease trip timer
                IncrementTripTimer(_timerIncrement);
                FishingTrawler.SyncTrawler(SyncType.TripTimer, _fishingTripTimer.Value, FishingTrawler.GetFarmersOnTrawler());

                // Decrease computer cooldown
                trawlerCabin.ReduceCooldown(_computerCooldownIncrement);
                FishingTrawler.SyncTrawler(SyncType.GPSCooldown, trawlerCabin.GetCooldown(), FishingTrawler.GetFarmersOnTrawler());
            }

            // Every x seconds recalculate the amount of fish caught / lost
            if (e.IsMultipleOf(_fishUpdateInterval))
            {
                trawlerSurface.UpdateFishCaught(trawlerHull.GetFuelLevel());
                FishingTrawler.SyncTrawler(SyncType.FishCaught, trawlerSurface.fishCaughtQuantity, FishingTrawler.GetFarmersOnTrawler());
            }

            // Every x seconds recalculate the flood level
            if (e.IsMultipleOf(_floodUpdateInterval))
            {
                // Update water level (from leaks) every second
                trawlerHull.RecalculateWaterLevel();
                FishingTrawler.SyncTrawler(SyncType.WaterLevel, trawlerHull.GetWaterLevel(), FishingTrawler.GetFarmersOnTrawler());
            }

            // Every x seconds recalculate the fuel level
            if (e.IsMultipleOf(_fuelUpdateInterval))
            {
                trawlerHull.AdjustFuelLevel(trawlerHull.fuelConsumptionIncrement);
                FishingTrawler.SyncTrawler(SyncType.Fuel, trawlerHull.fuelConsumptionIncrement, FishingTrawler.GetFarmersOnTrawler());
            }

            // Every random interval check for new event (leak, net tearing, etc.) on Trawler
            if (e.IsMultipleOf(_hullEventInterval))
            {
                // Check if the player gets lucky and skips getting an event, otherwise create the event(s)
                string message;
                if (Game1.random.NextDouble() < 0.05)
                {
                    message = Game1.random.NextDouble() < 0.5 ? FishingTrawler.i18n.Get("status_message.sea_favors_us") : Game1.random.NextDouble() < 0.5 ? FishingTrawler.i18n.Get("status_message.default") : FishingTrawler.i18n.Get("status_message.yoba_be_praised");
                }
                else
                {
                    message = GetMostImportantMessage(CreateEvents(trawlerHull), trawlerSurface, trawlerHull);
                }

                // Check for empty string 
                if (string.IsNullOrEmpty(message) is false)
                {
                    FishingTrawler.notificationManager.SetNotification(message);
                }

                _hullEventInterval = (uint)Game1.random.Next(FishingTrawler.config.hullEventFrequencyLower, FishingTrawler.config.hullEventFrequencyUpper + 1) * 100;
            }
            if (e.IsMultipleOf(_netEventInterval))
            {
                // Check if the player gets lucky and skips getting an event, otherwise create the event(s)
                string message;
                if (Game1.random.NextDouble() < 0.05)
                {
                    message = Game1.random.NextDouble() < 0.5 ? FishingTrawler.i18n.Get("status_message.sea_favors_us") : Game1.random.NextDouble() < 0.5 ? FishingTrawler.i18n.Get("status_message.default") : FishingTrawler.i18n.Get("status_message.yoba_be_praised");
                }
                else
                {
                    message = GetMostImportantMessage(CreateEvents(trawlerSurface), trawlerSurface, trawlerHull);
                }

                // Check for empty string 
                if (string.IsNullOrEmpty(message) is false)
                {
                    FishingTrawler.notificationManager.SetNotification(message);
                }

                _netEventInterval = (uint)Game1.random.Next(FishingTrawler.config.netEventFrequencyLower, FishingTrawler.config.netEventFrequencyUpper + 1) * 100;
            }

            // Update the track if needed
            if (FishingTrawler.themeSongUpdated)
            {
                FishingTrawler.themeSongUpdated = false;

                trawlerCabin.miniJukeboxTrack.Value = string.IsNullOrEmpty(FishingTrawler.trawlerThemeSong) ? null : FishingTrawler.trawlerThemeSong;
                trawlerHull.miniJukeboxTrack.Value = string.IsNullOrEmpty(FishingTrawler.trawlerThemeSong) ? null : FishingTrawler.trawlerThemeSong;
                trawlerSurface.miniJukeboxTrack.Value = string.IsNullOrEmpty(FishingTrawler.trawlerThemeSong) ? null : FishingTrawler.trawlerThemeSong;
            }
        }

        internal int GetTripTimer()
        {
            return _fishingTripTimer.Value;
        }

        internal void SetTripTimer(int milliseconds)
        {
            _fishingTripTimer.Value = milliseconds;
        }

        internal void IncrementTripTimer(int milliseconds)
        {
            SetTripTimer(_fishingTripTimer.Value + milliseconds);
        }

        private string GetMostImportantMessage(List<KeyValuePair<string, int>> possibleMessages, TrawlerSurface trawlerSurface, TrawlerHull trawlerHull)
        {
            int executedEvents = possibleMessages.Count;

            // Check if all possible events are activated
            if (trawlerSurface.AreAllNetsRipped() && trawlerHull.IsEngineFailing() && trawlerHull.AreAllHolesLeaking())
            {
                possibleMessages.Add(GetMessageEverythingFailing());
            }

            // Add a generic message if there are lots of issues
            if (trawlerSurface.GetRippedNetsCount() + trawlerHull.GetLeakingHolesCount() + (trawlerHull.IsEngineFailing() ? 1 : 0) > 1)
            {
                possibleMessages.Add(GetMessageMultiProblems());
            }
            // Select highest priority item (priority == default_priority_level * frequency)
            return executedEvents == 0 ? null : possibleMessages.OrderByDescending(m => m.Value * possibleMessages.Count(p => p.Key == m.Key)).FirstOrDefault().Key;
        }

        private List<KeyValuePair<string, int>> CreateEvents(TrawlerHull trawlerHull)
        {
            int executedEvents = 0;
            List<KeyValuePair<string, int>> possibleMessages = new List<KeyValuePair<string, int>>();

            // Attempt hull leaks
            for (int x = 0; x < trawlerHull.GetPatchedHolesCount(); x++)
            {
                // Break loop of leaks are disabled
                if (trawlerHull.areLeaksEnabled is false)
                {
                    break;
                }

                // Chance of stopping hull breaks increases with each pass of this loop
                if (Game1.random.NextDouble() >= 0.2f + x * 0.1f)
                {
                    // If hull is weak, create all possible leaks
                    if (trawlerHull.hasWeakHull)
                    {
                        foreach (Location tile in trawlerHull.GetAllLeakableLocations())
                        {
                            trawlerHull.AttemptCreateHullLeak(tile.X, tile.Y);
                            FishingTrawler.BroadcastTrawlerEvent(EventType.HullHole, new Vector2(tile.X, tile.Y), false, FishingTrawler.GetFarmersOnTrawler());
                        }
                        break;
                    }
                    else
                    {
                        Location? tile = trawlerHull.GetRandomPatchedHullHole();
                        if (tile is not null)
                        {
                            // Skip tiles that were recently repaired
                            if (trawlerHull.WasTileRepairedRecently(tile.Value.X, tile.Value.Y) is true)
                            {
                                continue;
                            }

                            trawlerHull.AttemptCreateHullLeak(tile.Value.X, tile.Value.Y);
                            FishingTrawler.BroadcastTrawlerEvent(EventType.HullHole, new Vector2(tile.Value.X, tile.Value.Y), false, FishingTrawler.GetFarmersOnTrawler());
                        }
                    }

                    possibleMessages.Add(trawlerHull.AreAllHolesLeaking() ? GetMessageMaxLeaks() : GetMessageLeakProblem());

                    executedEvents++;
                }
                else
                {
                    // Stop attempting for hull breaks
                    break;
                }
            }
            trawlerHull.ClearAllRepairedTiles();

            return possibleMessages;
        }

        private List<KeyValuePair<string, int>> CreateEvents(TrawlerSurface trawlerSurface)
        {
            int executedEvents = 0;
            List<KeyValuePair<string, int>> possibleMessages = new List<KeyValuePair<string, int>>();

            // Attempt net tears
            for (int x = 0; x < trawlerSurface.GetWorkingNetCount(); x++)
            {
                Location? tile = trawlerSurface.GetRandomWorkingNet();
                if (tile is not null)
                {
                    // Skip tiles that were recently repaired
                    if (trawlerSurface.WasTileRepairedRecently(tile.Value.X, tile.Value.Y) is true)
                    {
                        continue;
                    }

                    // Chance of stopping net breaks increases with each pass of this loop
                    if (Game1.random.NextDouble() < 0.1f + x * 0.25f)
                    {
                        // Stop attempting for net breaks
                        break;
                    }

                    trawlerSurface.AttemptCreateNetRip(tile.Value.X, tile.Value.Y);
                    FishingTrawler.BroadcastTrawlerEvent(EventType.NetTear, new Vector2(tile.Value.X, tile.Value.Y), false, FishingTrawler.GetFarmersOnTrawler());
                }

                possibleMessages.Add(trawlerSurface.AreAllNetsRipped() ? GetMessageLosingFish() : GetMessageNetProblem());

                executedEvents++;
            }
            trawlerSurface.ClearAllRepairedTiles();

            return possibleMessages;
        }
    }
}
