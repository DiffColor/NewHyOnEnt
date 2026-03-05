package com.github.postapczuk.lalauncher;

import android.annotation.SuppressLint;
import android.app.Activity;
import android.app.AlertDialog;
import android.content.ComponentName;
import android.content.DialogInterface;
import android.content.Intent;
import android.content.SharedPreferences;
import android.content.pm.ApplicationInfo;
import android.content.pm.PackageManager;
import android.content.pm.ResolveInfo;
import android.os.Build;
import android.os.Bundle;
import android.os.Handler;
import android.text.TextUtils;
import android.view.KeyEvent;
import android.view.View;
import android.view.ViewGroup;
import android.view.Window;
import android.view.WindowManager;
import android.widget.ArrayAdapter;
import android.widget.ListView;
import android.widget.TextView;
import android.widget.Toast;

import java.lang.reflect.Field;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collections;
import java.util.List;

import java8.util.Comparators;

import static android.view.WindowManager.LayoutParams.FLAG_LAYOUT_NO_LIMITS;

public class FavoriteAppsActivity extends Activity {

    private static final String FAVS = "favorites";
    private static final String SEPARATOR = ",,,";

//    private static final String START_INTENT = "kr.co.turtlelab.startnow";

    public static FavoriteAppsActivity act;

    private List<String> packageNames = new ArrayList<>();
    private ArrayAdapter<String> adapter;
    private android.widget.ListView listView;

    private SharedPreferences preferences;
    private List<String> favorites = new ArrayList<String>();

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
//        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.ICE_CREAM_SANDWICH) {
//            getWindow().getDecorView().setSystemUiVisibility(
//                    SYSTEM_UI_FLAG_LOW_PROFILE);
//        }

        act = FavoriteAppsActivity.this;

        final android.view.View decorView = getWindow().getDecorView();
        decorView.setOnSystemUiVisibilityChangeListener
                (new android.view.View.OnSystemUiVisibilityChangeListener() {
                    @Override
                    public void onSystemUiVisibilityChange(int visibility) {
                        if(visibility == 0)
                            return;
                        hide();
                    }
                });

        getWindow().setFlags(
                FLAG_LAYOUT_NO_LIMITS,
                FLAG_LAYOUT_NO_LIMITS);

        setContentView(R.layout.activity_favorites);

        loadFavoritesFromPreferences();
        adapter = createNewAdapter();
        listView = (ListView) findViewById(R.id.mobile_list);
        listView.setAdapter(adapter);
        fetchAppList();
        AttitudeHelper.applyPadding(listView, ScreenUtils.getDisplay(getApplicationContext()));

        hide();

//        sendStartIntent();
        startNowApps();
    }

