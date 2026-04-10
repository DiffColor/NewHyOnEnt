package kr.co.turtlelab.andowsignage.data.store;

import java.util.Locale;

import io.objectbox.annotation.Entity;
import io.objectbox.annotation.Id;
import io.objectbox.annotation.Unique;
import kr.co.turtlelab.andowsignage.data.objectbox.BusinessId;

@Entity
public class StoredWeeklySchedule {

    @Id
    private long objectBoxId;

    @BusinessId
    @Unique
    private String playerId;

    public long getObjectBoxId() {
        return objectBoxId;
    }

    public void setObjectBoxId(long objectBoxId) {
        this.objectBoxId = objectBoxId;
    }

    public String getPlayerId() {
        return playerId;
    }

    public void setPlayerId(String playerId) {
        this.playerId = playerId;
    }

    private int monStartHour;
    private int monStartMinute;
    private int monEndHour;
    private int monEndMinute;
    private boolean monOnAir = true;

    private int tueStartHour;
    private int tueStartMinute;
    private int tueEndHour;
    private int tueEndMinute;
    private boolean tueOnAir = true;

    private int wedStartHour;
    private int wedStartMinute;
    private int wedEndHour;
    private int wedEndMinute;
    private boolean wedOnAir = true;

    private int thuStartHour;
    private int thuStartMinute;
    private int thuEndHour;
    private int thuEndMinute;
    private boolean thuOnAir = true;

    private int friStartHour;
    private int friStartMinute;
    private int friEndHour;
    private int friEndMinute;
    private boolean friOnAir = true;

    private int satStartHour;
    private int satStartMinute;
    private int satEndHour;
    private int satEndMinute;
    private boolean satOnAir = true;

    private int sunStartHour;
    private int sunStartMinute;
    private int sunEndHour;
    private int sunEndMinute;
    private boolean sunOnAir = true;

    public void setSchedule(String day, int startHour, int startMinute, int endHour, int endMinute) {
        day = normalize(day);
        switch (day) {
            case "MON":
                monStartHour = startHour;
                monStartMinute = startMinute;
                monEndHour = endHour;
                monEndMinute = endMinute;
                break;
            case "TUE":
                tueStartHour = startHour;
                tueStartMinute = startMinute;
                tueEndHour = endHour;
                tueEndMinute = endMinute;
                break;
            case "WED":
                wedStartHour = startHour;
                wedStartMinute = startMinute;
                wedEndHour = endHour;
                wedEndMinute = endMinute;
                break;
            case "THU":
                thuStartHour = startHour;
                thuStartMinute = startMinute;
                thuEndHour = endHour;
                thuEndMinute = endMinute;
                break;
            case "FRI":
                friStartHour = startHour;
                friStartMinute = startMinute;
                friEndHour = endHour;
                friEndMinute = endMinute;
                break;
            case "SAT":
                satStartHour = startHour;
                satStartMinute = startMinute;
                satEndHour = endHour;
                satEndMinute = endMinute;
                break;
            case "SUN":
                sunStartHour = startHour;
                sunStartMinute = startMinute;
                sunEndHour = endHour;
                sunEndMinute = endMinute;
                break;
        }
    }

    public int getStartHour(String day) {
        day = normalize(day);
        switch (day) {
            case "MON": return monStartHour;
            case "TUE": return tueStartHour;
            case "WED": return wedStartHour;
            case "THU": return thuStartHour;
            case "FRI": return friStartHour;
            case "SAT": return satStartHour;
            case "SUN": return sunStartHour;
            default: return 0;
        }
    }

    public int getStartMinute(String day) {
        day = normalize(day);
        switch (day) {
            case "MON": return monStartMinute;
            case "TUE": return tueStartMinute;
            case "WED": return wedStartMinute;
            case "THU": return thuStartMinute;
            case "FRI": return friStartMinute;
            case "SAT": return satStartMinute;
            case "SUN": return sunStartMinute;
            default: return 0;
        }
    }

    public int getEndHour(String day) {
        day = normalize(day);
        switch (day) {
            case "MON": return monEndHour;
            case "TUE": return tueEndHour;
            case "WED": return wedEndHour;
            case "THU": return thuEndHour;
            case "FRI": return friEndHour;
            case "SAT": return satEndHour;
            case "SUN": return sunEndHour;
            default: return 0;
        }
    }

    public int getEndMinute(String day) {
        day = normalize(day);
        switch (day) {
            case "MON": return monEndMinute;
            case "TUE": return tueEndMinute;
            case "WED": return wedEndMinute;
            case "THU": return thuEndMinute;
            case "FRI": return friEndMinute;
            case "SAT": return satEndMinute;
            case "SUN": return sunEndMinute;
            default: return 0;
        }
    }

    public boolean isOnAir(String day) {
        day = normalize(day);
        switch (day) {
            case "MON": return monOnAir;
            case "TUE": return tueOnAir;
            case "WED": return wedOnAir;
            case "THU": return thuOnAir;
            case "FRI": return friOnAir;
            case "SAT": return satOnAir;
            case "SUN": return sunOnAir;
            default: return true;
        }
    }

    public void setOnAir(String day, boolean onAir) {
        day = normalize(day);
        switch (day) {
            case "MON": monOnAir = onAir; break;
            case "TUE": tueOnAir = onAir; break;
            case "WED": wedOnAir = onAir; break;
            case "THU": thuOnAir = onAir; break;
            case "FRI": friOnAir = onAir; break;
            case "SAT": satOnAir = onAir; break;
            case "SUN": sunOnAir = onAir; break;
        }
    }

    public int getMonStartHour() { return monStartHour; }
    public int getMonStartMinute() { return monStartMinute; }
    public int getMonEndHour() { return monEndHour; }
    public int getMonEndMinute() { return monEndMinute; }
    public boolean getMonOnAir() { return monOnAir; }
    public int getTueStartHour() { return tueStartHour; }
    public int getTueStartMinute() { return tueStartMinute; }
    public int getTueEndHour() { return tueEndHour; }
    public int getTueEndMinute() { return tueEndMinute; }
    public boolean getTueOnAir() { return tueOnAir; }
    public int getWedStartHour() { return wedStartHour; }
    public int getWedStartMinute() { return wedStartMinute; }
    public int getWedEndHour() { return wedEndHour; }
    public int getWedEndMinute() { return wedEndMinute; }
    public boolean getWedOnAir() { return wedOnAir; }
    public int getThuStartHour() { return thuStartHour; }
    public int getThuStartMinute() { return thuStartMinute; }
    public int getThuEndHour() { return thuEndHour; }
    public int getThuEndMinute() { return thuEndMinute; }
    public boolean getThuOnAir() { return thuOnAir; }
    public int getFriStartHour() { return friStartHour; }
    public int getFriStartMinute() { return friStartMinute; }
    public int getFriEndHour() { return friEndHour; }
    public int getFriEndMinute() { return friEndMinute; }
    public boolean getFriOnAir() { return friOnAir; }
    public int getSatStartHour() { return satStartHour; }
    public int getSatStartMinute() { return satStartMinute; }
    public int getSatEndHour() { return satEndHour; }
    public int getSatEndMinute() { return satEndMinute; }
    public boolean getSatOnAir() { return satOnAir; }
    public int getSunStartHour() { return sunStartHour; }
    public int getSunStartMinute() { return sunStartMinute; }
    public int getSunEndHour() { return sunEndHour; }
    public int getSunEndMinute() { return sunEndMinute; }
    public boolean getSunOnAir() { return sunOnAir; }

    private String normalize(String day) {
        return day == null ? "" : day.toUpperCase(Locale.US);
    }
}
