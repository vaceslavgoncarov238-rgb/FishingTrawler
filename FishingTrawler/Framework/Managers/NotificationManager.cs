using FishingTrawler.GameLocations;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.BellsAndWhistles;
using System;

namespace FishingTrawler.Framework.Managers
{
    public class NotificationManager
    {
        private IMonitor _monitor;

        // Notification related
        private string _activeNotification;
        private float _notificationAlpha;
        private bool _isNotificationFading;
        private double _remainingMilliseconds;

        public NotificationManager(IMonitor monitor)
        {
            _monitor = monitor;

            Reset();
        }

        private void Reset()
        {
            // Set up notification related variables
            _activeNotification = string.Empty;
            _notificationAlpha = 1f;
            _isNotificationFading = false;
            _remainingMilliseconds = 3000f;
        }

        internal string GetActiveNotification()
        {
            return _activeNotification;
        }

        internal void SetNotification(string message, bool forceRefresh = false)
        {
            if (String.IsNullOrEmpty(_activeNotification) is true || forceRefresh is true)
            {
                Reset();

                _activeNotification = message;
                FishingTrawler.BroadcastNotification(message, FishingTrawler.GetFarmersOnTrawler());
            }
        }

        internal bool HasExpired(double milliseconds)
        {
            if (String.IsNullOrEmpty(_activeNotification) is false)
            {
                _remainingMilliseconds -= milliseconds;
            }

            return _remainingMilliseconds <= 0;
        }

        internal void FadeNotification(float fadeAmount)
        {
            if (_isNotificationFading is false)
            {
                return;
            }
            _notificationAlpha -= fadeAmount;

            if (_notificationAlpha < 0f)
            {
                Reset();
            }
        }

        internal void StartFading()
        {
            if (String.IsNullOrEmpty(_activeNotification) is false)
            {
                _isNotificationFading = true;
            }
        }

        internal void DrawNotification(SpriteBatch b, GameLocation location)
        {
            if (string.IsNullOrEmpty(_activeNotification))
            {
                return;
            }

            Vector2 position;
            switch (location)
            {
                case TrawlerSurface trawlerSurface:
                    position = FishingTrawler.config.useOldTrawlerSprite
                        ? Game1.GlobalToLocal(new Vector2(40.45f, 22.8f) * 64f)
                        : Game1.GlobalToLocal(new Vector2(41.5f, 23.7f) * 64f);
                    break;
                case TrawlerHull trawlerHull:
                    position = Game1.GlobalToLocal(new Vector2(1.59f, 0.58f) * 64f);
                    break;
                case TrawlerCabin trawlerCabin:
                    position = Game1.GlobalToLocal(new Vector2(8.5f, 0f) * 64f);
                    break;
                default:
                    position = new Vector2(0f, 0f);
                    break;
            }

            string text = _activeNotification.Replace(Environment.NewLine, "").Replace('♡', '<');

            SpriteText.drawString(
                b,
                text,
                (int)position.X,
                (int)position.Y,
                width: 1500,
                alpha: _notificationAlpha,
                layerDepth: 0.88f,
                drawBGScroll: 0
            );
        }
    }
}
