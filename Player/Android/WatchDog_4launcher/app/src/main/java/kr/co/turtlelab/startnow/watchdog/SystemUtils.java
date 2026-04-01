package kr.co.turtlelab.startnow.watchdog;

import java.io.DataOutputStream;
import java.io.File;
import java.io.IOException;
import java.lang.reflect.Field;
import java.util.ArrayList;
import java.util.List;

import android.annotation.SuppressLint;
import android.annotation.TargetApi;
import android.app.Activity;
import android.app.ActivityManager;
import android.app.ActivityManager.RunningAppProcessInfo;
import android.content.ComponentName;
import android.content.Context;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.content.pm.ResolveInfo;
import android.content.res.Configuration;
import android.net.ConnectivityManager;
import android.net.NetworkInfo;
import android.os.Build;
import android.os.Bundle;
import android.os.Environment;
import android.os.Handler;
import android.os.Looper;
import android.os.PowerManager;
import android.os.StatFs;
import android.util.Log;
import android.view.View;
import android.view.Window;
import android.view.WindowManager.LayoutParams;

public class SystemUtils {
	private static final String LAUNCHER_PACKAGE = "kr.co.turtlelab.launcher";
	private static final String ACTION_LAUNCH_COMPONENT = "kr.co.turtlelab.launcher.action.LAUNCH_COMPONENT";
	private static final String EXTRA_TARGET_PACKAGE = "target_package";
	private static final String EXTRA_TARGET_CLASS = "target_class";
	private static final String EXTRA_CLEAR_TASK = "clear_task";

	public static void setDimButtons(Activity act, boolean dimButtons) {     
		Window window = act.getWindow();
		LayoutParams layoutParams = window.getAttributes();     
		float val = dimButtons ? 0 : -1;     
		
		try {         
			Field buttonBrightness = layoutParams.getClass().getField("buttonBrightness");         
			buttonBrightness.set(layoutParams, val);     
			} catch (Exception e) {         
				//e.printStackTrace();     
			}     
		
		window.setAttributes(layoutParams); 
	} 
	
	@TargetApi(android.os.Build.VERSION_CODES.HONEYCOMB)
	@SuppressLint("NewApi")
	public static void systemBarVisibility(Activity act, boolean visible) {
	
//		int hideState = act.getWindow().getDecorView().getSystemUiVisibility();
//				
//		if( android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.ICE_CREAM_SANDWICH )
//			hideState |= View.SYSTEM_UI_FLAG_HIDE_NAVIGATION;
//		if( android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.JELLY_BEAN )
//			hideState |= View.SYSTEM_UI_FLAG_FULLSCREEN;
//		
////		if( android.os.Build.VERSION.SDK_INT >= 19 )
////			hideState |= 4096;
////		else if(android.os.Build.VERSION.SDK_INT < 19)
////			hideState |= View.SYSTEM_UI_FLAG_LOW_PROFILE;
//		
//		if (android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.KITKAT) {
//			hideState |= View.SYSTEM_UI_FLAG_LAYOUT_STABLE | View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN | View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY;
//	    } 
//		
////		else {
////	    	hideState = View.SYSTEM_UI_FLAG_LAYOUT_STABLE
////	    	        | View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION
////	    	        | View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN
////	    	        | View.SYSTEM_UI_FLAG_HIDE_NAVIGATION // hide nav bar
////	    	        | View.SYSTEM_UI_FLAG_FULLSCREEN; // hide status bar
////	    }
//		
//		int showState = View.SYSTEM_UI_FLAG_VISIBLE;
		
		if(visible) {
//		if(VERSION.SDK_INT > 10) {
			//act.getWindow().getDecorView().setSystemUiVisibility(showState);
			act.getWindow().getDecorView().setSystemUiVisibility(
		            View.SYSTEM_UI_FLAG_LAYOUT_STABLE
		            | View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION
		            | View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN);
//		}
		} else {
			//act.getWindow().getDecorView().setSystemUiVisibility(hideState);
			act.getWindow().getDecorView().setSystemUiVisibility(
	                View.SYSTEM_UI_FLAG_LAYOUT_STABLE
	                | View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION
	                | View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN
	                | View.SYSTEM_UI_FLAG_HIDE_NAVIGATION
	                | View.SYSTEM_UI_FLAG_FULLSCREEN
	                | View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY);
		}
	}
	
//	@TargetApi(android.os.Build.VERSION_CODES.HONEYCOMB)
//	@SuppressLint("NewApi")
//	public static void systemBarVisibility(Activity act, boolean visible) {
//
//		if(visible) {
//			act.getWindow().getDecorView().setSystemUiVisibility(View.SYSTEM_UI_FLAG_VISIBLE);
//		} else {
//			
//			// The UI options currently enabled are represented by a bitfield.
//			// getSystemUiVisibility() gives us that bitfield.
//			int uiOptions = act.getWindow().getDecorView().getSystemUiVisibility();
//			int newUiOptions = uiOptions;
//		
//			// Navigation bar hiding: Backwards compatible to ICS.
//			if (android.os.Build.VERSION.SDK_INT >= 14) {
//				newUiOptions ^= View.SYSTEM_UI_FLAG_HIDE_NAVIGATION;
//			}
//	
//			// Status bar hiding: Backwards compatible to Jellybean
//			if (android.os.Build.VERSION.SDK_INT >= 16) {
//				newUiOptions ^= View.SYSTEM_UI_FLAG_FULLSCREEN;
//			}
//	
//			// Immersive mode: Backward compatible to KitKat.
//			// Note that this flag doesn't do anything by itself, it only augments
//			// the behavior
//			// of HIDE_NAVIGATION and FLAG_FULLSCREEN. For the purposes of this
//			// sample
//			// all three flags are being toggled together.
//			// Note that there are two immersive mode UI flags, one of which is
//			// referred to as "sticky".
//			// Sticky immersive mode differs in that it makes the navigation and
//			// status bars
//			// semi-transparent, and the UI flag does not get cleared when the user
//			// interacts with
//			// the screen.
//	
//			if (android.os.Build.VERSION.SDK_INT >= 18) {
//				newUiOptions ^= View.SYSTEM_UI_FLAG_IMMERSIVE;
////				newUiOptions ^= View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY;
//			}
//			
//			act.getWindow().getDecorView().setSystemUiVisibility(newUiOptions);
//		}
//	}
	
