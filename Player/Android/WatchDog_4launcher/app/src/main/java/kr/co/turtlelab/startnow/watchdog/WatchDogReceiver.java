
package kr.co.turtlelab.startnow.watchdog;

import android.app.Notification;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.preference.PreferenceManager;
import android.support.v4.app.NotificationCompat;
import android.support.v4.app.NotificationManagerCompat;

public class WatchDogReceiver extends BroadcastReceiver {
	
	public static String pkgname = "kr.co.turtlelab.andowsignage";
    public static String clsname = "AndoWSignage";
    
	public static final String KEY_WATCH_ENABLE = "watch_enable";
	public static final String KEY_WATCH_PING = "watchdog_ping_ts";
	private final int NM_ID = 2442;
	
	@Override
	public void onReceive(Context context, Intent intent) {
		if(context == null || intent == null || intent.getAction() == null)
			return;

		if(intent.getAction().equalsIgnoreCase("kr.co.turtlelab.watchdog.state")) {
			toggle(context);
		}
		else if(intent.getAction().equalsIgnoreCase("kr.co.turtlelab.watchdog.disable")) {
			setEnable(context, false, false);
		} 
		else if(intent.getAction().equalsIgnoreCase("kr.co.turtlelab.watchdog.enable")) {
			setEnable(context, true, false);
		}
		else if(intent.getAction().equalsIgnoreCase("kr.co.turtlelab.watchdog.enable7wait")) {
			setEnable(context, true, true);
		}
		else if(intent.getAction().equalsIgnoreCase("kr.co.turtlelab.watchdog.ping")) {
			updatePing(context);
		}
		else if(intent.getAction().equalsIgnoreCase("kr.co.turtlelab.watchdog.kill")) {
			context.stopService(new Intent(context, WatchDogService.class));
		}

	}

	private void updatePing(Context context) {
		SharedPreferences.Editor editor = PreferenceManager.getDefaultSharedPreferences(context).edit();
		editor.putLong(KEY_WATCH_PING, System.currentTimeMillis());
		editor.commit();
	}
	
	void toggle(Context context) {
		final Context ctx = context;
		new Thread(new Runnable() {
			
			@Override
			public void run() {
				SharedPreferences prefs = PreferenceManager.getDefaultSharedPreferences(ctx);
				boolean enabled = !prefs.getBoolean(KEY_WATCH_ENABLE, false);
				setEnable(ctx, enabled, false);
			}
		}).start();
	}
	
	void setEnable(Context context, boolean enable, boolean wait7) {
		final Context ctx = context.getApplicationContext();
		final boolean state = enable;
		final boolean wait = wait7;

		SharedPreferences.Editor editor = PreferenceManager.getDefaultSharedPreferences(ctx).edit();
		editor.putBoolean(KEY_WATCH_ENABLE, state);
		editor.commit();
		showNotification(ctx, state);

		if(!state)
			return;

		new Thread(new Runnable() {
			@Override
			public void run() {
				if(wait) {
					try {
						Thread.sleep(7000);
					} catch (InterruptedException e) {
						Thread.currentThread().interrupt();
						return;
					}
				}

				if(WatchDogService.wts != null && WatchDogService.wts._timeGap > 1)
					SystemUtils.launchAppNewOrClear(ctx, pkgname, clsname);
			}
		}).start();
	}

	private void showNotification(Context context, boolean enabled) {
		String title = enabled ? "WatchDog (Enabled)" : "WatchDog (Disabled)";
		NotificationManagerCompat nm = NotificationManagerCompat.from(context);

		Intent customIntent = new Intent("kr.co.turtlelab.watchdog.state");
		PendingIntent contentIntent = PendingIntent.getBroadcast(context, 0, customIntent, 0);

		NotificationCompat.Builder noti = new NotificationCompat.Builder(context)
			.setContentTitle(title)
            .setContentText("").setSmallIcon(R.drawable.ic_launcher)
            .setWhen(0)
            .setContentIntent(contentIntent)
            .setOngoing(true);

		nm.notify(NM_ID, noti.build());
	}
}
