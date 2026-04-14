package kr.co.turtlelab.andowsignage.receivers;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;

import kr.co.turtlelab.andowsignage.AndoWSignage;

public class WakeOrSleepReceiver extends BroadcastReceiver {

	@Override
	public void onReceive(Context context, Intent intent) {
		if (intent.getAction().equals("andowsignage.intent.action.WAKEUP")){
			Intent wakeIntent = new Intent();
			wakeIntent.setAction("rk.android.wakeupmode.action");
			context.sendBroadcast(wakeIntent);

			Intent playerIntent = new Intent(context, AndoWSignage.class);
			playerIntent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
			playerIntent.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TOP);
			playerIntent.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TASK);
			context.startActivity(playerIntent);
		} else if(intent.getAction().equals("andowsignage.intent.action.SLEEP")) {
			Intent _intent = new Intent();
			_intent.setAction("andowsignage.intent.action.STOP_AND_SLEEP");
            context.sendBroadcast(_intent);
		}
	}
}
