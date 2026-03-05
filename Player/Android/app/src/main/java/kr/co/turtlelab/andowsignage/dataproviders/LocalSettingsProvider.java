package kr.co.turtlelab.andowsignage.dataproviders;

import android.text.TextUtils;

import java.util.ArrayList;
import java.util.List;

import io.realm.Realm;
import kr.co.turtlelab.andowsignage.AndoWSignageApp;
import kr.co.turtlelab.andowsignage.data.realm.RealmLocalSettings;
import kr.co.turtlelab.andowsignage.datamodels.LocalSettingsModel;
import kr.co.turtlelab.andowsignage.tools.AuthUtils;
import kr.co.turtlelab.andowsignage.tools.NetworkUtils;

public class LocalSettingsProvider {

    private static final String LOCAL_SETTINGS_ID = "local_settings";
    public static final String KEY_ENABLE_MANUAL_IP = "is_manual";
    public static final String KEY_KEEP_RATIO = "keepratio";
    public static final String KEY_SWITCH_ON_CONTENT_END = "switch_on_content_end";
    public static final String KEY_PLAYER_ID = "player_ip";
    public static final String KEY_MANAGER_IP = "manager_ip";
    public static final String KEY_MANUAL_IP = "manual_ip";
    public static final String KEY_SIGNALR_PORT = "signalr_port";
    public static final String KEY_SIGNALR_HUB_PATH = "signalr_hub_path";

    private LocalSettingsProvider() {
    }

    public static List<LocalSettingsModel> getLocalSettings() {
        List<LocalSettingsModel> list = new ArrayList<>();
        LocalSettingsModel model = loadOrCreateSettings();
        list.add(model);
        return list;
    }

    private static LocalSettingsModel loadOrCreateSettings() {
        LocalSettingsModel model = new LocalSettingsModel();
        Realm realm = null;
        try {
            realm = Realm.getDefaultInstance();
            RealmLocalSettings settings = realm.where(RealmLocalSettings.class)
                    .equalTo("id", LOCAL_SETTINGS_ID)
                    .findFirst();
            if (settings == null) {
                realm.close();
                createNewLocalSettings();
                realm = Realm.getDefaultInstance();
                settings = realm.where(RealmLocalSettings.class)
                        .equalTo("id", LOCAL_SETTINGS_ID)
                        .findFirst();
            }
            if (settings != null) {
                model.setManualIPState(settings.isManualIpEnabled());
                model.setKeepRatioState(settings.isKeepRatioEnabled());
                model.setSwitchOnContentEnd(settings.isSwitchOnContentEndEnabled());
                model.setUsbAuthKey(settings.getUsbAuthKey());
                model.setPlayerId(settings.getPlayerId());
                model.setManagerIp(settings.getManagerIp());
                model.setManualIp(settings.getManualIp());
                model.setSignalrPort(settings.getSignalrPort());
                model.setSignalrHubPath(settings.getSignalrHubPath());
                model.setDataServerIp(settings.getDataServerIp());
                model.setMessageServerIp(settings.getMessageServerIp());
                model.setFtpPort(settings.getFtpPort());
                model.setFtpPasvMinPort(settings.getFtpPasvMinPort());
                model.setFtpPasvMaxPort(settings.getFtpPasvMaxPort());
                model.setFtpRootPath(settings.getFtpRootPath());
            }
        } finally {
            if (realm != null && !realm.isClosed()) {
                realm.close();
            }
        }
        return model;
    }

