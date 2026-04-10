package kr.co.turtlelab.andowsignage.dataproviders;

import java.util.ArrayList;
import java.util.List;

import kr.co.turtlelab.andowsignage.data.objectbox.ObjectBoxDb;
import kr.co.turtlelab.andowsignage.data.store.StoredContent;
import kr.co.turtlelab.andowsignage.data.store.StoredElement;
import kr.co.turtlelab.andowsignage.data.store.StoredPage;
import kr.co.turtlelab.andowsignage.datamodels.ScrolltextDataModel;

public class ScrolltextDataProvider {

    private ScrolltextDataProvider() {
    }

    public static List<ScrolltextDataModel> getContentList(String pageId, String elementName) {
        List<ScrolltextDataModel> contentList = new ArrayList<>();
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
                ScrolltextDataModel sdm = new ScrolltextDataModel();
                sdm.setText(storedContent.getFileName());
                sdm.setFont(storedContent.getContentType());
                sdm.setBackColor(storedContent.getPlaySecond());
                sdm.setForeColor(storedContent.getPlayMinute());
                sdm.setScrolltime(String.valueOf(Math.max(1, storedContent.getScrollSpeedSec())));
                contentList.add(sdm);
            }
        } finally {
            storeDb.close();
        }
        return contentList;
    }
}
