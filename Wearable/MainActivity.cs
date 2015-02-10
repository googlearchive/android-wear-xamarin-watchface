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

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Support.V4.App;
using Android.Support.Wearable.Views;
using Android.Views;
using Android.Widget;

// This is here to allow starting the Watchface Sample from the Xamarin debugger.
namespace WatchfaceSample
{
	[Activity (Label = "WatchfaceDemo", MainLauncher = true, Icon = "@drawable/icon")]
	public class MainActivity : Activity
	{
		int count = 1;

		protected override void OnCreate (Bundle bundle)
		{
			base.OnCreate (bundle);

			// Set our view from the "main" layout resource
			SetContentView (Resource.Layout.Main);

			var v = FindViewById<WatchViewStub> (Resource.Id.watch_view_stub);
			v.LayoutInflated += delegate {

				// Get our button from the layout resource,
				// and attach an event to it
				Button button = FindViewById<Button> (Resource.Id.myButton);

				button.Click += delegate {
					var notification = new NotificationCompat.Builder (this)
						.SetContentTitle ("Button tapped")
						.SetContentText ("Button tapped " + count++ + " times!")
						.SetSmallIcon (Android.Resource.Drawable.StatNotifyVoicemail)
						.SetGroup ("group_key_demo").Build ();

					var manager = NotificationManagerCompat.From (this);
					manager.Notify (1, notification);
					button.Text = "Check Notification!";
				};
			};
		}
	}
}



