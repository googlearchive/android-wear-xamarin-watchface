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
using Android.Graphics;
using Android.Gms.Wearable;
using Android.Gms.Common.Apis;
using Android.Runtime;
using Android.Util;
using Android.Text;

namespace Google.XamarinSamples.WatchFace
{
	public sealed class DigitalWatchFaceUtil
	{
		const string Tag = "DigitalWatchFaceUtil";

		// The DataMap key for DigitalWatchFaceService background color name.
		// The color name must be a string recognized by Color.parseColor.
		public const string KeyBackgroundColor = "BACKGROUND_COLOR";

		// The DataMap key for DigitalWatchFaceService hour digits color name.
		// The color name must be a string recognized by Color#parseColor.
		public const string KeyHoursColor = "HOURS_COLOR";

		// The DataMap key for DigitalWatchFaceService minute digits color name.
		// The color name must be a string recognized by Color.parseColor.
		public const string KeyMinutesColor = "MINUTES_COLOR";

		// The DataMap key for DigitalWatchFaceService second digits color name.
		// The color name must be a string recognized by Color.parseColor.
		public const string KeySecondsColor = "SECONDS_COLOR";

		// The path for the DataItem containing DigitalWatchFaceService configuration.
		public const string PathWithFeature = "/watch_face_config/Digital";

		// Name of the default interactive mode background color and the ambient mode background color.
		const string ColorNameDefaultAndAmbientBackground = "Black";
		public static Color ColorValueDefaultAndAmbientBackground = Color.ParseColor (ColorNameDefaultAndAmbientBackground);

		// Name of the default interactive mode hour digits color and the ambient mode hour digits
		// color.
		const string ColorNameDefaultAndAmbientHourDigits = "White";
		public static Color ColorValueDefaultAndAmbientHourDigits = Color.ParseColor (ColorNameDefaultAndAmbientHourDigits);

		// Name of the default interactive mode minute digits color and the ambient mode minute digits
		// color.
		const string ColorNameDefaultAndAmbientMinuteDigits = "White";
		public static Color ColorValueDefaultAndAmbientMinuteDigits = Color.ParseColor (ColorNameDefaultAndAmbientMinuteDigits);

		// Name of the default interactive mode second digits color and the ambient mode second digits
		// color.
		const string ColorNameDefaultAndAmbientSecondDigits = "Gray";
		public static Color ColorValueDefaultAndAmbientSecondDigits = Color.ParseColor (ColorNameDefaultAndAmbientSecondDigits);

		internal class ResultCallback: Java.Lang.Object, IResultCallback
		{
			readonly Action<INodeApiGetLocalNodeResult> OnResultAction;

			public ResultCallback (Action<INodeApiGetLocalNodeResult> onResultAction)
			{
				OnResultAction = onResultAction;
			}

			public void OnResult (Java.Lang.Object result)
			{
				var localNodeResult = result.JavaCast<INodeApiGetLocalNodeResult> ();
				OnResultAction (localNodeResult);
			}
		}

		public class DataItemResultCallback: Java.Lang.Object, IResultCallback
		{
			Action<IDataApiDataItemResult> OnResultAction;

			public DataItemResultCallback(Action<IDataApiDataItemResult> action) {
				OnResultAction = action;
			}

			public void OnResult (Java.Lang.Object result)
			{
				var dataItemResult = result.JavaCast<IDataApiDataItemResult> ();
				if (dataItemResult.Status.IsSuccess) {
					OnResultAction (dataItemResult);
				}
			}
		}

		public static void FetchConfigDataMap (IGoogleApiClient googleApiClient, DataItemResultCallback fetchConfigDataMapCallback)
		{
			WearableClass.NodeApi.GetLocalNode (googleApiClient).SetResultCallback (
				new ResultCallback (localNodeResult => {
					var localNode = localNodeResult.Node.Id;
					var uri = new Android.Net.Uri.Builder ()
							.Scheme ("wear")
							.Path (DigitalWatchFaceUtil.PathWithFeature)
							.Authority (localNode)
							.Build ();
					WearableClass.DataApi.GetDataItem (googleApiClient, uri)
							.SetResultCallback (fetchConfigDataMapCallback);
				}
				)
			);
		}

		public static void OverwriteKeysInConfigDataMap (IGoogleApiClient googleApiClient, DataMap configKeysToOverwrite)
		{
			FetchConfigDataMap (googleApiClient, 
				new DataItemResultCallback(dataItemResult => {
					var overwrittenConfig = new DataMap ();

					if (dataItemResult.DataItem != null) {
						var dataItem = dataItemResult.DataItem;
						var dataMapItem = DataMapItem.FromDataItem (dataItem);
						var currentConfig = dataMapItem.DataMap;
						overwrittenConfig.PutAll (currentConfig);
					}

					overwrittenConfig.PutAll (configKeysToOverwrite);
					DigitalWatchFaceUtil.PutConfigDataItem (googleApiClient, overwrittenConfig);
				})
			);
		}

		public static void PutConfigDataItem (IGoogleApiClient googleApiClient, DataMap newConfig)
		{
			var putDataMapRequest = PutDataMapRequest.Create (PathWithFeature);
			var configToPut = putDataMapRequest.DataMap;
			configToPut.PutAll (newConfig);
			WearableClass.DataApi.PutDataItem (googleApiClient, putDataMapRequest.AsPutDataRequest ())
				.SetResultCallback (new DataItemResultCallback(dataItemResult => {
					if (Log.IsLoggable (Tag, LogPriority.Debug)) {
						Log.Debug (Tag, "PutDataItem result status: " + dataItemResult.Status);
					}
				})
			);

		}

		DigitalWatchFaceUtil () { }

	}
}
