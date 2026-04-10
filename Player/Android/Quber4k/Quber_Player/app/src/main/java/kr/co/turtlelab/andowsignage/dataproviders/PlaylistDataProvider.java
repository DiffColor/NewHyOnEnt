package kr.co.turtlelab.andowsignage.dataproviders;

import java.util.ArrayList;
import java.util.List;

import kr.co.turtlelab.andowsignage.data.objectbox.ObjectBoxDb;
import kr.co.turtlelab.andowsignage.data.objectbox.ObjectBoxSort;
import kr.co.turtlelab.andowsignage.data.store.StoredPage;
import kr.co.turtlelab.andowsignage.datamodels.PageDataModel;

public class PlaylistDataProvider {

    private PlaylistDataProvider() {
    }

    public static List<PageDataModel> getPageList(String playlistName) {
        List<PageDataModel> pageList = new ArrayList<>();
        if (playlistName == null || playlistName.isEmpty()) {
            return pageList;
        }
        ObjectBoxDb storeDb = ObjectBoxDb.getDefaultInstance();
        try {
            List<StoredPage> storedPages = storeDb.copyEntity(
                    storeDb.where(StoredPage.class)
                            .equalTo("playlistName", playlistName)
                            .sort("orderIndex", ObjectBoxSort.ASCENDING)
                            .findAll());
            for (StoredPage storedPage : storedPages) {
                PageDataModel pdm = new PageDataModel();
                pdm.setPageName(storedPage.getPageName());
                pdm.setPlayTime(String.valueOf(storedPage.getPlayHour()),
                        String.valueOf(storedPage.getPlayMinute()),
                        String.valueOf(storedPage.getPlaySecond()));
                pdm.setVolume(String.valueOf(storedPage.getVolume()));
                pdm.setGUID(storedPage.getPageId());
                pdm.setLandscape(storedPage.isLandscape());
                pdm.setCanvasSize(storedPage.getCanvasWidth(), storedPage.getCanvasHeight());
                pageList.add(pdm);
            }
        } finally {
            storeDb.close();
        }
        return pageList;
    }
}
