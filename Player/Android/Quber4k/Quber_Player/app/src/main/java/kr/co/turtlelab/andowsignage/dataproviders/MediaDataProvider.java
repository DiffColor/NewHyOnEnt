package kr.co.turtlelab.andowsignage.dataproviders;

import java.io.File;
import java.util.ArrayList;
import java.util.List;

import kr.co.turtlelab.andowsignage.data.objectbox.ObjectBoxDb;
import kr.co.turtlelab.andowsignage.data.store.StoredContent;
import kr.co.turtlelab.andowsignage.data.store.StoredElement;
import kr.co.turtlelab.andowsignage.data.store.StoredPage;
import kr.co.turtlelab.andowsignage.datamodels.MediaDataModel;
import kr.co.turtlelab.andowsignage.tools.ContentPeriodEvaluator;
import kr.co.turtlelab.andowsignage.tools.LocalPathUtils;

public class MediaDataProvider {

    public static class ContentLoadResult {
        public final List<MediaDataModel> contentList = new ArrayList<>();
        public boolean hasContentPeriodConstraint = false;
        public long visibleDurationSec = 0L;
    }

    private MediaDataProvider() {
    }

    public static List<MediaDataModel> getContentList(String pageId, String elementName) {
        return getContentLoadResult(pageId, elementName).contentList;
    }

    public static ContentLoadResult getContentLoadResult(String pageId, String elementName) {
        ContentLoadResult result = new ContentLoadResult();
        if (pageId == null || elementName == null) {
            return result;
        }
        ObjectBoxDb storeDb = ObjectBoxDb.getDefaultInstance();
        try {
            StoredPage page = storeDb.where(StoredPage.class)
                    .equalTo("pageId", pageId)
                    .findFirst();
            if (page == null || page.getElements() == null) {
                return result;
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
                return result;
            }
            for (StoredContent storedContent : target.getContents()) {
                if (ContentPeriodEvaluator.hasPeriod(storeDb, storedContent.getGuid())) {
                    result.hasContentPeriodConstraint = true;
                }
                if (!ContentPeriodEvaluator.isAllowed(storeDb, storedContent.getGuid(), System.currentTimeMillis())) {
                    continue;
                }
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
                result.contentList.add(mdm);
                result.visibleDurationSec += Math.max(1L, mdm.getPlayTimeSec());
            }
        } finally {
            storeDb.close();
        }
        return result;
    }
}
