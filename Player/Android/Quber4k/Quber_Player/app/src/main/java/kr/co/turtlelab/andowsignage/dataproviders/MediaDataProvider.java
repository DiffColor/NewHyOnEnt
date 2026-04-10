package kr.co.turtlelab.andowsignage.dataproviders;

import java.io.File;
import java.util.ArrayList;
import java.util.List;

import kr.co.turtlelab.andowsignage.data.objectbox.ObjectBoxDb;
import kr.co.turtlelab.andowsignage.data.store.StoredContent;
import kr.co.turtlelab.andowsignage.data.store.StoredElement;
import kr.co.turtlelab.andowsignage.data.store.StoredPage;
import kr.co.turtlelab.andowsignage.datamodels.MediaDataModel;
import kr.co.turtlelab.andowsignage.tools.LocalPathUtils;

public class MediaDataProvider {

    private MediaDataProvider() {
    }

    public static List<MediaDataModel> getContentList(String pageId, String elementName) {
        List<MediaDataModel> contentList = new ArrayList<>();
        if (pageId == null || elementName == null) {
            return contentList;
        }
        ObjectBoxDb storeDb = ObjectBoxDb.getDefaultInstance();
        try {
            StoredPage page = storeDb.where(StoredPage.class)
                    .equalTo("pageId", pageId)
                    .findFirst();
            if (page == null || page.getElements() == null) {
                return contentList;
            }
            StoredPage detached = storeDb.copyEntity(page);
            StoredElement target = null;
            for (StoredElement element : detached.getElements()) {
                if (elementName.equalsIgnoreCase(element.getName())) {
                    target = element;
                    break;
                }
            }
            if (target == null || target.getContents() == null) {
                return contentList;
            }
            for (StoredContent storedContent : target.getContents()) {
                MediaDataModel mdm = new MediaDataModel();
                if (storedContent.getFileFullPath() != null && !storedContent.getFileFullPath().isEmpty()) {
                    String path = storedContent.getFileFullPath();
                    File file = new File(path);
                    if (!file.exists() && storedContent.getFileName() != null && !storedContent.getFileName().isEmpty()) {
                        path = LocalPathUtils.getContentPath(storedContent.getFileName());
                    }
                    mdm.setFilePath(path);
                } else if (storedContent.getFileName() != null) {
                    mdm.setFileName(storedContent.getFileName());
                }
                mdm.setType(storedContent.getContentType());
                mdm.setPlayTime(storedContent.getPlayMinute(), storedContent.getPlaySecond());
                mdm.setValidState(String.valueOf(storedContent.isContentValid()));
                mdm.setMuted(target.isMuted());
                contentList.add(mdm);
            }
        } finally {
            storeDb.close();
        }
        return contentList;
    }
}
