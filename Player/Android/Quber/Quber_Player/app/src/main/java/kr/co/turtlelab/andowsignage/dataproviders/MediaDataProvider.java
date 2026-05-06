package kr.co.turtlelab.andowsignage.dataproviders;

import java.io.File;
import java.util.ArrayList;
import java.util.List;

import io.realm.Realm;
import kr.co.turtlelab.andowsignage.data.realm.RealmContent;
import kr.co.turtlelab.andowsignage.data.realm.RealmElement;
import kr.co.turtlelab.andowsignage.data.realm.RealmPage;
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
        Realm realm = Realm.getDefaultInstance();
        try {
            RealmPage page = realm.where(RealmPage.class)
                    .equalTo("pageId", pageId)
                    .findFirst();
            if (page == null || page.getElements() == null) {
                return result;
            }
            RealmPage detached = realm.copyFromRealm(page);
            RealmElement target = null;
            for (RealmElement element : detached.getElements()) {
                if (elementName.equalsIgnoreCase(element.getName())) {
                    target = element;
                    break;
                }
            }
            if (target == null || target.getContents() == null) {
                return result;
            }
            for (RealmContent realmContent : target.getContents()) {
                if (ContentPeriodEvaluator.hasPeriod(realm, realmContent.getGuid())) {
                    result.hasContentPeriodConstraint = true;
                }
                if (!ContentPeriodEvaluator.isAllowed(realm, realmContent.getGuid(), System.currentTimeMillis())) {
                    continue;
                }
                MediaDataModel mdm = new MediaDataModel();
                if (realmContent.getFileFullPath() != null && !realmContent.getFileFullPath().isEmpty()) {
                    String path = realmContent.getFileFullPath();
                    File file = new File(path);
                    if (!file.exists() && realmContent.getFileName() != null && !realmContent.getFileName().isEmpty()) {
                        path = LocalPathUtils.getContentPath(realmContent.getFileName());
                    }
                    mdm.setFilePath(path);
                } else if (realmContent.getFileName() != null) {
                    mdm.setFileName(realmContent.getFileName());
                }
                mdm.setType(realmContent.getContentType());
                mdm.setPlayTime(realmContent.getPlayMinute(), realmContent.getPlaySecond());
                mdm.setValidState(String.valueOf(realmContent.isContentValid()));
                mdm.setMuted(target.isMuted());
                result.contentList.add(mdm);
                result.visibleDurationSec += Math.max(1L, mdm.getPlayTimeSec());
            }
        } finally {
            realm.close();
        }
        return result;
    }
}
