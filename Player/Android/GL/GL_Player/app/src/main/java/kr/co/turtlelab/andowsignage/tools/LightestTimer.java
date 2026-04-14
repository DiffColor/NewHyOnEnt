package kr.co.turtlelab.andowsignage.tools;

import android.os.Handler;
import android.os.Looper;
import android.os.SystemClock;

public class LightestTimer {

	private long intervalMs;
	long getInterval() { return intervalMs; }
	void setInterval(int delay) { intervalMs = normalizeInterval(delay); }
	private final Handler handler;
	private Runnable tickHandler;
	private boolean ticking;
	private long nextTickAtUptimeMs = -1L;
	private final Runnable delegate = new Runnable() {
		@Override
		public void run() {
			Runnable localTickHandler;
			synchronized (LightestTimer.this) {
				if (!ticking) {
					return;
				}
				localTickHandler = tickHandler;
			}

			if (localTickHandler != null) {
				localTickHandler.run();
			}

			synchronized (LightestTimer.this) {
				if (!ticking) {
					return;
				}
				scheduleNextLocked(true);
			}
		}
	};

	public synchronized boolean getIsTicking(){ return ticking; }

	public LightestTimer(int interval)
	{
		intervalMs = normalizeInterval(interval);
		handler = new Handler(Looper.getMainLooper());
	}

	public LightestTimer(Runnable onTickHandler) {
		handler = new Handler(Looper.getMainLooper());
		setOnTickHandler(onTickHandler);
	}

	public LightestTimer(int interval, Runnable onTickHandler)
	{
		intervalMs = normalizeInterval(interval);
		handler = new Handler(Looper.getMainLooper());
		setOnTickHandler(onTickHandler);
	}

	public synchronized void start(int interval, Runnable onTickHandler)
	{
		intervalMs = normalizeInterval(interval);
		setOnTickHandler(onTickHandler);
		start();
	}

	public synchronized void start()
	{
		if (ticking || tickHandler == null) {
			return;
		}

		ticking = true;
		scheduleNextLocked(false);
	}

	public synchronized void stop()
	{
		ticking = false;
		nextTickAtUptimeMs = -1L;
		handler.removeCallbacks(delegate);
	}

	public synchronized void changeInterval(long interval) {
		intervalMs = normalizeInterval(interval);
		if (!ticking) {
			return;
		}
		handler.removeCallbacks(delegate);
		scheduleNextLocked(false);
	}

	public synchronized void setOnTickHandler(Runnable onTickHandler)
	{
	    tickHandler = onTickHandler;
	}

	private void scheduleNextLocked(boolean keepPhase) {
		long now = SystemClock.uptimeMillis();
		if (!keepPhase || nextTickAtUptimeMs < 0L) {
			nextTickAtUptimeMs = now + intervalMs;
		} else {
			do {
				nextTickAtUptimeMs += intervalMs;
			} while (nextTickAtUptimeMs <= now);
		}
		handler.postAtTime(delegate, nextTickAtUptimeMs);
	}

	private long normalizeInterval(long interval) {
		return Math.max(1L, interval);
	}
}