//    void sendStartIntent() {
//        Intent startIntent = new Intent();
//        startIntent.setAction(START_INTENT);
//        sendBroadcast(startIntent);
//    }

    public void hide() {
        new Handler().postDelayed(new Runnable()
        {
            @Override
            public void run()
            {
                setDimButtons(act, true);
                systemBarVisibility(act, false);
            }}, 500);
    }

    public static void setDimButtons(Activity act, boolean dimButtons) {
        Window window = act.getWindow();
        WindowManager.LayoutParams layoutParams = window.getAttributes();
        float val = dimButtons ? 0 : -1;

        try {
            Field buttonBrightness = layoutParams.getClass().getField("buttonBrightness");
            buttonBrightness.set(layoutParams, val);
        } catch (Exception e) {
            e.printStackTrace();
        }

        window.setAttributes(layoutParams);
    }

    public static void systemBarVisibility(Activity act, boolean visible) {

        int hideState = act.getWindow().getDecorView().getSystemUiVisibility();
        if( android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.ICE_CREAM_SANDWICH )
            hideState |= android.view.View.SYSTEM_UI_FLAG_HIDE_NAVIGATION;
        if( android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.JELLY_BEAN )
            hideState |= android.view.View.SYSTEM_UI_FLAG_FULLSCREEN;

        if( android.os.Build.VERSION.SDK_INT >= 19 )
            hideState |= 4096;

        if(android.os.Build.VERSION.SDK_INT < 19)
            hideState = android.view.View.SYSTEM_UI_FLAG_LOW_PROFILE;

        int showState = android.view.View.SYSTEM_UI_FLAG_VISIBLE;

        if(visible) {
            act.getWindow().getDecorView().setSystemUiVisibility(showState);
        } else {
            act.getWindow().getDecorView().setSystemUiVisibility(hideState);
        }
    }

    @Override
    protected void onRestart() {
        super.onRestart();
        hide();
    }

    @Override
    protected void onResume() {
        super.onResume();
    }

    private void loadFavoritesFromPreferences() {
        preferences = getSharedPreferences("light-phone-launcher", 0);
        favorites = Arrays.asList(preferences.getString(FAVS, "").split(SEPARATOR));
        if (favorites.size() == 1 && favorites.get(0) == "") {
            favorites = new ArrayList<>();
        }
    }

    private ArrayAdapter<String> createNewAdapter() {
        return new ArrayAdapter<String>(
                this,
                R.layout.activity_listview,
                new ArrayList<>()
        ) {
            @Override
            public View getView(int position, View convertView, ViewGroup parent) {
                TextView view = (TextView) super.getView(position, convertView, parent);
                setTextColoring(view);
                if (position == getCount() - 1 && position != 0) {
                    view.setTextColor(getResources().getColor(R.color.colorTextDimmed));
                }
                return view;
            }
        };
    }

    private void fetchAppList() {
        adapter.clear();

        List<ComponentName> componentNames = new ArrayList<>();

        for (String option : favorites) {
            componentNames.add(new ComponentName(option, option));
        }

        List<String> apps = new ArrayList<>();
        for (ComponentName componentName : componentNames) {
            apps.add(getApplicationLabel(componentName));
        }

//        apps.add(apps.size() > 0 ? "+" : "+ Add favorites");

        packageNames = new ArrayList<>();
        for (ComponentName componentName : componentNames) {
            String packageName = componentName.getPackageName();
            packageNames.add(packageName);
        }
        for (String app : apps) {
            adapter.add(app);
        }
        setActions();
    }

    private String getApplicationLabel(ComponentName componentName) {
        try {
            ApplicationInfo applicationInfo = getPackageManager().getApplicationInfo(componentName.getPackageName(), 0);
            return getPackageManager().getApplicationLabel(applicationInfo).toString();
        } catch (PackageManager.NameNotFoundException e) {
            return "Uninstalled app";
        }
    }

    private void setTextColoring(TextView text) {
        text.setTextColor(getResources().getColor(R.color.colorTextPrimary));
        text.setHighlightColor(getResources().getColor(R.color.colorTextPrimary));
    }

    private void setActions() {
        onClickHandler(listView);
        onLongPressHandler(listView);
        onSwipeHandler(listView);
    }

    private void onClickHandler(ListView listView) {
        listView.setOnItemClickListener((parent, view, position, id) -> {
            toggleTextViewBackground(view, 100L);

//            if (position == packageNames.size() || packageNames.get(position).equals("")) {
//                showFavoriteModal();
//                return;
//            }
            String packageName = packageNames.get(position);
            try {
                startActivity(getPackageManager().getLaunchIntentForPackage(packageName));
            } catch (Exception e) {
                Toast.makeText(
                        this,
                        String.format("Error: Couldn't launch app: %s", packageName),
                        Toast.LENGTH_LONG
                ).show();
            }
        });
    }

    @Override
    public boolean onKeyDown(int keyCode, KeyEvent event) {
        switch(keyCode) {

            case KeyEvent.KEYCODE_S:
                if(event.isAltPressed()) {
                    startActivityForResult(new Intent(android.provider.Settings.ACTION_SETTINGS), 0);
                }
                break;

            case KeyEvent.KEYCODE_A:
                if(event.isAltPressed()) {
                    showFavoriteModal();
                }
                break;

//            case KeyEvent.KEYCODE_Q:
//                if(event.isAltPressed()) {
//                    startActivity(new Intent(getBaseContext(), InstalledAppsActivity.class));
//                    overridePendingTransition(R.anim.slide_up, android.R.anim.fade_out);
//                }
//                break;

            case KeyEvent.KEYCODE_POWER :
            case KeyEvent.KEYCODE_HOME :
            case KeyEvent.KEYCODE_MENU :
            case KeyEvent.KEYCODE_APP_SWITCH :
            case KeyEvent.KEYCODE_BACK :
            default:
                break;
        }
        return super.onKeyDown(keyCode, event);
    }

    private void onLongPressHandler(ListView listView) {
        listView.setOnItemLongClickListener((parent, view, position, id) -> {
            toggleTextViewBackground(view, 350L);
            FavoriteAppsActivity favoriteAppsActivity = this;

            runOnUiThread(() -> new Handler().postDelayed(() -> {
                        AlertDialog alertDialog = new AlertDialog.Builder(this).create();
                        alertDialog.setTitle("Favorite app removal");
                        alertDialog.setMessage("Do you want to remove this application from your favorites?");
                        alertDialog.setButton(DialogInterface.BUTTON_POSITIVE, "Yes", new DialogInterface.OnClickListener() {
                            public void onClick(DialogInterface dialog, int which) {
                                favoriteAppsActivity.removeFavorite(position);
                                Toast.makeText(getApplicationContext(), "Success", Toast.LENGTH_SHORT).show();
                            }
                        });
                        alertDialog.show();


                    }
                    , 350L));
            return true;
        });
    }

    @SuppressLint("ClickableViewAccessibility")
    private void onSwipeHandler(ListView listView) {
        listView.setOnTouchListener(new OnSwipeTouchListenerMain(this) {
            public void onSwipeTop() {
                onBackPressed();
            }
        });
    }

    @Override
    public void onBackPressed() {
//        startActivity(new Intent(getBaseContext(), InstalledAppsActivity.class));
//        overridePendingTransition(R.anim.slide_up, android.R.anim.fade_out);
    }

    private void showFavoriteModal() {
        AlertDialog.Builder builder = new AlertDialog.Builder(this);
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP_MR1) {
            builder = new AlertDialog.Builder(this, android.R.style.Theme_DeviceDefault_Dialog_Alert);
        }
        builder.setTitle("Add favorite app");

        List<String> smallAdapter = new ArrayList<>();
        List<String> smallPackageNames = new ArrayList<>();

        List<ResolveInfo> activities = getActivities();
        Collections.sort(activities, Comparators.comparing(pm -> pm.loadLabel(getPackageManager()).toString().toLowerCase()));


        for (ResolveInfo resolver : activities) {
            String appName = (String) resolver.loadLabel(getPackageManager());
            if (appName.equals("Light Android Launcher") || favorites.contains(resolver.activityInfo.packageName))
                continue;
            smallAdapter.add(appName);
            smallPackageNames.add(resolver.activityInfo.packageName);
        }

        builder.setItems(smallAdapter.toArray(new CharSequence[smallAdapter.size()]), (dialog, which) -> {
            setFavorite(smallPackageNames, which);
            fetchAppList();
        });
        builder.show();
    }

    void startNowApps() {
        List<ResolveInfo> activities = getActivities();
        for (ResolveInfo resolver : activities) {
            String _pkgName = resolver.activityInfo.packageName;
            if(_pkgName.contains("startnow"))
                startActivity(getPackageManager().getLaunchIntentForPackage(_pkgName));
        }
    }

    private List<ResolveInfo> getActivities() {
        Intent intent = new Intent(Intent.ACTION_MAIN, null)
                .addCategory(Intent.CATEGORY_LAUNCHER);
        List<ResolveInfo> activities = getPackageManager().queryIntentActivities(intent, 0);
        Collections.sort(activities, new ResolveInfo.DisplayNameComparator(getPackageManager()));
        return activities;
    }

    private void updateFavoritesInPreferences() {
        if (packageNames.isEmpty()) {
            preferences.edit().putString(FAVS, "").commit();
        } else {
            preferences.edit().putString(FAVS, TextUtils.join(SEPARATOR, packageNames)).commit();
        }
    }

    private void setFavorite(List<String> smallPackageNames, int which) {
        packageNames.add(smallPackageNames.get(which));
        updateFavoritesInPreferences();
        loadListView();
    }

    private void removeFavorite(int position) {
        if (position < packageNames.size()) {
            packageNames.remove(position);
            updateFavoritesInPreferences();
            loadListView();
        }
    }

    private void loadListView() {
        loadFavoritesFromPreferences();
        fetchAppList();
        AttitudeHelper.applyPadding(listView, ScreenUtils.getDisplay(getApplicationContext()));
    }

    private void toggleTextViewBackground(View selectedItem, Long millis) {
        selectedItem.setBackgroundColor(getResources().getColor(R.color.colorBackgroundFavorite));
        new Handler().postDelayed(() -> selectedItem.setBackgroundColor(getResources().getColor(R.color.colorTransparent)), millis);
    }
}
