package kr.co.turtlelab.andowsignage.data.objectbox;

import java.util.ArrayList;
import java.util.List;

public final class ObjectBoxQuery<T> {

    private final ObjectBoxDb db;
    private final Class<T> entityClass;
    private final List<Filter> filters = new ArrayList<>();
    private String sortField;
    private ObjectBoxSort sort = ObjectBoxSort.ASCENDING;

    ObjectBoxQuery(ObjectBoxDb db, Class<T> entityClass) {
        this.db = db;
        this.entityClass = entityClass;
    }

    public ObjectBoxQuery<T> equalTo(String fieldName, Object value) {
        filters.add(new Filter(fieldName, FilterType.EQUALS, new Object[]{value}));
        return this;
    }

    public ObjectBoxQuery<T> in(String fieldName, Object[] values) {
        filters.add(new Filter(fieldName, FilterType.IN, values));
        return this;
    }

    public ObjectBoxQuery<T> notEqualTo(String fieldName, Object value) {
        filters.add(new Filter(fieldName, FilterType.NOT_EQUALS, new Object[]{value}));
        return this;
    }

    public ObjectBoxQuery<T> greaterThan(String fieldName, Object value) {
        filters.add(new Filter(fieldName, FilterType.GREATER_THAN, new Object[]{value}));
        return this;
    }

    public ObjectBoxQuery<T> lessThan(String fieldName, Object value) {
        filters.add(new Filter(fieldName, FilterType.LESS_THAN, new Object[]{value}));
        return this;
    }

    public ObjectBoxQuery<T> lessThanOrEqualTo(String fieldName, Object value) {
        filters.add(new Filter(fieldName, FilterType.LESS_THAN_OR_EQUAL_TO, new Object[]{value}));
        return this;
    }

    public ObjectBoxQuery<T> sort(String fieldName) {
        return sort(fieldName, ObjectBoxSort.ASCENDING);
    }

    public ObjectBoxQuery<T> sort(String fieldName, ObjectBoxSort sort) {
        this.sortField = fieldName;
        this.sort = sort == null ? ObjectBoxSort.ASCENDING : sort;
        return this;
    }

    public T findFirst() {
        List<T> filtered = apply();
        return filtered.isEmpty() ? null : filtered.get(0);
    }

    public ObjectBoxResults<T> findAll() {
        return new ObjectBoxResults<>(db, entityClass, apply());
    }

    public Number max(String fieldName) {
        return ObjectBoxEntitySupport.maxValue(apply(), fieldName);
    }

    private List<T> apply() {
        List<T> values = new ArrayList<>(db.load(entityClass));
        if (!filters.isEmpty()) {
            List<T> matched = new ArrayList<>();
            for (T entity : values) {
                if (matches(entity)) {
                    matched.add(entity);
                }
            }
            values = matched;
        }
        if (sortField != null && sortField.length() > 0) {
            values.sort((left, right) -> ObjectBoxEntitySupport.comparator(sortField, sort).compare(left, right));
        }
        return values;
    }

    private boolean matches(T entity) {
        for (Filter filter : filters) {
            if (filter.type == FilterType.EQUALS) {
                if (!ObjectBoxEntitySupport.matches(entity, filter.fieldName, filter.values[0])) {
                    return false;
                }
            } else if (filter.type == FilterType.IN) {
                if (!ObjectBoxEntitySupport.matchesAny(entity, filter.fieldName, filter.values)) {
                    return false;
                }
            } else if (filter.type == FilterType.NOT_EQUALS) {
                if (ObjectBoxEntitySupport.matches(entity, filter.fieldName, filter.values[0])) {
                    return false;
                }
            } else if (filter.type == FilterType.GREATER_THAN) {
                if (ObjectBoxEntitySupport.compareField(entity, filter.fieldName, filter.values[0]) <= 0) {
                    return false;
                }
            } else if (filter.type == FilterType.LESS_THAN) {
                if (ObjectBoxEntitySupport.compareField(entity, filter.fieldName, filter.values[0]) >= 0) {
                    return false;
                }
            } else if (filter.type == FilterType.LESS_THAN_OR_EQUAL_TO) {
                if (ObjectBoxEntitySupport.compareField(entity, filter.fieldName, filter.values[0]) > 0) {
                    return false;
                }
            }
        }
        return true;
    }

    private enum FilterType {
        EQUALS,
        IN,
        NOT_EQUALS,
        GREATER_THAN,
        LESS_THAN,
        LESS_THAN_OR_EQUAL_TO
    }

    private static final class Filter {
        final String fieldName;
        final FilterType type;
        final Object[] values;

        Filter(String fieldName, FilterType type, Object[] values) {
            this.fieldName = fieldName;
            this.type = type;
            this.values = values;
        }
    }
}
