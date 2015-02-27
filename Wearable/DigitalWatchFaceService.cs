//
//  Copyright 2015 Google Inc. All Rights Reserved.
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

using System;
using Android.Support.Wearable.Watchface;
using Android.Views;
using Android.OS;
using Android.Graphics;
using Android.App;
using Android.Text.Format;
using Android.Util;
using Java.Util.Concurrent;
using System.Threading;
using Android.Content;
using Android.Content.Res;
using Android.Gms.Common.Apis;
using Android.Gms.Wearable;
using Java.Lang;
using System.Collections;
using Android.Gms.Common.Data;
using Android.Runtime;

namespace Google.XamarinSamples.WatchFace
{
	// Sample digital watch face with blinking colons and seconds. In ambient mode, the seconds are
	// replaced with an AM/PM indicator and the colons don't blink. On devices with low-bit ambient
	// mode, the text is drawn without anti-aliasing in ambient mode. On devices which require burn-in
	// protection, the hours are drawn in normal rather than bold. The time is drawn with less contrast
	// and without seconds in mute mode.
	[Service (Label = "DigitalWatchFaceService")]
	public class DigitalWatchFaceService: CanvasWatchFaceService
	{
		const string Tag = "DigitalWatchFaceService";

		static Typeface BoldTypeFace = Typeface.Create (Typeface.SansSerif, TypefaceStyle.Bold);
		static Typeface NormalTypeFace = Typeface.Create (Typeface.SansSerif, TypefaceStyle.Normal);

		// Update rate in milliseconds for normal (not ambient and not mute) mode. We update twice
		// a second to blink the colons.
		const long NormalUpdateRateMs = 500;

		// Update rate in milliseconds for mute mode. We update every minute, like in ambient mode.
		static long MuteUpdateRateMs = TimeUnit.Minutes.ToMillis (1);

		public override Android.Service.Wallpaper.WallpaperService.Engine OnCreateEngine ()
		{
			return new DigitalWatchFaceEngine (this);
		}

