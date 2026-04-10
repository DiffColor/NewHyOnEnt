package kr.co.turtlelab.andowsignage.dataproviders;

import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.List;

import kr.co.turtlelab.andowsignage.AndoWSignageApp;
import kr.co.turtlelab.andowsignage.data.objectbox.ObjectBoxDb;
import kr.co.turtlelab.andowsignage.data.store.StoredElement;
import kr.co.turtlelab.andowsignage.data.store.StoredPage;
import kr.co.turtlelab.andowsignage.datamodels.ElementDataModel;

public class ElementDataProvider {

    private ElementDataProvider() {
    }

    public static List<ElementDataModel> getPageElementList(String pageId) {
        List<ElementDataModel> elementList = new ArrayList<>();
        if (pageId == null || pageId.isEmpty()) {
            return elementList;
        }
        ObjectBoxDb storeDb = ObjectBoxDb.getDefaultInstance();
        try {
            StoredPage storedPage = storeDb.where(StoredPage.class)
                    .equalTo("pageId", pageId)
                    .findFirst();
            if (storedPage == null || storedPage.getElements() == null) {
                return elementList;
            }
            StoredPage detached = storeDb.copyEntity(storedPage);
            float[] scales = AndoWSignageApp.getScaleFactorsForCanvas(detached.getCanvasWidth(), detached.getCanvasHeight());
            List<StoredElement> storedElements = detached.getElements();
            Collections.sort(storedElements, new Comparator<StoredElement>() {
                @Override
                public int compare(StoredElement o1, StoredElement o2) {
                    return o1.getzIndex() - o2.getzIndex();
                }
            });
            for (StoredElement storedElement : storedElements) {
                ElementDataModel evm = new ElementDataModel();
                evm.setScales(scales[0], scales[1], scales[2]);
                evm.setid(String.valueOf(elementList.size()));
                evm.setName(storedElement.getName());
                evm.setType(storedElement.getType());
                evm.setWidth(String.valueOf(storedElement.getWidth()));
                evm.setHeight(String.valueOf(storedElement.getHeight()));
                evm.setX(String.valueOf(storedElement.getPosLeft()));
                evm.setY(String.valueOf(storedElement.getPosTop()));
                evm.setZ(String.valueOf(storedElement.getzIndex()));
                elementList.add(evm);
            }
        } finally {
            storeDb.close();
        }
        return elementList;
    }
}
