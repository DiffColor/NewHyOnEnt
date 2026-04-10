package kr.co.turtlelab.andowsignage.data.objectbox;

import java.util.ArrayList;
import java.util.Collection;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

import io.objectbox.Box;
import io.objectbox.BoxStore;

public final class ObjectBoxDb {

    private final BoxStore boxStore;
    private final Map<Class<?>, List<?>> workingSets = new HashMap<>();
    private boolean inTransaction;
    private boolean closed;

    private ObjectBoxDb(BoxStore boxStore) {
        this.boxStore = boxStore;
    }

    public static ObjectBoxDb getDefaultInstance() {
        return new ObjectBoxDb(ObjectBoxStore.get());
    }

    public <T> ObjectBoxQuery<T> query(Class<T> entityClass) {
        return new ObjectBoxQuery<>(this, entityClass);
    }

    public <T> ObjectBoxQuery<T> where(Class<T> entityClass) {
        return query(entityClass);
    }

    public <T> T copy(T entity) {
        if (entity == null) {
            return null;
        }
        @SuppressWarnings("unchecked")
        Class<T> entityClass = (Class<T>) entity.getClass();
        return ObjectBoxEntitySupport.deepCopy(entity, entityClass);
    }

    public <T> List<T> copy(List<T> entities) {
        List<T> copied = new ArrayList<>();
        if (entities == null || entities.isEmpty()) {
            return copied;
        }
        @SuppressWarnings("unchecked")
        Class<T> entityClass = (Class<T>) entities.get(0).getClass();
        copied.addAll(ObjectBoxEntitySupport.deepCopyList(entities, entityClass));
        return copied;
    }

    public <T> T copyEntity(T entity) {
        return copy(entity);
    }

    public <T> List<T> copyEntity(List<T> entities) {
        return copy(entities);
    }

    public void executeTransaction(ObjectBoxTransaction transaction) {
        beginTransaction();
        try {
            transaction.run(this);
            commitTransaction();
        } catch (RuntimeException ex) {
            cancelTransaction();
            throw ex;
        } catch (Exception ex) {
            cancelTransaction();
            throw new IllegalStateException("ObjectBox 트랜잭션 실행 중 오류가 발생했습니다.", ex);
        }
    }

    public void beginTransaction() {
        if (inTransaction) {
            throw new IllegalStateException("이미 트랜잭션 중입니다.");
        }
        inTransaction = true;
        workingSets.clear();
    }

    public void commitTransaction() {
        ensureTransaction();
        boxStore.runInTx(() -> {
            for (Map.Entry<Class<?>, List<?>> entry : workingSets.entrySet()) {
                persistWorkingSet(entry.getKey(), entry.getValue());
            }
        });
        clearTransaction();
    }

    public void cancelTransaction() {
        if (!inTransaction) {
            return;
        }
        clearTransaction();
    }

    public boolean isInTransaction() {
        return inTransaction;
    }

    public boolean isClosed() {
        return closed;
    }

    public void close() {
        closed = true;
    }

    public <T> T create(Class<T> entityClass, Object businessId) {
        T entity = ObjectBoxEntitySupport.instantiate(entityClass);
        ObjectBoxEntitySupport.assignBusinessId(entity, businessId);
        if (inTransaction) {
            List<T> workingSet = workingSet(entityClass);
            Object existingBusinessId = ObjectBoxEntitySupport.getBusinessId(entity);
            T existing = findByBusinessId(workingSet, existingBusinessId);
            if (existing != null) {
                long existingObjectBoxId = ObjectBoxEntitySupport.getObjectBoxId(existing);
                if (existingObjectBoxId > 0) {
                    ObjectBoxEntitySupport.setObjectBoxId(entity, existingObjectBoxId);
                }
                workingSet.remove(existing);
            }
            workingSet.add(entity);
        } else {
            upsertImmediately(entityClass, entity);
        }
        return entity;
    }

    public <T> T createObject(Class<T> entityClass, Object businessId) {
        return create(entityClass, businessId);
    }

    public <T> void insertOrUpdate(T entity) {
        if (entity == null) {
            return;
        }
        @SuppressWarnings("unchecked")
        Class<T> entityClass = (Class<T>) entity.getClass();
        if (inTransaction) {
            List<T> workingSet = workingSet(entityClass);
            workingSet.removeIf(item -> isSameEntity(item, entity));
            workingSet.add(entity);
            return;
        }
        upsertImmediately(entityClass, entity);
    }