		class DigitalWatchFaceEngine : CanvasWatchFaceService.Engine,
			IDataApiDataListener,
			IGoogleApiClientConnectionCallbacks, 
			IGoogleApiClientOnConnectionFailedListener
		{
			const string ColonString = ":";

			// Alpha value for drawing time when in mute mode.
			const int MuteAlpha = 100;

			// Alpha value for drawing time when not in mute mode.
			const int NormalAlpha = 255;

			CanvasWatchFaceService owner;

			// How often the timer ticks in milliseconds.
			long InteractiveUpdateRateMs = NormalUpdateRateMs;

			bool mute;

			Paint backgroundPaint;
			Paint hourPaint;
			Paint minutePaint;
			Paint secondPaint;
			Paint amPmPaint;
			Paint colonPaint;
			float colonWidth;

			Color interactiveBackgroundColor = DigitalWatchFaceUtil.ColorValueDefaultAndAmbientBackground;

			Color InteractiveBackgroundColor {
				get { return interactiveBackgroundColor; }
				set {
					interactiveBackgroundColor = value;
					UpdatePaintIfInteractive (backgroundPaint, value);
				}
			}

			Color interactiveHourDigitsColor = DigitalWatchFaceUtil.ColorValueDefaultAndAmbientHourDigits;

			Color InteractiveHourDigitsColor {
				get { return interactiveHourDigitsColor; }
				set {
					interactiveHourDigitsColor = value;
					UpdatePaintIfInteractive (hourPaint, value);
				}
			}

			Color interactiveMinuteDigitsColor = DigitalWatchFaceUtil.ColorValueDefaultAndAmbientMinuteDigits;

			Color InteractiveMinuteDigitsColor {
				get { return interactiveMinuteDigitsColor; }
				set {
					interactiveMinuteDigitsColor = value;
					UpdatePaintIfInteractive (minutePaint, value);
				}
			}

			Color interactiveSecondDigitsColor = DigitalWatchFaceUtil.ColorValueDefaultAndAmbientSecondDigits;

			Color InteractiveSecondDigitsColor {
				get { return interactiveSecondDigitsColor; }
				set {
					interactiveSecondDigitsColor = value;
					UpdatePaintIfInteractive (secondPaint, value);
				}
			}

			void UpdatePaintIfInteractive (Paint paint, Color interactiveColor)
			{
				if (!IsInAmbientMode && paint != null) {
					paint.Color = interactiveColor;
				}
			}

			Timer timerSeconds;
			TimeZoneReceiver timeZoneReceiver;
			bool registeredTimezoneReceiver = false;

			// Whether the display supports fewer bits for each color in ambient mode. When true, we
			// disable anti-aliasing in ambient mode.
			bool lowBitAmbient;

			bool shouldDrawColons;
			float xOffset;
			float yOffset;

			IGoogleApiClient googleApiClient;

			public DigitalWatchFaceEngine (CanvasWatchFaceService owner) : base (owner)
			{
				this.owner = owner;
			}

			public override void OnCreate (ISurfaceHolder surfaceHolder)
			{
				base.OnCreate (surfaceHolder);
				SetWatchFaceStyle (new WatchFaceStyle.Builder (owner)
					.SetCardPeekMode (WatchFaceStyle.PeekModeShort)
					.SetBackgroundVisibility (WatchFaceStyle.BackgroundVisibilityInterruptive)
					.SetShowSystemUiTime (false)
					.Build ()
				);

				var resources = owner.Resources;
				yOffset = resources.GetDimension (Resource.Dimension.DigitalYOffset);

				backgroundPaint = new Paint ();
				backgroundPaint.Color = interactiveBackgroundColor;
				hourPaint = CreateTextPaint (interactiveHourDigitsColor, BoldTypeFace);
				minutePaint = CreateTextPaint (interactiveMinuteDigitsColor);
				secondPaint = CreateTextPaint (interactiveSecondDigitsColor);
				amPmPaint = CreateTextPaint (owner.Resources.GetColor (Resource.Color.DigitalAmPm));
				colonPaint = CreateTextPaint (owner.Resources.GetColor (Resource.Color.DigitalColons));

				googleApiClient = new GoogleApiClientBuilder (owner)
					.AddConnectionCallbacks (this)
					.AddOnConnectionFailedListener (this)
					.AddApi (WearableClass.Api)
					.Build ();
			}

			public override void OnApplyWindowInsets (WindowInsets insets)
			{
				base.OnApplyWindowInsets (insets);
				if (Log.IsLoggable (Tag, LogPriority.Debug)) {
					Log.Debug (Tag, "OnApplyWindowInsets: " + (insets.IsRound ? "round" : "square"));
				}

				// Load resources that have alternate values for round watches.
				var resources = owner.Resources;
				bool isRound = insets.IsRound;
				xOffset = resources.GetDimension (isRound
					? Resource.Dimension.DigitalXOffsetRound : Resource.Dimension.DigitalXOffset);
				var textSize = resources.GetDimension (isRound
					? Resource.Dimension.DigitalTextSizeRound : Resource.Dimension.DigitalTextSize);
				var amPmSize = resources.GetDimension (isRound
					? Resource.Dimension.DigitalAmPmSizeRound : Resource.Dimension.DigitalAmPmSize);

				hourPaint.TextSize = textSize;
				minutePaint.TextSize = textSize;
				secondPaint.TextSize = textSize;
				amPmPaint.TextSize = amPmSize;
				colonPaint.TextSize = textSize;

				colonWidth = colonPaint.MeasureText (ColonString);
			}

			public override void OnPropertiesChanged (Bundle properties)
			{
				base.OnPropertiesChanged (properties);
				bool burnInProtection = properties.GetBoolean (WatchFaceService.PropertyBurnInProtection);
				hourPaint.SetTypeface (burnInProtection ? NormalTypeFace : BoldTypeFace);
				lowBitAmbient = properties.GetBoolean (WatchFaceService.PropertyLowBitAmbient);
				if (Log.IsLoggable (Tag, LogPriority.Debug)) {
					Log.Debug (Tag, "OnPropertiesChanged: burn-in protection: " + burnInProtection
					+ ", low-bit ambient = " + lowBitAmbient);
				}
			}

			public override void OnTimeTick ()
			{
				base.OnTimeTick ();
				if (Log.IsLoggable (Tag, LogPriority.Debug)) {
					Log.Debug (Tag, "onTimeTick: ambient = " + IsInAmbientMode);
				}
				Invalidate ();
			}

			public override void OnAmbientModeChanged (bool inAmbientMode)
			{
				base.OnAmbientModeChanged (inAmbientMode);

				if (Log.IsLoggable (Tag, LogPriority.Debug)) {
					Log.Debug (Tag, "OnAmbientMode");
				}
				AdjustPaintColorToCurrentMode (backgroundPaint, interactiveBackgroundColor,
					DigitalWatchFaceUtil.ColorValueDefaultAndAmbientBackground);
				AdjustPaintColorToCurrentMode (hourPaint, interactiveHourDigitsColor,
					DigitalWatchFaceUtil.ColorValueDefaultAndAmbientHourDigits);
				AdjustPaintColorToCurrentMode (minutePaint, interactiveMinuteDigitsColor,
					DigitalWatchFaceUtil.ColorValueDefaultAndAmbientMinuteDigits);
				AdjustPaintColorToCurrentMode (minutePaint, interactiveMinuteDigitsColor,
					DigitalWatchFaceUtil.ColorValueDefaultAndAmbientMinuteDigits);
				AdjustPaintColorToCurrentMode (secondPaint, interactiveSecondDigitsColor,
					DigitalWatchFaceUtil.ColorValueDefaultAndAmbientSecondDigits);
				if (lowBitAmbient) {
					bool antiAlias = !inAmbientMode;
					hourPaint.AntiAlias = antiAlias;
					minutePaint.AntiAlias = antiAlias;
					secondPaint.AntiAlias = antiAlias;
					amPmPaint.AntiAlias = antiAlias;
					colonPaint.AntiAlias = antiAlias;
				}
				Invalidate ();

				UpdateTimer ();
			}

			private void AdjustPaintColorToCurrentMode (Paint paint, Color interactiveColor, Color ambientColor)
			{
				paint.Color = IsInAmbientMode ? ambientColor : interactiveColor;
			}

			public override void OnInterruptionFilterChanged (int interruptionFilter)
			{
				base.OnInterruptionFilterChanged (interruptionFilter);
				if (Log.IsLoggable (Tag, LogPriority.Debug)) {
					Log.Debug (Tag, "OnInterruptionFilterChanged");
				}

				bool inMuteMode = (interruptionFilter == WatchFaceService.InterruptionFilterNone);

				// We only need to update once a minute in mute mode.
				SetInteractiveUpdateRateMs (inMuteMode ? MuteUpdateRateMs : NormalUpdateRateMs);

				if (mute != inMuteMode) {
					mute = inMuteMode;
					int alpha = inMuteMode ? MuteAlpha : NormalAlpha;
					hourPaint.Alpha = alpha;
					minutePaint.Alpha = alpha;
					secondPaint.Alpha = alpha;
					colonPaint.Alpha = alpha;
					amPmPaint.Alpha = alpha;
					Invalidate ();
				}
			}

			public void SetInteractiveUpdateRateMs (long updateRateMs)
			{
				if (updateRateMs == InteractiveUpdateRateMs) {
					return;
				}
				InteractiveUpdateRateMs = updateRateMs;

				// Stop and restart the timer so the new update rate takes effect immediately.
				if (ShouldTimerBeRunning ()) {
					UpdateTimer ();
				}
			}

			public override void OnDraw (Canvas canvas, Rect bounds)
			{
				var now = DateTime.Now;

				// Show colons for the first half of each second so the colons blink on when the time
				// updates.
				shouldDrawColons = (JavaSystem.CurrentTimeMillis () % 1000) < 500;

				// Draw the background.
				canvas.DrawRect (0, 0, bounds.Width (), bounds.Height (), backgroundPaint);

				// Draw the hours.
				float x = xOffset;
				var hourString = string.Format ("{0:hh}", now);
				canvas.DrawText (hourString, x, yOffset, hourPaint);
				x += hourPaint.MeasureText (hourString);

				// In ambient and mute modes, always draw the first colon. Otherwise, draw the
				// first colon for the first half of each second.
				if (IsInAmbientMode || mute || shouldDrawColons) {
					canvas.DrawText (ColonString, x, yOffset, colonPaint);
				}
				x += colonWidth;

				// Draw the minutes.
				var minuteString = string.Format ("{0:mm}", now);
				canvas.DrawText (minuteString, x, yOffset, minutePaint);
				x += minutePaint.MeasureText (minuteString);

				// In ambient and mute modes, draw AM/PM. Otherwise, draw a second blinking
				// colon followed by the seconds.
				if (IsInAmbientMode || mute) {
					x += colonWidth;
					string amPmString = string.Format ("{0:tt}", now);
					canvas.DrawText (amPmString, x, yOffset, amPmPaint);
				} else {
					if (shouldDrawColons) {
						canvas.DrawText (ColonString, x, yOffset, colonPaint);
					}
					x += colonWidth;
					var secondString = string.Format ("{0:ss}", now);
					canvas.DrawText (secondString, x, yOffset, secondPaint);
				}

			}

			public override void OnVisibilityChanged (bool visible)
			{
				base.OnVisibilityChanged (visible);
				if (Log.IsLoggable (Tag, LogPriority.Debug)) {
					Log.Debug (Tag, "OnVisibilityChanged: " + visible);
				}
				if (visible) {
					googleApiClient.Connect ();
					RegisterTimezoneReceiver ();
				} else {
					UnregisterTimezoneReceiver ();

					if (googleApiClient != null && googleApiClient.IsConnected) {
						WearableClass.DataApi.RemoveListener (googleApiClient, this);
						googleApiClient.Disconnect ();
					}
				}

				UpdateTimer ();
			}

			private Paint CreateTextPaint (Color defaultInteractiveColor)
			{
				return CreateTextPaint (defaultInteractiveColor, NormalTypeFace);
			}

			private Paint CreateTextPaint (Color defaultInteractiveColor, Typeface typeface)
			{
				Paint paint = new Paint ();
				paint.Color = defaultInteractiveColor;
				paint.SetTypeface (typeface);
				paint.AntiAlias = true;
				return paint;
			}

			private void RegisterTimezoneReceiver ()
			{
				if (registeredTimezoneReceiver) {
					return;
				} else {
					if (timeZoneReceiver == null) {
						timeZoneReceiver = new TimeZoneReceiver ();
						timeZoneReceiver.Receive = (intent) => {
							// TODO redraw / invalidate
						};
					}
					registeredTimezoneReceiver = true;
					var filter = new IntentFilter (Intent.ActionTimezoneChanged);
					owner.RegisterReceiver (timeZoneReceiver, filter);
				}
			}

			private void UnregisterTimezoneReceiver ()
			{
				if (!registeredTimezoneReceiver) {
					return;
				}
				registeredTimezoneReceiver = false;
				owner.UnregisterReceiver (timeZoneReceiver);
			}

			/**
			 * Whether the timer should be running depends on whether we're in ambient mode (as well
			 * as whether we're visible), so we may need to start or stop the timer.
			 */
			private void UpdateTimer ()
			{
				if (Log.IsLoggable (Tag, LogPriority.Debug)) {
					Log.Debug (Tag, "update time");
				}

				if (timerSeconds == null) {
					timerSeconds = new Timer (state => Invalidate (), null,
						TimeSpan.FromMilliseconds (InteractiveUpdateRateMs),
						TimeSpan.FromMilliseconds (InteractiveUpdateRateMs));
				} else {
					if (ShouldTimerBeRunning ()) {
						timerSeconds.Change (0, InteractiveUpdateRateMs);
					} else {
						timerSeconds.Change (Timeout.Infinite, 0);
					}
				}
			}

			bool ShouldTimerBeRunning ()
			{
				return IsVisible && !IsInAmbientMode;
			}

			void UpdateConfigDataItemAndUiOnStartup ()
			{
				DigitalWatchFaceUtil.FetchConfigDataMap (googleApiClient, 
					new DigitalWatchFaceUtil.DataItemResultCallback(dataItemResult => {
						if (dataItemResult.DataItem != null) {
							var dataItem = dataItemResult.DataItem;
							if (dataItem != null) {
								var dataMapItem = DataMapItem.FromDataItem (dataItem);
								var startupConfig = dataMapItem.DataMap;

								SetDefaultValuesForMissingKeys (startupConfig);
								DigitalWatchFaceUtil.PutConfigDataItem (googleApiClient, startupConfig);
								UpdateUiForConfigDataMap (startupConfig);
							}
						}
					})
				);
			}

			void SetDefaultValuesForMissingKeys (DataMap config)
			{
				addIntKeyIfMissing (config,
					DigitalWatchFaceUtil.KeyBackgroundColor, 
					DigitalWatchFaceUtil.ColorValueDefaultAndAmbientBackground);
				addIntKeyIfMissing (config,
					DigitalWatchFaceUtil.KeyHoursColor,
					DigitalWatchFaceUtil.ColorValueDefaultAndAmbientHourDigits);
				addIntKeyIfMissing (config,
					DigitalWatchFaceUtil.KeyMinutesColor, 
					DigitalWatchFaceUtil.ColorValueDefaultAndAmbientMinuteDigits);
				addIntKeyIfMissing (config,
					DigitalWatchFaceUtil.KeySecondsColor, 
					DigitalWatchFaceUtil.ColorValueDefaultAndAmbientSecondDigits);
			}

			void addIntKeyIfMissing (DataMap config, string key, int color)
			{
				if (!config.ContainsKey (key)) {
					config.PutInt (key, color);
				}
			}

			public void OnConnected (Bundle connectionHint)
			{
				if (Log.IsLoggable (Tag, LogPriority.Debug)) {
					Log.Debug (Tag, "OnConnected: " + connectionHint);
				}
				WearableClass.DataApi.AddListener (googleApiClient, this);
				UpdateConfigDataItemAndUiOnStartup ();
			}

			public void OnConnectionSuspended (int cause)
			{
				if (Log.IsLoggable (Tag, LogPriority.Debug)) {
					Log.Debug (Tag, "OnConnectionSuspended: " + cause);
				}
			}

			public void OnConnectionFailed (Android.Gms.Common.ConnectionResult result)
			{
				if (Log.IsLoggable (Tag, LogPriority.Debug)) {
					Log.Debug (Tag, "OnConnectionFailed: " + result);
				}
			}

			public void OnDataChanged (DataEventBuffer dataEvents)
			{
				var events = FreezableUtils.FreezeIterable (dataEvents);
				dataEvents.Close ();
				foreach (var ev in events) {
					var dataEvent = ((Java.Lang.Object)ev).JavaCast<IDataEvent> ();
					if (dataEvent.Type != DataEvent.TypeChanged) {
						continue;
					}

					var dataItem = dataEvent.DataItem;
					if (!dataItem.Uri.Path.Equals (DigitalWatchFaceUtil.PathWithFeature)) {
						continue;
					}

					var dataMapItem = DataMapItem.FromDataItem (dataItem);
					var config = dataMapItem.DataMap;
					if (Log.IsLoggable (Tag, LogPriority.Debug)) {
						Log.Debug (Tag, "Config DataItem updated: " + config);
					}
					UpdateUiForConfigDataMap (config);
				}
			}

			void UpdateUiForConfigDataMap (DataMap config)
			{
				bool uiUpdated = false;
				foreach (var configKey in config.KeySet ()) {
					if (!config.ContainsKey (configKey)) {
						continue;
					}
					int color = config.GetInt (configKey);
					if (Log.IsLoggable (Tag, LogPriority.Debug)) {
						Log.Debug (Tag, "Found watch face config key: " + configKey + " -> "
						+ Integer.ToHexString (color));
					}
					if (UpdateUiForKey (configKey, color)) {
						uiUpdated = true;
					}
				}
				if (uiUpdated) {
					Invalidate ();
				}
			}

			bool UpdateUiForKey (string configKey, int color)
			{
				if (configKey.Equals (DigitalWatchFaceUtil.KeyBackgroundColor)) {
					InteractiveBackgroundColor = new Color (color);
				} else if (configKey.Equals (DigitalWatchFaceUtil.KeyHoursColor)) {
					InteractiveHourDigitsColor = new Color (color);
				} else if (configKey.Equals (DigitalWatchFaceUtil.KeyMinutesColor)) {
					InteractiveMinuteDigitsColor = new Color (color);
				} else if (configKey.Equals (DigitalWatchFaceUtil.KeySecondsColor)) {
					InteractiveSecondDigitsColor = new Color (color);
				} else {
					Log.Warn (Tag, "Unknown key: " + configKey);
					return false;
				}
				return true;
			}
		}

		public class TimeZoneReceiver: BroadcastReceiver
		{
			public Action<Intent> Receive { get; set; }

			public override void OnReceive (Context context, Intent intent)
			{
				if (Receive != null) {
					Receive (intent);
				}
			}
		}

	}

}