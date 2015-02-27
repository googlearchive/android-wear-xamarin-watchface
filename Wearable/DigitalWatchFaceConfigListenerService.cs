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
using Android.Gms.Wearable;
using Android.Gms.Common.Apis;
using Android.Util;
using Java.Util.Concurrent;
using Android.Gms.Common;
using Android.App;

namespace Google.XamarinSamples.WatchFace
{
	// A WearableListenerService listening for DigitalWatchFaceService config messages
	// and updating the config DataItem accordingly.
	[Service]
	[IntentFilter (new[]{"com.google.android.gms.wearable.BIND_LISTENER"})]
	public class DigitalWatchFaceConfigListenerService: WearableListenerService,
		IGoogleApiClientConnectionCallbacks,
		IGoogleApiClientOnConnectionFailedListener
	{
		const string Tag = "DigitalConfigListener";

		IGoogleApiClient googleApiClient;

		public override void OnMessageReceived (IMessageEvent messageEvent)
		{
			if (!messageEvent.Path.Equals (DigitalWatchFaceUtil.PathWithFeature)) {
				return;
			}
			var rawData = messageEvent.GetData ();

			// It's allowed that the message carries only some of the keys used in the config DataItem
			// and skips the ones that we don't want to change.
			var configKeysToOverwrite = DataMap.FromByteArray (rawData);
			if (Log.IsLoggable (Tag, LogPriority.Debug)) {
				Log.Debug (Tag, "Received watch face config message: " + configKeysToOverwrite);
			}

			if (googleApiClient == null) {
				googleApiClient = new GoogleApiClientBuilder (this)
					.AddConnectionCallbacks (this)
					.AddOnConnectionFailedListener (this)
					.AddApi (WearableClass.Api)
					.Build ();
			}
			if (!googleApiClient.IsConnected) {
				var connectionResult = googleApiClient.BlockingConnect (30, TimeUnit.Seconds);
				if (!connectionResult.IsSuccess) {
					Log.Error (Tag, "Failed to connect to GoogleApiClient.");
					return;
				}
			}

			DigitalWatchFaceUtil.OverwriteKeysInConfigDataMap (googleApiClient, configKeysToOverwrite);
		}

		public void OnConnected (Android.OS.Bundle connectionHint)
		{
			if (Log.IsLoggable (Tag, LogPriority.Debug)) {
				Log.Debug (Tag, "OnConnected: " + connectionHint);
			}
		}
		public void OnConnectionSuspended (int cause)
		{
			if (Log.IsLoggable (Tag, LogPriority.Debug)) {
				Log.Debug (Tag, "OnConnectionSuspended: " + cause);
			}
		}

		public void OnConnectionFailed (ConnectionResult result)
		{
			if (Log.IsLoggable (Tag, LogPriority.Debug)) {
				Log.Debug (Tag, "OnConnectionFailed: " + result);
			}
		}
	}
}