    public <T> void insertOrUpdate(List<T> entities) {
        if (entities == null || entities.isEmpty()) {
            return;
        }
        for (T entity : entities) {
            insertOrUpdate(entity);
        }
    }

    public void deleteAll(Class<?> entityClass) {
        if (inTransaction) {
            workingSet(entityClass).clear();
            return;
        }
        boxStore.boxFor(entityClass).removeAll();
    }

    public void delete(Class<?> entityClass) {
        deleteAll(entityClass);
    }

    public void delete(Object entity) {
        if (entity == null) {
            return;
        }
        @SuppressWarnings("unchecked")
        Class<Object> entityClass = (Class<Object>) entity.getClass();
        if (inTransaction) {
            workingSet(entityClass).removeIf(item -> isSameEntity(item, entity));
            return;
        }
        Box<Object> box = boxStore.boxFor(entityClass);
        long objectBoxId = ObjectBoxEntitySupport.getObjectBoxId(entity);
        if (objectBoxId > 0) {
            box.remove(objectBoxId);
            return;
        }
        List<Object> all = new ArrayList<>(box.getAll());
        all.removeIf(item -> isSameEntity(item, entity));
        box.removeAll();
        if (!all.isEmpty()) {
            box.put(all);
        }
    }

    <T> List<T> load(Class<T> entityClass) {
        if (inTransaction) {
            return workingSet(entityClass);
        }
        return ObjectBoxEntitySupport.loadAll(boxStore, entityClass);
    }

    <T> void deleteEntities(Class<T> entityClass, Collection<T> entities) {
        if (entities == null || entities.isEmpty()) {
            return;
        }
        if (inTransaction) {
            workingSet(entityClass).removeIf(item -> containsSameEntity(entities, item));
            return;
        }
        Box<T> box = boxStore.boxFor(entityClass);
        List<T> current = new ArrayList<>(box.getAll());
        current.removeIf(item -> containsSameEntity(entities, item));
        box.removeAll();
        if (!current.isEmpty()) {
            box.put(current);
        }
    }

    private void ensureTransaction() {
        if (!inTransaction) {
            throw new IllegalStateException("트랜잭션 중이 아닙니다.");
        }
    }

    @SuppressWarnings("unchecked")
    private <T> List<T> workingSet(Class<T> entityClass) {
        List<T> values = (List<T>) workingSets.get(entityClass);
        if (values == null) {
            values = ObjectBoxEntitySupport.loadAll(boxStore, entityClass);
            workingSets.put(entityClass, values);
        }
        return values;
    }

    @SuppressWarnings("unchecked")
    private void persistWorkingSet(Class<?> entityClass, List<?> workingSet) {
        Box<Object> box = (Box<Object>) boxStore.boxFor((Class<Object>) entityClass);
        box.removeAll();
        if (workingSet == null || workingSet.isEmpty()) {
            return;
        }
        box.put((Collection<Object>) workingSet);
    }

    private void clearTransaction() {
        workingSets.clear();
        inTransaction = false;
    }

    private <T> void upsertImmediately(Class<T> entityClass, T entity) {
        List<T> current = ObjectBoxEntitySupport.loadAll(boxStore, entityClass);
        current.removeIf(item -> isSameEntity(item, entity));
        current.add(entity);
        Box<T> box = boxStore.boxFor(entityClass);
        box.removeAll();
        box.put(current);
    }

    private <T> T findByBusinessId(List<T> values, Object businessId) {
        if (values == null) {
            return null;
        }
        for (T value : values) {
            Object currentBusinessId = ObjectBoxEntitySupport.getBusinessId(value);
            if (businessId == null ? currentBusinessId == null : businessId.equals(currentBusinessId)) {
                return value;
            }
        }
        return null;
    }

    private boolean isSameEntity(Object left, Object right) {
        long leftObjectBoxId = ObjectBoxEntitySupport.getObjectBoxId(left);
        long rightObjectBoxId = ObjectBoxEntitySupport.getObjectBoxId(right);
        if (leftObjectBoxId > 0 && rightObjectBoxId > 0) {
            return leftObjectBoxId == rightObjectBoxId;
        }
        Object leftBusinessId = ObjectBoxEntitySupport.getBusinessId(left);
        Object rightBusinessId = ObjectBoxEntitySupport.getBusinessId(right);
        return leftBusinessId == null ? rightBusinessId == null : leftBusinessId.equals(rightBusinessId);
    }

    private boolean containsSameEntity(Collection<?> entities, Object candidate) {
        for (Object entity : entities) {
            if (isSameEntity(entity, candidate)) {
                return true;
            }
        }
        return false;
    }
}
