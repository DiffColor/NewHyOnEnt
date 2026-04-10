package kr.co.turtlelab.andowsignage.data.store;

import io.objectbox.annotation.Entity;
import io.objectbox.annotation.Id;
import io.objectbox.annotation.Unique;
import kr.co.turtlelab.andowsignage.data.objectbox.BusinessId;

@Entity
public class StoredWelcome {

    @Id
    private long objectBoxId;

    @BusinessId
    @Unique
    private String elementId;
    private String pageId;
    private String elementName;
    private String imageFileName;
    private String imageFilePath;

    public long getObjectBoxId() {
        return objectBoxId;
    }

    public void setObjectBoxId(long objectBoxId) {
        this.objectBoxId = objectBoxId;
    }

    public String getElementId() {
        return elementId;
    }

    public void setElementId(String elementId) {
        this.elementId = elementId;
    }

    public String getPageId() {
        return pageId;
    }

    public void setPageId(String pageId) {
        this.pageId = pageId;
    }

    public String getElementName() {
        return elementName;
    }

    public void setElementName(String elementName) {
        this.elementName = elementName;
    }

    public String getImageFileName() {
        return imageFileName;
    }

    public void setImageFileName(String imageFileName) {
        this.imageFileName = imageFileName;
    }

    public String getImageFilePath() {
        return imageFilePath;
    }

    public void setImageFilePath(String imageFilePath) {
        this.imageFilePath = imageFilePath;
    }
}