    public static void createNewLocalSettings() {
        final boolean enableManualIp = AndoWSignageApp.IS_MANUAL;
        final boolean keepRatio = AndoWSignageApp.KEEP_ASPECT_RATIO;
        final boolean switchOnContentEnd = AndoWSignageApp.SWITCH_ON_CONTENT_END;
        String resolvedPlayerId = AndoWSignageApp.PLAYER_ID;
        String resolvedManagerIp = AndoWSignageApp.MANAGER_IP;
        String resolvedManualIp = AndoWSignageApp.MANUAL_IP;
        Realm realmPlayer = null;
        try {
            realmPlayer = Realm.getDefaultInstance();
            kr.co.turtlelab.andowsignage.data.realm.RealmPlayer rp =
                    realmPlayer.where(kr.co.turtlelab.andowsignage.data.realm.RealmPlayer.class).findFirst();
            if (rp != null && !TextUtils.isEmpty(rp.getPlayerName())) {
                resolvedPlayerId = rp.getPlayerName();
            }
        } catch (Exception ignored) {
        } finally {
            if (realmPlayer != null) realmPlayer.close();
        }
        final String playerId = resolvedPlayerId;
        final String managerIp = resolvedManagerIp;
        final String manualIp = resolvedManualIp;

        Realm realm = Realm.getDefaultInstance();
        realm.executeTransaction(r -> {
            RealmLocalSettings settings = r.where(RealmLocalSettings.class)
                    .equalTo("id", LOCAL_SETTINGS_ID)
                    .findFirst();
            if (settings == null) {
                settings = r.createObject(RealmLocalSettings.class, LOCAL_SETTINGS_ID);
            }
            settings.setManualIpEnabled(enableManualIp);
            settings.setKeepRatioEnabled(keepRatio);
            settings.setSwitchOnContentEnd(switchOnContentEnd);
            settings.setUsbAuthKey("");
            settings.setPlayerId(playerId == null ? "" : playerId);
            settings.setManagerIp(managerIp == null ? "" : managerIp);
            settings.setManualIp(manualIp == null ? "" : manualIp);
            settings.setSignalrPort(5000);
            settings.setSignalrHubPath("/Data");
            settings.setDataServerIp("");
            settings.setMessageServerIp("");
            settings.setFtpPort(AndoWSignageApp.FTP_PORT);
            settings.setFtpPasvMinPort(0);
            settings.setFtpPasvMaxPort(0);
            settings.setFtpRootPath("/NewHyOnEnt");
        });
        realm.close();
    }

    public static void updateManualIPState(boolean state) {
        Realm realm = Realm.getDefaultInstance();
        realm.executeTransaction(r -> {
            RealmLocalSettings settings = r.where(RealmLocalSettings.class)
                    .equalTo("id", LOCAL_SETTINGS_ID)
                    .findFirst();
            if (settings == null) {
                settings = r.createObject(RealmLocalSettings.class, LOCAL_SETTINGS_ID);
            }
            settings.setManualIpEnabled(state);
        });
        realm.close();
    }

    public static void updateKeepRatioState(boolean state) {
        Realm realm = Realm.getDefaultInstance();
        realm.executeTransaction(r -> {
            RealmLocalSettings settings = r.where(RealmLocalSettings.class)
                    .equalTo("id", LOCAL_SETTINGS_ID)
                    .findFirst();
            if (settings == null) {
                settings = r.createObject(RealmLocalSettings.class, LOCAL_SETTINGS_ID);
            }
            settings.setKeepRatioEnabled(state);
        });
        realm.close();
    }

    public static void updateSwitchOnContentEndState(boolean state) {
        Realm realm = Realm.getDefaultInstance();
        realm.executeTransaction(r -> {
            RealmLocalSettings settings = r.where(RealmLocalSettings.class)
                    .equalTo("id", LOCAL_SETTINGS_ID)
                    .findFirst();
            if (settings == null) {
                settings = r.createObject(RealmLocalSettings.class, LOCAL_SETTINGS_ID);
            }
            settings.setSwitchOnContentEnd(state);
        });
        realm.close();
    }

    public static void updateUsbAuthKey(String encodedKey) {
        Realm realm = Realm.getDefaultInstance();
        realm.executeTransaction(r -> {
            RealmLocalSettings settings = r.where(RealmLocalSettings.class)
                    .equalTo("id", LOCAL_SETTINGS_ID)
                    .findFirst();
            if (settings == null) {
                settings = r.createObject(RealmLocalSettings.class, LOCAL_SETTINGS_ID);
            }
            settings.setUsbAuthKey(encodedKey == null ? "" : encodedKey);
        });
        realm.close();
    }

    public static void updatePlayerId(String playerId) {
        Realm realm = Realm.getDefaultInstance();
        realm.executeTransaction(r -> {
            RealmLocalSettings settings = r.where(RealmLocalSettings.class)
                    .equalTo("id", LOCAL_SETTINGS_ID)
                    .findFirst();
            if (settings == null) {
                settings = r.createObject(RealmLocalSettings.class, LOCAL_SETTINGS_ID);
            }
            settings.setPlayerId(playerId == null ? "" : playerId);
        });
        realm.close();
    }

