package kr.co.turtlelab.andowsignage.data.store;

import io.objectbox.annotation.Entity;
import io.objectbox.annotation.Id;
import io.objectbox.annotation.Unique;
import kr.co.turtlelab.andowsignage.data.objectbox.BusinessId;

@Entity
public class StoredContentPeriod {

    @Id
    private long objectBoxId;

    @BusinessId
    @Unique
    private String contentGuid;
    private String fileName;
    private String startDate;
    private String endDate;
    private String startTime;
    private String endTime;

    public long getObjectBoxId() {
        return objectBoxId;
    }

    public void setObjectBoxId(long objectBoxId) {
        this.objectBoxId = objectBoxId;
    }

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
