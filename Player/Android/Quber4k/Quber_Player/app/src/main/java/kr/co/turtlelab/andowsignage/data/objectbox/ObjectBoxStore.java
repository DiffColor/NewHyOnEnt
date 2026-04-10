package kr.co.turtlelab.andowsignage.data.objectbox;

import android.content.Context;

import java.io.File;

import io.objectbox.BoxStore;
import kr.co.turtlelab.andowsignage.data.store.MyObjectBox;

public final class ObjectBoxStore {

    private static BoxStore boxStore;

    private ObjectBoxStore() {
    }

    public static synchronized void init(Context context, File appRootDir) {
        if (boxStore != null) {
            return;
        }
        if (context == null) {
            throw new IllegalArgumentException("context == null");
        }

        File externalRoot = context.getExternalFilesDir(null);
        if (externalRoot == null) {
            throw new IllegalStateException("ObjectBox 외부 저장소를 사용할 수 없습니다.");
        }
        File objectBoxDir = new File(new File(externalRoot, "AndoWSignage"), "objectbox");
        if (!objectBoxDir.exists() && !objectBoxDir.mkdirs()) {
            throw new IllegalStateException("ObjectBox 디렉터리를 생성할 수 없습니다: " + objectBoxDir.getAbsolutePath());
        }

        boxStore = MyObjectBox.builder()
                .androidContext(context.getApplicationContext())
                .directory(objectBoxDir)
                .build();
    }

    public static synchronized BoxStore get() {
        if (boxStore == null) {
            throw new IllegalStateException("ObjectBoxStore가 초기화되지 않았습니다.");
        }
        return boxStore;
    }
}
