package kr.co.turtlelab.andowsignage.data.objectbox;

import com.google.gson.Gson;

import java.lang.reflect.Field;
import java.util.ArrayList;
import java.util.Collection;
import java.util.Comparator;
import java.util.List;
import java.util.Objects;

import io.objectbox.Box;
import io.objectbox.BoxStore;
import io.objectbox.annotation.Id;
import kr.co.turtlelab.andowsignage.data.store.StoredContent;
import kr.co.turtlelab.andowsignage.data.store.StoredElement;
import kr.co.turtlelab.andowsignage.data.store.StoredPage;

final class ObjectBoxEntitySupport {

    private static final Gson GSON = new Gson();

    private ObjectBoxEntitySupport() {
    }

    static <T> List<T> loadAll(BoxStore boxStore, Class<T> entityClass) {
        Box<T> box = boxStore.boxFor(entityClass);
        List<T> loaded = new ArrayList<>(box.getAll());
        for (T entity : loaded) {
            hydrateGraph(boxStore, entity);
        }
        return loaded;
    }

    static <T> T instantiate(Class<T> entityClass) {
        try {
            return entityClass.newInstance();
        } catch (Exception ex) {
            throw new IllegalStateException("엔티티를 생성할 수 없습니다: " + entityClass.getName(), ex);
        }
    }

    static void assignBusinessId(Object entity, Object primaryKeyValue) {
        if (entity == null) {
            return;
        }
        Field field = findBusinessIdField(entity.getClass());
        if (field == null) {
            return;
        }
        setFieldValue(entity, field, primaryKeyValue);
    }

    static Object getBusinessId(Object entity) {
        if (entity == null) {
            return null;
        }
        Field field = findBusinessIdField(entity.getClass());
        return field == null ? null : getFieldValue(entity, field);
    }

    static long getObjectBoxId(Object entity) {
        if (entity == null) {
            return 0L;
        }
        Field field = findObjectBoxIdField(entity.getClass());
        Object value = field == null ? null : getFieldValue(entity, field);
        if (value instanceof Number) {
            return ((Number) value).longValue();
        }
        return 0L;
    }

    static void setObjectBoxId(Object entity, long id) {
        if (entity == null) {
            return;
        }
        Field field = findObjectBoxIdField(entity.getClass());
        if (field != null) {
            setFieldValue(entity, field, id);
        }
    }

    static <T> T deepCopy(T entity, Class<T> entityClass) {
        if (entity == null) {
            return null;
        }
        return GSON.fromJson(GSON.toJson(entity), entityClass);
    }

    static <T> List<T> deepCopyList(Collection<T> entities, Class<T> entityClass) {
        List<T> copied = new ArrayList<>();
        if (entities == null) {
            return copied;
        }
        for (T entity : entities) {
            copied.add(deepCopy(entity, entityClass));
        }
        return copied;
    }

    static Comparator<Object> comparator(String fieldName, ObjectBoxSort sort) {
        Comparator<Object> comparator = (left, right) -> compareValues(getFieldValue(left, fieldName), getFieldValue(right, fieldName));
        if (sort == ObjectBoxSort.DESCENDING) {
            comparator = comparator.reversed();
        }
        return comparator;
    }

    static boolean matches(Object entity, String fieldName, Object expected) {
        Object actual = getFieldValue(entity, fieldName);
        return Objects.equals(actual, expected);
    }

    static boolean matchesAny(Object entity, String fieldName, Object[] expectedValues) {
        Object actual = getFieldValue(entity, fieldName);
        if (expectedValues == null) {
            return false;
        }
        for (Object expected : expectedValues) {
            if (Objects.equals(actual, expected)) {
                return true;
            }
        }
        return false;
    }

    static int compareField(Object entity, String fieldName, Object expected) {
        return compareValues(getFieldValue(entity, fieldName), expected);
    }

    static Number maxValue(Collection<?> entities, String fieldName) {
        Number max = null;
        if (entities == null) {
            return null;
        }
        for (Object entity : entities) {
            Object value = getFieldValue(entity, fieldName);
            if (!(value instanceof Number)) {
                continue;
            }
            Number number = (Number) value;
            if (max == null || Double.compare(number.doubleValue(), max.doubleValue()) > 0) {
                max = number;
            }
        }
        return max;
    }

    static void hydrateGraph(BoxStore boxStore, Object entity) {
        if (entity instanceof StoredPage) {
            hydratePage(boxStore, (StoredPage) entity);
        } else if (entity instanceof StoredElement) {
            hydrateElement(boxStore, (StoredElement) entity);
        }
    }