	@SuppressWarnings("deprecation")
	public static int getRemainingExtSDPercent()
	{
	    StatFs stat = new StatFs(Environment.getExternalStorageDirectory().getPath());
//	    long remainBytes = (long)stat.getBlockSize() * (long)stat.getAvailableBlocks();
//	    long totalBytes = (long)stat.getBlockSize() * (long)stat.getBlockCount();
	    int remainBlocks = stat.getAvailableBlocks();
	    int totalBlocks = stat.getBlockCount();
	    return (remainBlocks*100)/totalBlocks;
	}
	
	@SuppressWarnings("deprecation")
	public static int getUsingExtSDPercent()
	{
	    return (100 - getRemainingExtSDPercent());
	}
	
	public static boolean checkIsLand(Activity act) {
		return act.getResources().getConfiguration().orientation == Configuration.ORIENTATION_LANDSCAPE;
	}
	

	public static void runOnUiThread(Runnable runnable){
	    final Handler UIHandler = new Handler(Looper.getMainLooper());
	    UIHandler.post(runnable);
	}
	
	public static void uninstallAPK(String packagename) {
		try {   
            Process proc = Runtime.getRuntime().exec(new String[] { "pm", "uninstall", packagename });
            proc.waitFor();
        } catch (Exception e) {
            //e.printStackTrace();
        }
	}
	
	public static void installAPK(String apkpath) {
		File file = new File(apkpath); 
	    if(file.exists()){
	        try {
	            Process proc = Runtime.getRuntime().exec(new String[] { "pm", "install", file.getAbsolutePath() });
	            proc.waitFor();
	        } catch (Exception e) {
	            //e.printStackTrace();
	        }
	     }
	}
	
	public static void replaceAPK(String packagename, String apkpath) {
		uninstallAPK(packagename);
		installAPK(apkpath);
	}
	
//	public static boolean isRunningProcess(Context context, String packageName, boolean ontask) {
//				
//        boolean isRunning = false;
// 
//        ActivityManager actMng = (ActivityManager)context.getSystemService(Context.ACTIVITY_SERVICE);                      
// 
//        List<RunningAppProcessInfo> list = actMng.getRunningAppProcesses();     
// 
//        for(RunningAppProcessInfo rap : list)                           
//        {                                
//            if(rap.processName.equals(packageName))                              
//            {                       
//            	if(rap.importance == rap.IMPORTANCE_FOREGROUND || ontask) {
//            		isRunning = true;     
//            	}
//                break;
//            }                         
//        }
// 
//        return isRunning;
//    }
	
	public static void launchAppNewOrClear(Context context, String pkgname, String clsname) {
//		if(SystemUtils.isRunningProcess(context,pkgname, false))
//			SystemUtils.launchAnotherAct(context, pkgname, clsname, true);
//		else
			SystemUtils.launchAppNewTask(context, pkgname, clsname);
	}
	
