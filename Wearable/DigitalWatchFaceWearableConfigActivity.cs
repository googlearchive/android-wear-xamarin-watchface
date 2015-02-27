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

using Android.App;
using Android.Content;
using Android.OS;
using Android.Views;
using Android.Widget;
using Android.Gms.Wearable;
using Android.Support.Wearable.Views;
using Android.Gms.Common.Apis;
using Android.Animation;
using Android.Support.V7.Widget;
using Android.Graphics;

namespace Google.XamarinSamples.WatchFace
{

	[Activity (Label = "Digital Watchface")]
	[IntentFilter (new[]{ "google.xamarinsamples.watchface.CONFIG_DIGITAL" },
		Categories = new[] {
			"com.google.android.wearable.watchface.category.WEARABLE_CONFIGURATION",
			"android.intent.category.DEFAULT"
		})]
	public class DigitalWatchFaceWearableConfigActivity : Activity, 
		WearableListView.IClickListener, WearableListView.IOnScrollListener
	{
		const string Tag = "DigitalWatchFaceConfig";

		IGoogleApiClient googleApiClient;
		TextView header;

		protected override void OnCreate (Bundle savedInstanceState)
		{
			base.OnCreate (savedInstanceState);

			SetContentView (Resource.Layout.ActivityDigitalConfig);
			header = FindViewById<TextView> (Resource.Id.Header);

			var listView = FindViewById<WearableListView> (Resource.Id.ColorPicker);
			var content = FindViewById<BoxInsetLayout> (Resource.Id.Content);

			content.ApplyWindowInsets = (v, insets) => {
				if (!insets.IsRound) {
					v.SetPaddingRelative (
						Resources.GetDimensionPixelSize (Resource.Dimension.ContentPaddingStart),
						v.PaddingTop,
						v.PaddingEnd,
						v.PaddingBottom
					);
				}
				return v.OnApplyWindowInsets (insets);
			};

			listView.HasFixedSize = true;
			listView.SetClickListener (this);
			listView.AddOnScrollListener (this);
			var colors = Resources.GetStringArray (Resource.Array.ColorArray);
			listView.SetAdapter (new ColorListAdapter (this, colors));

			googleApiClient = new GoogleApiClientBuilder (this)
				.AddApi (WearableClass.Api)
				.Build ();
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

		public void OnClick (WearableListView.ViewHolder viewHolder)
		{
			var colorItemViewHolder = viewHolder as ColorItemViewHolder;
			UpdateConfigDataItem (colorItemViewHolder.ColorItem.GetColor ());
			Finish ();
		}

		void UpdateConfigDataItem (int backgroundColor)
		{
			var configKeysToOverwrite = new DataMap ();
			configKeysToOverwrite.PutInt (DigitalWatchFaceUtil.KeyBackgroundColor, backgroundColor);
			DigitalWatchFaceUtil.OverwriteKeysInConfigDataMap (googleApiClient, configKeysToOverwrite);
		}


		public void OnTopEmptyRegionClick () {}

		public void OnAbsoluteScrollChange (int scroll)
		{
			var newTranslation = Math.Min (-scroll, 0);
			header.TranslationY = newTranslation;
		}

		public void OnCentralPositionChanged (int centralPosition) { }

		public void OnScroll (int scroll) { }

		public void OnScrollStateChanged (int scrollState)
		{
		}

		class ColorListAdapter: WearableListView.Adapter
		{
			readonly string[] colors;

			Context context;

			public ColorListAdapter (Context context, string[] colors)
			{
				this.context = context;
				this.colors = colors;
			}

			public override void OnBindViewHolder (RecyclerView.ViewHolder holder, int position)
			{
				var colorItemViewHolder = (ColorItemViewHolder)holder;
				var colorName = colors [position];
				colorItemViewHolder.ColorItem.SetColor (colorName);

				var layoutParams = new RecyclerView.LayoutParams (ViewGroup.LayoutParams.MatchParent,
					                   ViewGroup.LayoutParams.WrapContent);

				var colorPickerItemMargin = (int)context.Resources.GetDimension (Resource.Dimension.DigitalConfigColorPickerItemMargin);

				// Add margins to first and last item to make it possible for user to tap on them.
				if (position == 0) {
					layoutParams.SetMargins (0, colorPickerItemMargin, 0, 0);
				} else if (position == colors.Length - 1) {
					layoutParams.SetMargins (0, 0, 0, colorPickerItemMargin);
				} else {
					layoutParams.SetMargins (0, 0, 0, 0);
				}
				colorItemViewHolder.ItemView.LayoutParameters = layoutParams;
			}

			public override RecyclerView.ViewHolder OnCreateViewHolder (ViewGroup parent, int viewType)
			{
				return new ColorItemViewHolder (new ColorItem (parent.Context));
			}

			public override int ItemCount {
				get {
					return colors.Length;
				}
			}
		}

		// The layout of a color item including image and label
		class ColorItem: LinearLayout, WearableListView.IOnCenterProximityListener
		{
			// The duration of the expand/shrink animation.
			const int AnimationDurationMs = 150;

			// The ratio for the size of a circle in shrink state.
			const float ShrinkCricleRatio = .75f;

			const float ShrinkLabelAlpha = .5f;
			const float ExpandLabelAlpha = 1f;

			TextView label;
			CircledImageView colorView;

			readonly float expandCircleRadius;
			readonly float shrinkCircleRadius;

			ObjectAnimator expandCircleAnimator;
			ObjectAnimator expandLabelAnimator;
			AnimatorSet expandAnimator;

			ObjectAnimator shrinkCircleAnimator;
			ObjectAnimator shrinkLabelAnimator;
			AnimatorSet shrinkAnimator;

			public void SetColor (string color)
			{
				label.Text = color;
				colorView.SetCircleColor (Color.ParseColor (color));
			}

			public int GetColor ()
			{
				return colorView.DefaultCircleColor;
			}

			public ColorItem (Context context) : base (context)
			{
				View.Inflate (context, Resource.Layout.ColorPickerItem, this);

				label = FindViewById<TextView> (Resource.Id.Label);
				colorView = FindViewById<CircledImageView> (Resource.Id.Color);

				expandCircleRadius = colorView.CircleRadius;
				shrinkCircleRadius = expandCircleRadius * ShrinkCricleRatio;

				shrinkCircleAnimator = ObjectAnimator.OfFloat (colorView, "circleRadius",
					expandCircleRadius, shrinkCircleRadius);
				shrinkLabelAnimator = ObjectAnimator.OfFloat (label, "alpha",
					ExpandLabelAlpha, ShrinkLabelAlpha);

				// FIXME Xamarin: new AnimatorSet().SetDuration(long) should return an AnimatorSet
				shrinkAnimator = new AnimatorSet ();
				shrinkAnimator.SetDuration (AnimationDurationMs);
				shrinkAnimator.PlayTogether (shrinkCircleAnimator, shrinkLabelAnimator);

				expandCircleAnimator = ObjectAnimator.OfFloat (colorView, "circleRadius",
					shrinkCircleRadius, expandCircleRadius);
				expandLabelAnimator = ObjectAnimator.OfFloat (label, "alpha",
					ShrinkLabelAlpha, ExpandLabelAlpha);
				expandAnimator = new AnimatorSet ();
				expandAnimator.SetDuration (AnimationDurationMs);
				expandAnimator.PlayTogether (expandCircleAnimator, expandLabelAnimator);
			}

			public void OnCenterPosition (bool animate)
			{
				if (animate) {
					shrinkAnimator.Cancel ();
					if (!expandAnimator.IsRunning) {
						expandCircleAnimator.SetFloatValues (colorView.CircleRadius, expandCircleRadius);
						expandLabelAnimator.SetFloatValues (label.Alpha, ExpandLabelAlpha);
						expandAnimator.Start ();
					}
				} else {
					expandAnimator.Cancel ();
					colorView.CircleRadius = expandCircleRadius;
					label.Alpha = ExpandLabelAlpha;
				}

			}

			public void OnNonCenterPosition (bool animate)
			{
				if (animate) {
					expandAnimator.Cancel ();
					if (!shrinkAnimator.IsRunning) {
						shrinkCircleAnimator.SetFloatValues (colorView.CircleRadius, shrinkCircleRadius);
						shrinkLabelAnimator.SetFloatValues (label.Alpha, ShrinkLabelAlpha);
						shrinkAnimator.Start ();
					}
				} else {
					shrinkAnimator.Cancel ();
					colorView.CircleRadius = shrinkCircleRadius;
					label.Alpha = ShrinkLabelAlpha;
				}
			}

		}

		class ColorItemViewHolder: WearableListView.ViewHolder
		{
			public ColorItem ColorItem { get; set; }

			public ColorItemViewHolder (ColorItem colorItem) : base (colorItem)
			{
				this.ColorItem = colorItem;
			}
		}

	}

}