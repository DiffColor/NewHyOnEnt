package kr.co.turtlelab.andowsignage.tools;

import android.text.TextUtils;

import java.util.Calendar;

import kr.co.turtlelab.andowsignage.data.objectbox.ObjectBoxDb;
import kr.co.turtlelab.andowsignage.data.store.StoredContentPeriod;

public final class ContentPeriodEvaluator {

    private ContentPeriodEvaluator() {
    }

    public static boolean hasPeriod(ObjectBoxDb storeDb, String contentGuid) {
        if (storeDb == null || TextUtils.isEmpty(contentGuid)) {
            return false;
        }

        return storeDb.where(StoredContentPeriod.class)
                .equalTo("contentGuid", contentGuid)
                .findFirst() != null;
    }

    public static boolean isAllowed(ObjectBoxDb storeDb, String contentGuid, long nowMillis) {
        if (storeDb == null || TextUtils.isEmpty(contentGuid)) {
            return true;
        }

        StoredContentPeriod period = storeDb.where(StoredContentPeriod.class)
                .equalTo("contentGuid", contentGuid)
                .findFirst();
        if (period == null) {
            return true;
        }

        Calendar now = Calendar.getInstance();
        now.setTimeInMillis(nowMillis);
        int currentDate = toDateValue(now.get(Calendar.YEAR), now.get(Calendar.MONTH) + 1, now.get(Calendar.DAY_OF_MONTH));

        int startDate = parseDateValue(period.getStartDate());
        if (startDate > 0 && currentDate < startDate) {
            return false;
        }

        int endDate = parseDateValue(period.getEndDate());
        if (endDate > 0 && currentDate > endDate) {
            return false;
        }

        int startMinutes = parseTimeMinutes(period.getStartTime());
        int endMinutes = parseTimeMinutes(period.getEndTime());
        if (startMinutes < 0 || endMinutes < 0) {
            return true;
        }

        int currentMinutes = (now.get(Calendar.HOUR_OF_DAY) * 60) + now.get(Calendar.MINUTE);
        if (endMinutes < startMinutes) {
            return currentMinutes >= startMinutes || currentMinutes < endMinutes;
        }

        return currentMinutes >= startMinutes && currentMinutes < endMinutes;
    }

    private static int parseDateValue(String raw) {
        if (TextUtils.isEmpty(raw)) {
            return -1;
        }
        String[] parts = raw.trim().split("-");
        if (parts.length != 3) {
            return -1;
        }
        try {
            return toDateValue(Integer.parseInt(parts[0]), Integer.parseInt(parts[1]), Integer.parseInt(parts[2]));
        } catch (Exception ignore) {
            return -1;
        }
    }

    private static int parseTimeMinutes(String raw) {
        if (TextUtils.isEmpty(raw)) {
            return -1;
        }
        String[] parts = raw.trim().split(":");
        if (parts.length != 2) {
            return -1;
        }
        try {
            return (Integer.parseInt(parts[0]) * 60) + Integer.parseInt(parts[1]);
        } catch (Exception ignore) {
            return -1;
        }
    }

    private static int toDateValue(int year, int month, int day) {
        return (year * 10000) + (month * 100) + day;
    }
}