	public static boolean launchAppNewTask(Context context, String pkgname, String clsname){
		try {
			if (requestLauncherLaunch(context, pkgname, pkgname + "." + clsname, false)) {
				return true;
			}
			Intent launchIntent = new Intent();
			launchIntent.setComponent(new ComponentName(pkgname, pkgname+"."+clsname));
			launchIntent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
			context.startActivity(launchIntent);
			return true;
		} catch(Exception e) 
		{
			return false;
		}
    }

	public static boolean launchAppNewTask(Context context, String pkgname, String clsname, boolean cleartask){
		try {
			if (requestLauncherLaunch(context, pkgname, pkgname + "." + clsname, cleartask)) {
				return true;
			}
			Intent launchIntent = new Intent();
			launchIntent.setComponent(new ComponentName(pkgname, pkgname+"."+clsname));

			if(cleartask) {
				launchIntent.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP);
				launchIntent.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TASK);
			}

			launchIntent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
			context.startActivity(launchIntent);
			return true;
		} catch(Exception e)
		{
			return false;
		}
	}

	private static boolean requestLauncherLaunch(Context context, String packageName, String className, boolean clearTask) {
		try {
			Intent request = new Intent(ACTION_LAUNCH_COMPONENT);
			request.setPackage(LAUNCHER_PACKAGE);
			request.putExtra(EXTRA_TARGET_PACKAGE, packageName);
			request.putExtra(EXTRA_TARGET_CLASS, className);
			request.putExtra(EXTRA_CLEAR_TASK, clearTask);

			PackageManager packageManager = context.getPackageManager();
			java.util.List<ResolveInfo> receivers = packageManager.queryBroadcastReceivers(request, 0);
			if (receivers == null || receivers.isEmpty()) {
				return false;
			}

			context.sendBroadcast(request);
			return true;
		} catch (Exception e) {
			return false;
		}
	}
	
	public static boolean launchAnotherAct(Context context, String pkgname, String clsname, boolean cleartask) {
		try {
			Intent intent = new Intent();
			intent.setComponent(new ComponentName(pkgname, pkgname+"."+clsname));
			
			if(cleartask) {
				intent.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP);
				intent.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TASK);
	        }
			
			context.startActivity(intent);
			return true;
		} catch(Exception e) 
		{
			return false;
		}
	}
	
	public static boolean launchAnotherActWithData(Context context, String pkgname, String clsname, String data_apkpath, String data_pkgname, String data_clsname, boolean cleartask) {
		try {
			Intent intent = new Intent();
			intent.setComponent(new ComponentName(pkgname, pkgname+"."+clsname));
			
			Bundle extras = new Bundle();
			       extras.putString("apkpath", data_apkpath);
			       extras.putString("pkgname", data_pkgname);
			       extras.putString("clsname", data_clsname);
			       
	        intent.putExtras(extras);
	        
	        if(cleartask) {
				intent.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP);
				intent.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TASK);
	        }
	        
	        context.startActivity(intent);
	        return true;
		} catch(Exception e) 
		{
			return false;
		}
	}
	
	public static boolean launchAnotherAct(Activity act, String pkgname, String clsname, boolean cleartask) {
		try {
			Intent intent = new Intent();
			intent.setComponent(new ComponentName(pkgname, pkgname+"."+clsname));
			
			if(cleartask) {
				intent.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP);
				intent.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TASK);
	        }
			
			act.startActivity(intent);
	        act.finish();
	        return true;
		} catch(Exception e) 
		{
			return false;
		}
	}
	
	public static boolean launchAnotherActWithData(Activity act, String pkgname, String clsname, String data_apkpath, String data_pkgname, String data_clsname, boolean cleartask) {
		try {
			Intent intent = new Intent();
			intent.setComponent(new ComponentName(pkgname, pkgname+"."+clsname));
			
			Bundle extras = new Bundle();
			       extras.putString("apkpath", data_apkpath);
			       extras.putString("pkgname", data_pkgname);
			       extras.putString("clsname", data_clsname);
			       
	        intent.putExtras(extras);
	        
	        if(cleartask) {
				intent.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP);
				intent.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TASK);
	        }
	        
	        act.startActivity(intent);
	        act.finish();
	        return true;
		} catch(Exception e) 
		{
			return false;
		}
	}
	
	public static boolean restartAct(Context ctx, boolean cleartask) {
		try {
			Intent intent = ctx.getPackageManager().getLaunchIntentForPackage(ctx.getPackageName());
			
			if(cleartask) {
				intent.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP);
				intent.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TASK);
	        }
			
			ctx.startActivity(intent);
			return true;
		} catch(Exception e) 
		{
			return false;
		}
	}
	

	public static boolean checkNetwork(Context context) {
		boolean _ret = false;
        ConnectivityManager cm = (ConnectivityManager) context
                .getSystemService(Context.CONNECTIVITY_SERVICE);
 
        NetworkInfo activeNetwork = cm.getActiveNetworkInfo();
        if (null != activeNetwork) {
        	int _type = activeNetwork.getType();
            if(_type == ConnectivityManager.TYPE_WIFI
        		|| _type == ConnectivityManager.TYPE_ETHERNET)
                _ret = true;
        } 
                
        return _ret;
    }
	
	public static boolean checkInternet(Context context) {
		ConnectivityManager cm =
		        (ConnectivityManager)context.getSystemService(Context.CONNECTIVITY_SERVICE);

		NetworkInfo activeNetwork = cm.getActiveNetworkInfo();
		boolean isConnected = activeNetwork != null &&
		                      activeNetwork.isConnectedOrConnecting();
		return isConnected;
	}
	
	public static void RebootDevice() {
		ArrayList<String> commands = new ArrayList<String>();
		commands.add("reboot");
		execute(commands);
	}
	
	public static boolean execute(ArrayList<String> commands)
	{
	     boolean retval = false;

	     try
	     {
	        if (null != commands && commands.size() > 0)
	        {
	           Process suProcess = Runtime.getRuntime().exec("su");

		       DataOutputStream os = new DataOutputStream(suProcess.getOutputStream());
	
		       // Execute commands that require root access
		       for (String currCommand : commands)
		       {
		          os.writeBytes(currCommand + "\n");
		          os.flush();
		       }
	
		       os.writeBytes("exit\n");
		       os.flush();
	
		       try
		       {
		          int suProcessRetval = suProcess.waitFor();
		          if (255 != suProcessRetval)
		          {
		             // Root access granted
		             retval = true;
		          }
		          else
		          {
		             // Root access denied
		             retval = false;
		          }
		       }
		       catch (Exception ex)
		       {
		          Log.e("ROOT", "Error executing root action", ex);
		       }
		    }
	     }
		 catch (IOException ex)
		 {
		    Log.w("ROOT", "Can't get root access", ex);
		 }
		 catch (SecurityException ex)
		 {
		    Log.w("ROOT", "Can't get root access", ex);
		 }
		 catch (Exception ex)
		 {
		    Log.w("ROOT", "Error executing internal operation", ex);
	     }
	
	     return retval;
  	}


