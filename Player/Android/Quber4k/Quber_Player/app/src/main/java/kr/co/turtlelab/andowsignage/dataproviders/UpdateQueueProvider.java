package kr.co.turtlelab.andowsignage.dataproviders;

import kr.co.turtlelab.andowsignage.data.objectbox.ObjectBoxDb;
import kr.co.turtlelab.andowsignage.data.objectbox.ObjectBoxSort;
import kr.co.turtlelab.andowsignage.data.store.StoredUpdateQueue;
import kr.co.turtlelab.andowsignage.data.update.UpdateQueueContract;

public final class UpdateQueueProvider {

    private UpdateQueueProvider() { }

    public static boolean hasReadyQueue() {
        ObjectBoxDb storeDb = ObjectBoxDb.getDefaultInstance();
        try {
            StoredUpdateQueue queue = storeDb.where(StoredUpdateQueue.class)
                    .equalTo("status", UpdateQueueContract.Status.READY)
                    .sort("id")
                    .findFirst();
            return queue != null;
        } finally {
            storeDb.close();
        }
    }

    public static boolean consumeNextReadyQueue(DataConsumer consumer) {
        if (consumer == null) {
            return false;
        }
        ObjectBoxDb storeDb = ObjectBoxDb.getDefaultInstance();
        StoredUpdateQueue queue;
        try {
            storeDb.beginTransaction();
            queue = storeDb.where(StoredUpdateQueue.class)
                    .equalTo("status", UpdateQueueContract.Status.READY)
                    .sort("id")
                    .findFirst();
            if (queue == null) {
                storeDb.cancelTransaction();
                return false;
            }
            queue = storeDb.copyEntity(queue);
            storeDb.commitTransaction();
        } catch (Exception e) {
            if (storeDb.isInTransaction()) {
                storeDb.cancelTransaction();
            }
            storeDb.close();
            return false;
        }
        storeDb.close();
        return consumer.consume(queue);
    }

    public static StoredUpdateQueue getLatestQueueSnapshot() {
        ObjectBoxDb storeDb = ObjectBoxDb.getDefaultInstance();
        try {
            StoredUpdateQueue queue = storeDb.where(StoredUpdateQueue.class)
                    .sort("updatedAt", ObjectBoxSort.DESCENDING)
                    .findFirst();
            return queue == null ? null : storeDb.copyEntity(queue);
        } finally {
            storeDb.close();
        }
    }

    public interface DataConsumer {
        boolean consume(StoredUpdateQueue queue);
    }
}
