package kr.co.turtlelab.andowsignage.dataproviders;

import java.util.ArrayList;
import java.util.List;

import kr.co.turtlelab.andowsignage.AndoWSignageApp;
import kr.co.turtlelab.andowsignage.data.objectbox.ObjectBoxDb;
import kr.co.turtlelab.andowsignage.data.store.StoredWeeklySchedule;
import kr.co.turtlelab.andowsignage.datamodels.WeeklyScheduleDataModel;

public class WeeklyScheduleProvider {

    private static final String[] DAYS = {
            "MON", "TUE", "WED", "THU", "FRI", "SAT", "SUN"
    };

    private WeeklyScheduleProvider() {
    }

    public static List<WeeklyScheduleDataModel> getWeeklyScheduleList() {
        List<WeeklyScheduleDataModel> list = new ArrayList<>();
        ObjectBoxDb storeDb = ObjectBoxDb.getDefaultInstance();
        try {
            StoredWeeklySchedule schedule = storeDb.where(StoredWeeklySchedule.class)
                    .equalTo("playerId", AndoWSignageApp.PLAYER_ID)
                    .findFirst();
            if (schedule == null) {
                storeDb.executeTransaction(r -> ensureScheduleInTransaction(r));
                schedule = storeDb.where(StoredWeeklySchedule.class)
                        .equalTo("playerId", AndoWSignageApp.PLAYER_ID)
                        .findFirst();
            }
            if (schedule == null) {
                return list;
            }
            StoredWeeklySchedule detached = storeDb.copyEntity(schedule);
            for (String day : DAYS) {
                addModel(list, detached, day);
            }
        } finally {
            storeDb.close();
        }
        return list;
    }

    public static void updateFromTime(String day, String hour, String minute) {
        updateDay(day, true, hour, minute);
    }

    public static void updateToTime(String day, String hour, String minute) {
        updateDay(day, false, hour, minute);
    }

    public static void updateIsOnAir(String day, boolean isOnAir) {
        ObjectBoxDb storeDb = ObjectBoxDb.getDefaultInstance();
        storeDb.executeTransaction(r -> {
            StoredWeeklySchedule schedule = ensureScheduleInTransaction(r);
            if (schedule == null) {
                return;
            }
            schedule.setOnAir(day, isOnAir);
        });
        storeDb.close();
    }

    private static void updateDay(String day, boolean isFrom, String hour, String minute) {
        ObjectBoxDb storeDb = ObjectBoxDb.getDefaultInstance();
        storeDb.executeTransaction(r -> {
            StoredWeeklySchedule schedule = ensureScheduleInTransaction(r);
            if (schedule == null) {
                return;
            }
            int h = safeParse(hour);
            int m = safeParse(minute);
            int startHour = schedule.getStartHour(day);
            int startMinute = schedule.getStartMinute(day);
            int endHour = schedule.getEndHour(day);
            int endMinute = schedule.getEndMinute(day);
            if (isFrom) {
                schedule.setSchedule(day, h, m, endHour, endMinute);
            } else {
                schedule.setSchedule(day, startHour, startMinute, h, m);
            }
        });
        storeDb.close();
    }

    private static void addModel(List<WeeklyScheduleDataModel> list,
                                 StoredWeeklySchedule schedule,
                                 String day) {
        WeeklyScheduleDataModel model = new WeeklyScheduleDataModel();
        model.setDay(day);
        model.setFrom(String.valueOf(schedule.getStartHour(day)), String.valueOf(schedule.getStartMinute(day)));
        model.setTo(String.valueOf(schedule.getEndHour(day)), String.valueOf(schedule.getEndMinute(day)));
        model.setOnAir(String.valueOf(schedule.isOnAir(day)));
        list.add(model);
    }

    private static StoredWeeklySchedule ensureScheduleInTransaction(ObjectBoxDb storeDb) {
        if (storeDb == null) {
            return null;
        }
        StoredWeeklySchedule schedule = storeDb.where(StoredWeeklySchedule.class)
                .equalTo("playerId", AndoWSignageApp.PLAYER_ID)
                .findFirst();
        if (schedule != null) {
            return schedule;
        }
        if (AndoWSignageApp.PLAYER_ID == null) {
            return null;
        }
        schedule = storeDb.createObject(StoredWeeklySchedule.class, AndoWSignageApp.PLAYER_ID);
        applyDefaultSchedule(schedule);
        return schedule;
    }

    private static void applyDefaultSchedule(StoredWeeklySchedule schedule) {
        if (schedule == null) {
            return;
        }
        for (String day : DAYS) {
            schedule.setSchedule(day, 0, 0, 0, 0);
            schedule.setOnAir(day, true);
        }
    }

    private static int safeParse(String value) {
        try {
            return Integer.parseInt(value);
        } catch (Exception e) {
            return 0;
        }
    }
}