    public static void updateManagerIp(String managerIp) {
        Realm realm = Realm.getDefaultInstance();
        realm.executeTransaction(r -> {
            RealmLocalSettings settings = r.where(RealmLocalSettings.class)
                    .equalTo("id", LOCAL_SETTINGS_ID)
                    .findFirst();
            if (settings == null) {
                settings = r.createObject(RealmLocalSettings.class, LOCAL_SETTINGS_ID);
            }
            settings.setManagerIp(managerIp == null ? "" : managerIp);
        });
        realm.close();
    }

    public static void updateManualIp(String manualIp) {
        Realm realm = Realm.getDefaultInstance();
        realm.executeTransaction(r -> {
            RealmLocalSettings settings = r.where(RealmLocalSettings.class)
                    .equalTo("id", LOCAL_SETTINGS_ID)
                    .findFirst();
            if (settings == null) {
                settings = r.createObject(RealmLocalSettings.class, LOCAL_SETTINGS_ID);
            }
            settings.setManualIp(manualIp == null ? "" : manualIp);
        });
        realm.close();
    }

    public static void updateSignalrPort(int port) {
        Realm realm = Realm.getDefaultInstance();
        realm.executeTransaction(r -> {
            RealmLocalSettings settings = r.where(RealmLocalSettings.class)
                    .equalTo("id", LOCAL_SETTINGS_ID)
                    .findFirst();
            if (settings == null) {
                settings = r.createObject(RealmLocalSettings.class, LOCAL_SETTINGS_ID);
            }
            settings.setSignalrPort(port);
        });
        realm.close();
    }

    public static void updateSignalrHubPath(String hubPath) {
        Realm realm = Realm.getDefaultInstance();
        realm.executeTransaction(r -> {
            RealmLocalSettings settings = r.where(RealmLocalSettings.class)
                    .equalTo("id", LOCAL_SETTINGS_ID)
                    .findFirst();
            if (settings == null) {
                settings = r.createObject(RealmLocalSettings.class, LOCAL_SETTINGS_ID);
            }
            settings.setSignalrHubPath(hubPath == null ? "" : hubPath);
        });
        realm.close();
    }

    public static int getSignalrPort() {
        Realm realm = Realm.getDefaultInstance();
        try {
            RealmLocalSettings settings = rWhere(realm);
            return settings != null ? settings.getSignalrPort() : 0;
        } finally {
            realm.close();
        }
    }

    public static String getSignalrHubPath() {
        Realm realm = Realm.getDefaultInstance();
        try {
            RealmLocalSettings settings = rWhere(realm);
            return settings != null ? settings.getSignalrHubPath() : "";
        } finally {
            realm.close();
        }
    }

    public static String getUsbAuthKey() {
        Realm realm = Realm.getDefaultInstance();
        try {
            RealmLocalSettings settings = rWhere(realm);
            return settings != null ? settings.getUsbAuthKey() : "";
        } finally {
            realm.close();
        }
    }

    public static String getManagerIp() {
        Realm realm = Realm.getDefaultInstance();
        try {
            RealmLocalSettings settings = rWhere(realm);
            if (settings != null && !TextUtils.isEmpty(settings.getManagerIp())) {
                return settings.getManagerIp();
            }
            return AndoWSignageApp.MANAGER_IP == null ? "" : AndoWSignageApp.MANAGER_IP;
        } finally {
            realm.close();
        }
    }

    public static String getManualIp() {
        Realm realm = Realm.getDefaultInstance();
        try {
            RealmLocalSettings settings = rWhere(realm);
            if (settings != null && !TextUtils.isEmpty(settings.getManualIp())) {
                return settings.getManualIp();
            }
            return AndoWSignageApp.MANUAL_IP == null ? "" : AndoWSignageApp.MANUAL_IP;
        } finally {
            realm.close();
        }
    }

