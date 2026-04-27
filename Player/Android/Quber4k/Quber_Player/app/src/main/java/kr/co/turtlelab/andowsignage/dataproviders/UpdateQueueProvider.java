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

    public static boolean hasSilentReadyQueue() {
        ObjectBoxDb storeDb = ObjectBoxDb.getDefaultInstance();
        try {
            StoredUpdateQueue queue = findReadyQueue(storeDb, true);
            return queue != null;
        } finally {
            storeDb.close();
        }
    }

    public static boolean hasReadyQueueRequiringPlaybackRestart() {
        ObjectBoxDb storeDb = ObjectBoxDb.getDefaultInstance();
        try {
            StoredUpdateQueue queue = findReadyQueue(storeDb, false);
            return queue != null;
        } finally {
            storeDb.close();
        }
    }

    public static boolean consumeNextReadyQueue(DataConsumer consumer) {
        return consumeNextReadyQueue(consumer, null);
    }

    public static boolean consumeNextSilentReadyQueue(DataConsumer consumer) {
        return consumeNextReadyQueue(consumer, true);
    }

    public static boolean consumeNextPlaybackRestartReadyQueue(DataConsumer consumer) {
        return consumeNextReadyQueue(consumer, false);
    }

    private static boolean consumeNextReadyQueue(DataConsumer consumer, Boolean silentOnly) {
        if (consumer == null) {
            return false;
        }
        ObjectBoxDb storeDb = ObjectBoxDb.getDefaultInstance();
        StoredUpdateQueue queue;
        try {
            storeDb.beginTransaction();
            queue = findReadyQueue(storeDb, silentOnly);
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

    private static StoredUpdateQueue findReadyQueue(ObjectBoxDb storeDb, Boolean silentOnly) {
        if (storeDb == null) {
            return null;
        }
        java.util.List<StoredUpdateQueue> queues = storeDb.where(StoredUpdateQueue.class)
                .equalTo("status", UpdateQueueContract.Status.READY)
                .sort("id")
                .findAll();
        if (queues == null || queues.isEmpty()) {
            return null;
        }
        for (StoredUpdateQueue queue : queues) {
            if (queue == null) {
                continue;
            }
            boolean silent = isSilentQueue(queue);
            if (silentOnly == null || silentOnly == silent) {
                return queue;
            }
        }
        return null;
    }

    private static boolean isSilentQueue(StoredUpdateQueue queue) {
        if (queue == null) {
            return false;
        }
        return queue.isScheduleQueue()
                || UpdateQueueContract.Type.SCHEDULE.equals(queue.getType());
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
