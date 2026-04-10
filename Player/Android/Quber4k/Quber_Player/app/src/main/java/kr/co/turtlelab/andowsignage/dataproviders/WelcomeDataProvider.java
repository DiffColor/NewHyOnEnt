package kr.co.turtlelab.andowsignage.dataproviders;

import kr.co.turtlelab.andowsignage.data.objectbox.ObjectBoxDb;
import kr.co.turtlelab.andowsignage.data.store.StoredElement;
import kr.co.turtlelab.andowsignage.data.store.StoredPage;
import kr.co.turtlelab.andowsignage.data.store.StoredWelcome;
import kr.co.turtlelab.andowsignage.datamodels.WelcomeDataModel;

public class WelcomeDataProvider {

    private WelcomeDataProvider() {
    }

    public static WelcomeDataModel getContent(String pageId, String elementName) {
        ObjectBoxDb storeDb = ObjectBoxDb.getDefaultInstance();
        try {
            StoredPage page = storeDb.where(StoredPage.class)
                    .equalTo("pageId", pageId)
                    .findFirst();
            if (page == null) {
                return null;
            }
            StoredPage detached = storeDb.copyEntity(page);
            StoredElement element = null;
            if (detached.getElements() != null) {
                for (StoredElement e : detached.getElements()) {
                    if (elementName.equalsIgnoreCase(e.getName())) {
                        element = e;
                        break;
                    }
                }
            }
            if (element == null) {
                return null;
            }
            String elementId = detached.getPageId() + "_" + element.getName();
            StoredWelcome welcome = storeDb.where(StoredWelcome.class)
                    .equalTo("elementId", elementId)
                    .findFirst();
            if (welcome == null) {
                return null;
            }
            WelcomeDataModel model = new WelcomeDataModel();
            model.setLocalImage(welcome.getImageFileName(), welcome.getImageFilePath());
            return model;
        } finally {
            storeDb.close();
        }
    }
}