    public static void updateCommunicationSettings(String dataServerIp,
                                                   String messageServerIp,
                                                   int ftpPort,
                                                   int ftpPasvMinPort,
                                                   int ftpPasvMaxPort,
                                                   String ftpRootPath) {
        Realm realm = Realm.getDefaultInstance();
        realm.executeTransaction(r -> {
            RealmLocalSettings settings = rWhere(r);
            if (settings == null) {
                settings = r.createObject(RealmLocalSettings.class, LOCAL_SETTINGS_ID);
            }
            if (!TextUtils.isEmpty(dataServerIp)) {
                settings.setDataServerIp(dataServerIp.trim());
            }
            if (!TextUtils.isEmpty(messageServerIp)) {
                settings.setMessageServerIp(messageServerIp.trim());
            } else if (!TextUtils.isEmpty(dataServerIp)) {
                settings.setMessageServerIp(dataServerIp.trim());
            }
            if (ftpPort > 0 && ftpPort <= 65535) {
                settings.setFtpPort(ftpPort);
            }
            if (ftpPasvMinPort > 0 && ftpPasvMinPort <= 65535) {
                settings.setFtpPasvMinPort(ftpPasvMinPort);
            }
            if (ftpPasvMaxPort > 0 && ftpPasvMaxPort <= 65535) {
                settings.setFtpPasvMaxPort(ftpPasvMaxPort);
            }
            if (!TextUtils.isEmpty(ftpRootPath)) {
                settings.setFtpRootPath(normalizeFtpRootPath(ftpRootPath));
            }
        });
        realm.close();
    }

    public static String getDataServerIp() {
        Realm realm = Realm.getDefaultInstance();
        try {
            RealmLocalSettings settings = rWhere(realm);
            if (settings == null || TextUtils.isEmpty(settings.getDataServerIp())) {
                return "";
            }
            return settings.getDataServerIp();
        } finally {
            realm.close();
        }
    }

    public static String getMessageServerIp() {
        Realm realm = Realm.getDefaultInstance();
        try {
            RealmLocalSettings settings = rWhere(realm);
            if (settings == null) {
                return "";
            }
            if (!TextUtils.isEmpty(settings.getMessageServerIp())) {
                return settings.getMessageServerIp();
            }
            if (!TextUtils.isEmpty(settings.getDataServerIp())) {
                return settings.getDataServerIp();
            }
            return "";
        } finally {
            realm.close();
        }
    }

    public static int getFtpPort() {
        Realm realm = Realm.getDefaultInstance();
        try {
            RealmLocalSettings settings = rWhere(realm);
            if (settings == null) {
                return AndoWSignageApp.FTP_PORT;
            }
            int value = settings.getFtpPort();
            if (value <= 0 || value > 65535) {
                return AndoWSignageApp.FTP_PORT;
            }
            return value;
        } finally {
            realm.close();
        }
    }

    public static String getFtpRootPath() {
        Realm realm = Realm.getDefaultInstance();
        try {
            RealmLocalSettings settings = rWhere(realm);
            if (settings == null || TextUtils.isEmpty(settings.getFtpRootPath())) {
                return "/NewHyOnEnt";
            }
            return normalizeFtpRootPath(settings.getFtpRootPath());
        } finally {
            realm.close();
        }
    }

    public static void applyStoredCommunicationSettings() {
        int ftpPort = getFtpPort();
        if (ftpPort > 0 && ftpPort <= 65535) {
            AndoWSignageApp.FTP_PORT = ftpPort;
        }
    }

    private static String normalizeFtpRootPath(String rootPath) {
        if (TextUtils.isEmpty(rootPath)) {
            return "/NewHyOnEnt";
        }
        String normalized = rootPath.replace("\\", "/").trim();
        if (!normalized.startsWith("/")) {
            normalized = "/" + normalized;
        }
        while (normalized.length() > 1 && normalized.endsWith("/")) {
            normalized = normalized.substring(0, normalized.length() - 1);
        }
        return TextUtils.isEmpty(normalized) ? "/" : normalized;
    }

    private static RealmLocalSettings rWhere(Realm realm) {
        return realm.where(RealmLocalSettings.class)
                .equalTo("id", LOCAL_SETTINGS_ID)
                .findFirst();
    }

    public static boolean hasStoredUsbKeyForDevice() {
        return matchesStoredUsbKey(NetworkUtils.getMACAddress());
    }

    public static boolean matchesStoredUsbKey(String mac) {
        if (TextUtils.isEmpty(mac)) {
            return false;
        }
        String stored = getUsbAuthKey();
        if (TextUtils.isEmpty(stored)) {
            return false;
        }
        try {
            return AuthUtils.DecodeAuthKey(stored)
                    .equalsIgnoreCase(mac.replace(":", ""));
        } catch (Exception e) {
            return false;
        }
    }

}
