package kr.co.turtlelab.andowsignage.data.update;

import android.text.TextUtils;

public final class UpdateHeartbeatState {
    private static final long UPDATE_KEEPALIVE_MS = 5000L;
    private static final long UPDATE_REQUEST_MIN_INTERVAL_MS = 500L;
    private static final Object LOCK = new Object();

    private static boolean active = false;
    private static String status = "updating";
    private static int progress = 0;
    private static long lastReportedAtMs = 0L;
    private static long lastRequestedAtMs = 0L;

    private UpdateHeartbeatState() { }

    public static boolean reportProgress(String rawStatus, float rawProgress, boolean force) {
        synchronized (LOCK) {
            long now = System.currentTimeMillis();
            String normalizedStatus = normalizeUpdateStatus(rawStatus);
            int normalizedProgress = normalizeProgress(rawProgress);

            if (!force && active) {
                int currentRank = getStatusRank(status);
                int nextRank = getStatusRank(normalizedStatus);
                if (nextRank < currentRank) {
                    return false;
                }
                if (nextRank == currentRank && normalizedProgress < progress) {
                    return false;
                }
                if (nextRank > currentRank && normalizedProgress < progress) {
                    normalizedProgress = progress;
                }
            }

            boolean sameStatus = TextUtils.equals(status, normalizedStatus);
            boolean sameProgress = progress == normalizedProgress;

            active = true;
            status = normalizedStatus;
            progress = normalizedProgress;

            if (force || !sameStatus || !sameProgress || now - lastRequestedAtMs >= UPDATE_REQUEST_MIN_INTERVAL_MS) {
                lastRequestedAtMs = now;
                return true;
            }
            return false;
        }
    }

    public static boolean reportQueueStatus(String rawStatus, float rawProgress, boolean isScheduleQueue) {
        String normalizedStatus = normalizeUpdateStatus(rawStatus);
        if (isActiveUpdateStatus(normalizedStatus)) {
            return reportProgress(normalizedStatus, rawProgress, false);
        }

        synchronized (LOCK) {
            resetLocked();
        }
        return shouldSendNormalHeartbeatNow(normalizedStatus, isScheduleQueue);
    }

    public static Snapshot captureForPublish(boolean forceUpdateReport) {
        synchronized (LOCK) {
            if (!active) {
                return null;
            }
            long now = System.currentTimeMillis();
            if (!forceUpdateReport && lastReportedAtMs > 0L
                    && now - lastReportedAtMs < UPDATE_KEEPALIVE_MS) {
                return Snapshot.suppress();
            }
            lastReportedAtMs = now;
            return Snapshot.active(status, progress);
        }
    }

    public static void reset() {
        synchronized (LOCK) {
            resetLocked();
        }
    }

    private static void resetLocked() {
        active = false;
        status = "updating";
        progress = 0;
        lastReportedAtMs = 0L;
        lastRequestedAtMs = 0L;
    }

    private static boolean shouldSendNormalHeartbeatNow(String normalizedStatus, boolean isScheduleQueue) {
        return !"done".equals(normalizedStatus) || isScheduleQueue;
    }

    private static String normalizeUpdateStatus(String rawStatus) {
        if (TextUtils.isEmpty(rawStatus)) {
            return "updating";
        }
        return rawStatus.trim().toLowerCase();
    }

    private static int normalizeProgress(float rawProgress) {
        return Math.max(0, Math.min(100, Math.round(rawProgress)));
    }

    private static boolean isActiveUpdateStatus(String rawStatus) {
        if (TextUtils.isEmpty(rawStatus)) {
            return false;
        }
        String normalizedStatus = rawStatus.trim().toLowerCase();
        return "queued".equals(normalizedStatus)
                || "downloading".equals(normalizedStatus)
                || "downloaded".equals(normalizedStatus)
                || "validating".equals(normalizedStatus)
                || "ready".equals(normalizedStatus)
                || "applying".equals(normalizedStatus)
                || "updating".equals(normalizedStatus);
    }

    private static int getStatusRank(String rawStatus) {
        if (TextUtils.isEmpty(rawStatus)) {
            return -1;
        }
        String normalizedStatus = rawStatus.trim().toLowerCase();
        if ("queued".equals(normalizedStatus)) {
            return 0;
        }
        if ("downloading".equals(normalizedStatus) || "updating".equals(normalizedStatus)) {
            return 1;
        }
        if ("downloaded".equals(normalizedStatus)) {
            return 2;
        }
        if ("validating".equals(normalizedStatus)) {
            return 3;
        }
        if ("ready".equals(normalizedStatus)) {
            return 4;
        }
        if ("applying".equals(normalizedStatus)) {
            return 5;
        }
        if ("done".equals(normalizedStatus)) {
            return 6;
        }
        return -1;
    }

    public static final class Snapshot {
        public final String status;
        public final int progress;
        public final boolean suppressNormalHeartbeat;

        private Snapshot(String status, int progress, boolean suppressNormalHeartbeat) {
            this.status = status;
            this.progress = progress;
            this.suppressNormalHeartbeat = suppressNormalHeartbeat;
        }

        public static Snapshot active(String status, int progress) {
            return new Snapshot(status, progress, false);
        }

        public static Snapshot suppress() {
            return new Snapshot(null, 0, true);
        }
    }
}
