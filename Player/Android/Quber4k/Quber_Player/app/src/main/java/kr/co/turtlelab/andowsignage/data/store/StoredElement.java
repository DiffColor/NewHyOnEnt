package kr.co.turtlelab.andowsignage.data.store;

import java.util.ArrayList;
import java.util.List;

import io.objectbox.annotation.Entity;
import io.objectbox.annotation.Id;
import io.objectbox.annotation.Transient;
import io.objectbox.annotation.Unique;
import kr.co.turtlelab.andowsignage.data.objectbox.BusinessId;

@Entity
public class StoredElement {

    @Id
    private long objectBoxId;

    @BusinessId
    @Unique
    private String elementId;
    private String pageId;
    private String name;
    private String type;
    private double width;
    private double height;
    private double posTop;
    private double posLeft;
    private int zIndex;
    private boolean muted = true;
    @Transient
    private List<StoredContent> contents = new ArrayList<>();

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

    public String getName() {
        return name;
    }

    public void setName(String name) {
        this.name = name;
    }

    public String getType() {
        return type;
    }

    public void setType(String type) {
        this.type = type;
    }

    public double getWidth() {
        return width;
    }

    public void setWidth(double width) {
        this.width = width;
    }

    public double getHeight() {
        return height;
    }

    public void setHeight(double height) {
        this.height = height;
    }

    public double getPosTop() {
        return posTop;
    }

    public void setPosTop(double posTop) {
        this.posTop = posTop;
    }

    public double getPosLeft() {
        return posLeft;
    }

    public void setPosLeft(double posLeft) {
        this.posLeft = posLeft;
    }

    public int getzIndex() {
        return zIndex;
    }

    public int getZIndex() {
        return zIndex;
    }

    public void setzIndex(int zIndex) {
        this.zIndex = zIndex;
    }

    public boolean isMuted() {
        return muted;
    }

    public void setMuted(boolean muted) {
        this.muted = muted;
    }

    public List<StoredContent> getContents() {
        return contents;
    }

    public void setContents(List<StoredContent> contents) {
        this.contents = contents;
    }
}
