//
//  Copyright 2015  Google Inc. All Rights Reserved.
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
using System.Linq;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Widget;
using Android.Gms.Common.Apis;
using Android.Support.Wearable.Companion;
using Android.Gms.Wearable;
using Android.Util;
using Android.Graphics;
using Java.Lang;

namespace Google.XamarinSamples.WatchFace
{
	// The phone-side config activity for DigitalWatchFaceService. Like the watch-side config
	// activity (DigitalWatchFaceWearableConfigActivity), allows for setting the background
	// color. Additionally, enables setting the color for hour, minute and second digits.
	[Activity (Label = "Watchface")]
	[IntentFilter (new[]{ "google.xamarinsamples.watchface.CONFIG_DIGITAL" },
		Categories = new[] {
			"com.google.android.wearable.watchface.category.COMPANION_CONFIGURATION",
			"android.intent.category.DEFAULT"
		})]
	public class DigitalWatchFaceCompanionConfigActivity : Activity,
		IGoogleApiClientConnectionCallbacks,
		IGoogleApiClientOnConnectionFailedListener,
		IResultCallback
	{
		const string Tag = "DigitalWatchFaceConfig";

		const string PathWithFeature = "/watch_face_config/Digital";
		const string KeyBackgroundColor = "BACKGROUND_COLOR";
		const string KeyHoursColor = "HOURS_COLOR";
		const string KeyMinutesColor = "MINUTES_COLOR";
		const string KeySecondsColor = "SECONDS_COLOR";

		IGoogleApiClient googleApiClient;
		string peerId;

		protected override void OnCreate (Bundle savedInstanceState)
		{
			base.OnCreate (savedInstanceState);

			SetContentView (Resource.Layout.ActivityDigitalWatchFaceConfig);

			peerId = Intent.GetStringExtra (WatchFaceCompanion.ExtraPeerId);
			googleApiClient = new GoogleApiClientBuilder (this)
				.AddConnectionCallbacks (this)
				.AddOnConnectionFailedListener (this)
				.AddApi (WearableClass.Api)
				.Build ();

			var name = Intent.GetParcelableExtra (WatchFaceCompanion.ExtraWatchFaceComponent) as ComponentName;
			var label = FindViewById<TextView> (Resource.Id.Label);
			if (name != null) {
				var configText = Resources.GetString (Resource.String.DigitalConfigText);
				label.Text = string.Format ("{0} ({1})", configText, name.ClassName);
			}
		}

		protected override void OnStart ()
		{
			base.OnStart ();
			googleApiClient.Connect ();
		}

		protected override void OnStop ()
		{
			if (googleApiClient != null && googleApiClient.IsConnected) {
				googleApiClient.Disconnect ();
			}
			base.OnStop ();
		}

		public void OnConnected (Bundle connectionHint)
		{
			if (Log.IsLoggable (Tag, LogPriority.Debug)) {
				Log.Debug (Tag, "OnConnected: " + connectionHint);
			}

			if (peerId != null) {
				var uri = new Android.Net.Uri.Builder ()
					.Scheme ("wear")
					.Path (PathWithFeature)
					.Authority (peerId)
					.Build ();
				WearableClass.DataApi.GetDataItem (googleApiClient, uri).SetResultCallback (this);
			} else {
				DisplayNoConnectedDeviceDialog ();
			}
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

		void DisplayNoConnectedDeviceDialog ()
		{
			var messageText = Resources.GetString (Resource.String.TitleNoDeviceConnected);
			var okText = Resources.GetString (Resource.String.OkNoDevice_connected);
			new AlertDialog.Builder (this)
				.SetMessage (messageText)
				.SetCancelable (false)
				.SetPositiveButton (okText, delegate {})
				.Create ()
				.Show ();
		}

		public void OnResult (Java.Lang.Object result)
		{
			var dataItemResult = result.JavaCast<IDataApiDataItemResult> ();
			if (dataItemResult.Status.IsSuccess && dataItemResult.DataItem != null) {
				var configDataItem = dataItemResult.DataItem;
				var dataMapItem = DataMapItem.FromDataItem (configDataItem);
				SetupAllPickers (dataMapItem.DataMap);
			} else {
				SetupAllPickers (null);
			}

		}

		void SetupAllPickers (DataMap config)
		{
			SetupColorPickerSelection (Resource.Id.Background, KeyBackgroundColor, config, Resource.String.ColorBlack);
			SetupColorPickerSelection (Resource.Id.Hours, KeyHoursColor, config, Resource.String.ColorWhite);
			SetupColorPickerSelection (Resource.Id.Minutes, KeyMinutesColor, config, Resource.String.ColorWhite);
			SetupColorPickerSelection (Resource.Id.Seconds, KeySecondsColor, config, Resource.String.ColorGray);

			SetUpColorPickerListener (Resource.Id.Background, KeyBackgroundColor);
			SetUpColorPickerListener (Resource.Id.Hours, KeyHoursColor);
			SetUpColorPickerListener (Resource.Id.Minutes, KeyMinutesColor);
			SetUpColorPickerListener (Resource.Id.Seconds, KeySecondsColor);
		}

		void SetupColorPickerSelection (int spinnerId, string configKey, DataMap config, int defaultColorNameResId)
		{
			var defaultColorName = GetString (defaultColorNameResId);
			var defaultColor = Color.ParseColor (defaultColorName);
			int color = config != null ? config.GetInt (configKey, defaultColor) : defaultColor;
			var spinner = FindViewById<Spinner> (spinnerId);
			var colorNames = Resources.GetStringArray (Resource.Array.ColorArray);
			for (int i = 0; i < colorNames.Length; i++) {
				if (Color.ParseColor (colorNames [i]) == color) {
					spinner.SetSelection (i);
					break;
				}
			}
		}

		void SetUpColorPickerListener (int spinnerId, string configKey)
		{
			var spinner = FindViewById<Spinner> (spinnerId);
			spinner.ItemSelected += (sender, args) => {
				var colorName = (string)args.Parent.GetItemAtPosition (args.Position);
				SendConfigurationUpdateMessage (configKey, Color.ParseColor (colorName));
			};
		}

		void SendConfigurationUpdateMessage (string configKey, int color)
		{
			if (peerId != null) {
				var config = new DataMap ();
				config.PutInt (configKey, color);
				WearableClass.MessageApi.SendMessage (googleApiClient, peerId, PathWithFeature, config.ToByteArray ());

				if (Log.IsLoggable (Tag, LogPriority.Debug)) {
					Log.Debug (Tag, string.Format ("Sent watch face configuration messahe: {0} -> {1}", 
						configKey, Integer.ToHexString (color)));
				}
			}
		}
	}
}