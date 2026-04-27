package kr.co.turtlelab.andowsignage.tools;

import android.content.Context;
import android.media.MediaScannerConnection;
import android.net.Uri;

import java.io.File;

import kr.co.turtlelab.andowsignage.AndoWSignageApp;

public class MediaScanner implements MediaScannerConnection.MediaScannerConnectionClient {
    private MediaScannerConnection mScanner;
    private String mFilepath = null;
    private Context ctx;
    boolean mIsDeleted = false;

    public MediaScanner(Context context) {
        mScanner = new MediaScannerConnection(context, this);
        ctx = context;
    }

    public void notify(String filepath, boolean isDeleted) {
        if (shouldSkipScan(filepath)) {
            return;
        }
    	mIsDeleted = isDeleted;
        mFilepath = filepath;
        mScanner.connect(); // 이 함수 호출 후 onMediaScannerConnected가 호출 됨.
    }

    private boolean shouldSkipScan(String filepath) {
        if (filepath == null || filepath.length() < 1) {
            return true;
        }
        File appRoot = AndoWSignageApp.getAppRootDir();
        if (appRoot == null) {
            return false;
        }
        try {
            String appRootPath = appRoot.getCanonicalPath();
            String targetPath = new File(filepath).getCanonicalPath();
            return targetPath.equals(appRootPath) || targetPath.startsWith(appRootPath + File.separator);
        } catch (Exception ignore) {
            return false;
        }
    }

    @Override
    public void onMediaScannerConnected() {
        if(mFilepath != null) {
            String filepath = new String(mFilepath);
            mScanner.scanFile(filepath, null); // MediaStore의 정보를 업데이트
        }

        mFilepath = null;
    }

    @Override
    public void onScanCompleted(String path, Uri uri) {
        mScanner.disconnect(); // onMediaScannerConnected 수행이 끝난 후 연결 해제
        if(mIsDeleted && uri != null)
        	ctx.getContentResolver().delete(uri, null, null);
    }
    
}
