package kr.co.turtlelab.startnow.watchdog;

import android.app.Activity;
import android.content.Intent;
import android.os.Bundle;

public class BlankActivity extends Activity {

    public static String pkgname = "kr.co.turtlelab.andowsignage";
    public static String clsname = "AndoWSignage";

	@Override
	protected void onCreate(Bundle savedInstanceState) {
		super.onCreate(savedInstanceState);

        SystemUtils.launchAppNewTask(this, pkgname, clsname);

		startService(new Intent(this, WatchDogService.class));
		finish();
	}
}
