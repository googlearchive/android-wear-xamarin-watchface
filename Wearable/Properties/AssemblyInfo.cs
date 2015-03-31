using System.Reflection;
using System.Runtime.CompilerServices;
using Android.App;

// Information about this assembly is defined by the following attributes.
// Change them to the values specific to your project.

[assembly: AssemblyTitle ("WatchfaceSample")]
[assembly: AssemblyDescription ("")]
[assembly: AssemblyConfiguration ("")]
[assembly: AssemblyCompany ("Google Inc.")]
[assembly: AssemblyProduct ("")]
[assembly: AssemblyCopyright ("Google Inc. All Rights Reserved.")]
[assembly: AssemblyTrademark ("")]
[assembly: AssemblyCulture ("")]

// The assembly version has the format "{Major}.{Minor}.{Build}.{Revision}".
// The form "{Major}.{Minor}.*" will automatically update the build and revision,
// and "{Major}.{Minor}.{Build}.*" will update just the revision.

[assembly: AssemblyVersion ("1.0.0")]

// The following attributes are used to specify the signing key for the assembly,
// if desired. See the Mono documentation for more information about signing.

//[assembly: AssemblyDelaySign(false)]
//[assembly: AssemblyKeyFile("")]

[assembly: Android.App.UsesFeature (Android.Content.PM.PackageManager.FeatureWatch)]
[assembly: Application (Theme = "@android:style/Theme.DeviceDefault")]

[assembly: UsesFeature ("android.hardware.type.watch")]
[assembly: UsesPermission ("com.google.android.permission.PROVIDE_BACKGROUND")]
[assembly: UsesPermission (Android.Manifest.Permission.WakeLock)]
[assembly: MetaData ("com.google.android.gms.version", Value="@integer/google_play_services_version")]