    private static void hydratePage(BoxStore boxStore, StoredPage page) {
        if (page == null) {
            return;
        }
        List<StoredElement> elements = loadAll(boxStore, StoredElement.class);
        List<StoredElement> filtered = new ArrayList<>();
        for (StoredElement element : elements) {
            if (Objects.equals(page.getPageId(), element.getPageId())) {
                hydrateElement(boxStore, element);
                filtered.add(element);
            }
        }
        filtered.sort((left, right) -> Integer.compare(left.getzIndex(), right.getzIndex()));
        page.setElements(filtered);
    }

    private static void hydrateElement(BoxStore boxStore, StoredElement element) {
        if (element == null) {
            return;
        }
        List<StoredContent> contents = loadAll(boxStore, StoredContent.class);
        List<StoredContent> filtered = new ArrayList<>();
        for (StoredContent content : contents) {
            if (Objects.equals(element.getElementId(), content.getElementId())) {
                filtered.add(content);
            }
        }
        filtered.sort((left, right) -> compareValues(left.getUid(), right.getUid()));
        element.setContents(filtered);
    }

    private static Field findBusinessIdField(Class<?> type) {
        for (Class<?> current = type; current != null && current != Object.class; current = current.getSuperclass()) {
            for (Field field : current.getDeclaredFields()) {
                if (field.isAnnotationPresent(BusinessId.class)) {
                    field.setAccessible(true);
                    return field;
                }
            }
        }
        return null;
    }

    private static Field findObjectBoxIdField(Class<?> type) {
        for (Class<?> current = type; current != null && current != Object.class; current = current.getSuperclass()) {
            for (Field field : current.getDeclaredFields()) {
                if (field.isAnnotationPresent(Id.class)) {
                    field.setAccessible(true);
                    return field;
                }
            }
        }
        return null;
    }

    private static Object getFieldValue(Object entity, String fieldName) {
        if (entity == null || fieldName == null) {
            return null;
        }
        Field field = findField(entity.getClass(), fieldName);
        return field == null ? null : getFieldValue(entity, field);
    }

    private static Object getFieldValue(Object entity, Field field) {
        try {
            return field.get(entity);
        } catch (Exception ex) {
            throw new IllegalStateException("필드 값을 읽을 수 없습니다: " + field.getName(), ex);
        }
    }

    private static void setFieldValue(Object entity, Field field, Object value) {
        try {
            Class<?> type = field.getType();
            if (value == null) {
                if (!type.isPrimitive()) {
                    field.set(entity, null);
                }
                return;
            }
            if (type == long.class || type == Long.class) {
                long longValue = value instanceof Number ? ((Number) value).longValue() : Long.parseLong(String.valueOf(value));
                field.set(entity, longValue);
            } else if (type == int.class || type == Integer.class) {
                int intValue = value instanceof Number ? ((Number) value).intValue() : Integer.parseInt(String.valueOf(value));
                field.set(entity, intValue);
            } else if (type == boolean.class || type == Boolean.class) {
                boolean boolValue = value instanceof Boolean ? (Boolean) value : Boolean.parseBoolean(String.valueOf(value));
                field.set(entity, boolValue);
            } else if (type == float.class || type == Float.class) {
                float floatValue = value instanceof Number ? ((Number) value).floatValue() : Float.parseFloat(String.valueOf(value));
                field.set(entity, floatValue);
            } else if (type == double.class || type == Double.class) {
                double doubleValue = value instanceof Number ? ((Number) value).doubleValue() : Double.parseDouble(String.valueOf(value));
                field.set(entity, doubleValue);
            } else if (type == String.class) {
                field.set(entity, String.valueOf(value));
            } else {
                field.set(entity, value);
            }
        } catch (Exception ex) {
            throw new IllegalStateException("필드 값을 쓸 수 없습니다: " + field.getName(), ex);
        }
    }

    private static Field findField(Class<?> type, String fieldName) {
        for (Class<?> current = type; current != null && current != Object.class; current = current.getSuperclass()) {
            try {
                Field field = current.getDeclaredField(fieldName);
                field.setAccessible(true);
                return field;
            } catch (NoSuchFieldException ignore) {
            }
        }
        return null;
    }

    @SuppressWarnings({"rawtypes", "unchecked"})
    private static int compareValues(Object left, Object right) {
        if (left == right) {
            return 0;
        }
        if (left == null) {
            return -1;
        }
        if (right == null) {
            return 1;
        }
        if (left instanceof Number && right instanceof Number) {
            return Double.compare(((Number) left).doubleValue(), ((Number) right).doubleValue());
        }
        if (left instanceof Comparable && right instanceof Comparable) {
            return ((Comparable) left).compareTo(right);
        }
        return String.valueOf(left).compareTo(String.valueOf(right));
    }
}
