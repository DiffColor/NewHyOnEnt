package kr.co.turtlelab.andowsignage.data.objectbox;

import java.util.ArrayList;
import java.util.Collection;

public class ObjectBoxResults<T> extends ArrayList<T> {

    private final ObjectBoxDb db;
    private final Class<T> entityClass;

    ObjectBoxResults(ObjectBoxDb db, Class<T> entityClass, Collection<T> values) {
        super(values);
        this.db = db;
        this.entityClass = entityClass;
    }

    public T first() {
        return isEmpty() ? null : get(0);
    }

    public void deleteAll() {
        db.deleteEntities(entityClass, this);
        clear();
    }
}
