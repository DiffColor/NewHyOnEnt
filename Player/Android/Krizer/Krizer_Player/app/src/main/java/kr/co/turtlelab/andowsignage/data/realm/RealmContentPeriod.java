package kr.co.turtlelab.andowsignage.data.realm;

import io.realm.RealmObject;
import io.realm.annotations.PrimaryKey;

public class RealmContentPeriod extends RealmObject {

    @PrimaryKey
    private String contentGuid;
    private String fileName;
    private String startDate;
    private String endDate;
    private String startTime;
    private String endTime;

    public String getContentGuid() {
        return contentGuid;
    }

    public void setContentGuid(String contentGuid) {
        this.contentGuid = contentGuid;
    }

    public String getFileName() {
        return fileName;
    }

    public void setFileName(String fileName) {
        this.fileName = fileName;
    }

    public String getStartDate() {
        return startDate;
    }

    public void setStartDate(String startDate) {
        this.startDate = startDate;
    }

    public String getEndDate() {
        return endDate;
    }

    public void setEndDate(String endDate) {
        this.endDate = endDate;
    }

    public String getStartTime() {
        return startTime;
    }

    public void setStartTime(String startTime) {
        this.startTime = startTime;
    }

    public String getEndTime() {
        return endTime;
    }

    public void setEndTime(String endTime) {
        this.endTime = endTime;
    }
}
