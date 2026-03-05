package com.github.postapczuk.lalauncher;

import android.content.BroadcastReceiver;
import android.content.ComponentName;
import android.content.Context;
import android.content.Intent;
import android.text.TextUtils;
import android.util.Log;

public class LaunchRequestReceiver extends BroadcastReceiver {

    public static final String ACTION_LAUNCH_COMPONENT = "kr.co.turtlelab.turtlelauncher.action.LAUNCH_COMPONENT";
    public static final String EXTRA_TARGET_PACKAGE = "target_package";
    public static final String EXTRA_TARGET_CLASS = "target_class";
    public static final String EXTRA_CLEAR_TASK = "clear_task";

    private static final String TAG = "LaunchRequestReceiver";

    @Override
    public void onReceive(Context context, Intent intent) {
        if (intent == null || !ACTION_LAUNCH_COMPONENT.equals(intent.getAction())) {
            return;
        }

        String packageName = firstNonEmpty(
                intent.getStringExtra(EXTRA_TARGET_PACKAGE),
                intent.getStringExtra("packageName"),
                intent.getStringExtra("package_name"),
                intent.getStringExtra("pkgname")
        );
        String className = firstNonEmpty(
                intent.getStringExtra(EXTRA_TARGET_CLASS),
                intent.getStringExtra("className"),
                intent.getStringExtra("class_name"),
                intent.getStringExtra("activity_path"),
                intent.getStringExtra("activity_class"),
                intent.getStringExtra("classname")
        );
        boolean clearTask = intent.getBooleanExtra(EXTRA_CLEAR_TASK, false);

        if (TextUtils.isEmpty(packageName)) {
            Log.w(TAG, "Missing target package name.");
            return;
        }

        try {
            Intent launchIntent;
            if (!TextUtils.isEmpty(className)) {
                launchIntent = new Intent();
                launchIntent.setComponent(new ComponentName(packageName, className));
            } else {
                launchIntent = context.getPackageManager().getLaunchIntentForPackage(packageName);
                if (launchIntent == null) {
                    Log.w(TAG, "Launch intent not found for package: " + packageName);
                    return;
                }
            }

            if (clearTask) {
                launchIntent.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP);
                launchIntent.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TASK);
            }
            launchIntent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
            context.startActivity(launchIntent);
        } catch (Exception e) {
            Log.e(TAG, "Failed to launch requested component.", e);
        }
    }

    private static String firstNonEmpty(String... values) {
        if (values == null) {
            return null;
        }
        for (String value : values) {
            if (!TextUtils.isEmpty(value)) {
                return value;
            }
        }
        return null;
    }
}
