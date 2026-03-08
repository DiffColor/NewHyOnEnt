package kr.co.turtlelab.startnow.watchdog;

import java.io.File;
import java.lang.reflect.Method;
import java.text.SimpleDateFormat;
import java.util.ArrayList;
import java.util.Date;
import java.util.List;
import java.util.Locale;

import android.app.ActivityManager;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.app.Service;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.os.Binder;
import android.os.Environment;
import android.os.IBinder;
import android.preference.PreferenceManager;
import android.support.v4.app.NotificationCompat;
import android.support.v4.app.NotificationManagerCompat;

public class WatchDogService extends Service {

	public static WatchDogService wts;
	
    public static String pkgname = "kr.co.turtlelab.andowsignage";
    public static String clsname = "AndoWSignage";
    
	int interval = 10000;
	boolean loaded = false;
	
	LightestTimer watchTimer;
	
    int skipcount = 1;
    int count = 0;
    public long _timeGap = 0;
    private long serviceStartAt = 0;

	SharedPreferences prefs;
	public static final String KEY_WATCH_ENABLE = "watch_enable";
	public static final String KEY_WATCH_PING = "watchdog_ping_ts";
	private static final long PING_VALID_MS = 24000;

	void initTimer() {

		watchTimer = new LightestTimer(interval, new Runnable() {
			
			@Override
			public void run() {

				try {

					if(count < skipcount) {
						count++;
						return;
					}
					
					if(prefs.getBoolean(KEY_WATCH_ENABLE, true) == false)
						return;

					if(!isPingRecent()) {
                        SystemUtils.launchAppNewTask(wts, pkgname, clsname);
                        count = 0;
                        skipcount = 2;
                    }

				} catch(Exception e) {
					e.printStackTrace();
				}
			}
		});
		
		final SharedPreferences.Editor editor = PreferenceManager.getDefaultSharedPreferences(this).edit();
		editor.putBoolean(KEY_WATCH_ENABLE, true);
		editor.apply();
	}

	/*
	 * Binding
	 */
	
	@Override
	public void onCreate() {
		super.onCreate();		
		wts = this;
		serviceStartAt = System.currentTimeMillis();
		prefs = PreferenceManager.getDefaultSharedPreferences(this);
		initTimer();
	}

	@Override
	public boolean onUnbind(Intent intent) {
		return super.onUnbind(intent);
	}

	@Override
	public IBinder onBind(Intent intent) {
		return mBinder;
	}
	
	
	@Override
	public void onRebind(Intent intent) {
		super.onRebind(intent);
	}


	private final IBinder mBinder = new LocalBinder();
	
	public class LocalBinder extends Binder {
		public WatchDogService getService() {
            return WatchDogService.this;
        }
    }	
	
	@Override
	public int onStartCommand(Intent intent, int flags, int startId) {
		if(loaded) {
			if(watchTimer == null)
				initTimer();
			else
				watchTimer.stop();
			
			if(nm != null)
				nm.cancel(NM_ID); 
		}
		loaded = true;
		
		showNotification();
		watchTimer.start();
		return super.onStartCommand(intent, flags, startId);
	}
	
	@Override
	public void onDestroy() {

		if(watchTimer != null)
			watchTimer.stop();

		if(nm != null)
			nm.cancel(NM_ID);

		super.onDestroy();
	}
	
	private NotificationManagerCompat nm;
	private final int NM_ID = 2442;

	private void showNotification() {        

		prefs = PreferenceManager.getDefaultSharedPreferences(this);
		final Context ctx = this;
		
		new Thread(new Runnable() {
			
			@Override
			public void run() {
				boolean enabled = prefs.getBoolean(KEY_WATCH_ENABLE, true);
				
				String title = "WatchDog (Disabled)";
				if(enabled)
					title = "WatchDog (Enabled)";
				
				nm = NotificationManagerCompat.from(ctx);
		        
				Intent customIntent = new Intent("kr.co.turtlelab.watchdog.state");
				PendingIntent contentIntent = PendingIntent.getBroadcast(ctx, 0, customIntent, 0);

				NotificationCompat.Builder noti = new NotificationCompat.Builder(ctx)
					.setContentTitle(title)
		            .setContentText("").setSmallIcon(R.drawable.ic_launcher)
		            .setWhen(0)
		            .setContentIntent(contentIntent)
		            .setOngoing(true);
		                
				nm.notify(NM_ID, noti.build());
			}
		}).start();
	}

	public boolean isPingRecent() {
		if (prefs == null) {
			prefs = PreferenceManager.getDefaultSharedPreferences(this);
		}
		long now = System.currentTimeMillis();
		long lastPing = prefs.getLong(KEY_WATCH_PING, 0);
		if (lastPing <= 0) {
			if (serviceStartAt <= 0) {
				serviceStartAt = now;
			}
			return (now - serviceStartAt) < PING_VALID_MS;
		}
		return (now - lastPing) < PING_VALID_MS;
	}
}