//	public static boolean isAppRunning(final Context context, final String packageName) {
//		final ActivityManager activityManager = (ActivityManager) context.getSystemService(Context.ACTIVITY_SERVICE);
//		final List<ActivityManager.RunningAppProcessInfo> procInfos = activityManager.getRunningAppProcesses();
//		if (procInfos != null)
//		{
//			for (final ActivityManager.RunningAppProcessInfo processInfo : procInfos) {
//				if (processInfo.processName.equals(packageName)) {
//					return true;
//				}
//			}
//		}
//		return false;
//	}


//	public static boolean isForeground(Context context, String PackageName){
//		// Get the Activity Manager
//		ActivityManager manager = (ActivityManager) context.getSystemService(context.ACTIVITY_SERVICE);
//
//		// Get a list of running tasks, we are only interested in the last one,
//		// the top most so we give a 1 as parameter so we only get the topmost.
//		List< ActivityManager.RunningTaskInfo > task = manager.getRunningTasks(1);
//
//		// Get the info we need for comparison.
//		ComponentName componentInfo = task.get(0).topActivity;
//
//		// Check if it matches our package name.
//		if(componentInfo.getPackageName().equalsIgnoreCase(PackageName)) return true;
//
//		// If not then our app is not on the foreground.
//		return false;
//	}

//	public static boolean isAppForeground(final Context context, final String packageName) {
//		final ActivityManager activityManager = (ActivityManager) context.getSystemService(Context.ACTIVITY_SERVICE);
//		final List<ActivityManager.RunningAppProcessInfo> procInfos = activityManager.getRunningAppProcesses();
//
//		if (procInfos != null)
//		{
//			for (final ActivityManager.RunningAppProcessInfo processInfo : procInfos) {
//				if (processInfo.processName.equals(packageName)) {
//					ActivityManager.getMyMemoryState(processInfo);
//					return processInfo.importance == ActivityManager.RunningAppProcessInfo.IMPORTANCE_FOREGROUND;
//				}
//			}
//		}
//		return false;
//	}

	public static boolean isForeground(Context context, String PackageName){
		ActivityManager manager = (ActivityManager) context.getSystemService(context.ACTIVITY_SERVICE);

		if(Build.VERSION.SDK_INT > 20){
			return PackageName.equalsIgnoreCase(manager.getRunningAppProcesses().get(0).processName);
		}
		else{
			return PackageName.equalsIgnoreCase(manager.getRunningTasks(1).get(0).topActivity.getPackageName());
		}
	}
}
