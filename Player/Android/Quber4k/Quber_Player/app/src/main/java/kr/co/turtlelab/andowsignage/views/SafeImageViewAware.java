package kr.co.turtlelab.andowsignage.views;

import android.view.ViewGroup;
import android.widget.ImageView;

import com.nostra13.universalimageloader.core.imageaware.ImageViewAware;

import kr.co.turtlelab.andowsignage.AndoWSignageApp;

public class SafeImageViewAware extends ImageViewAware {
    private final int overrideWidth;
    private final int overrideHeight;

    public SafeImageViewAware(ImageView imageView) {
        this(imageView, false, 0, 0);
    }

    public SafeImageViewAware(ImageView imageView, boolean checkActualViewSize) {
        this(imageView, checkActualViewSize, 0, 0);
    }

    public SafeImageViewAware(ImageView imageView, int overrideWidth, int overrideHeight) {
        this(imageView, false, overrideWidth, overrideHeight);
    }

    public SafeImageViewAware(ImageView imageView, boolean checkActualViewSize, int overrideWidth, int overrideHeight) {
        super(imageView, checkActualViewSize);
        this.overrideWidth = overrideWidth;
        this.overrideHeight = overrideHeight;
    }

    @Override
    public int getWidth() {
        if (overrideWidth > 0) {
            return overrideWidth;
        }
        ImageView view = getWrappedView();
        if (view == null) {
            return 0;
        }
        int width = view.getWidth();
        if (width <= 0) {
            width = view.getMeasuredWidth();
        }
        if (width <= 0) {
            ViewGroup.LayoutParams params = view.getLayoutParams();
            if (params != null && params.width > 0) {
                width = params.width;
            }
        }
        if (width <= 0) {
            width = AndoWSignageApp.getDeviceWidth();
        }
        return width;
    }

    @Override
    public int getHeight() {
        if (overrideHeight > 0) {
            return overrideHeight;
        }
        ImageView view = getWrappedView();
        if (view == null) {
            return 0;
        }
        int height = view.getHeight();
        if (height <= 0) {
            height = view.getMeasuredHeight();
        }
        if (height <= 0) {
            ViewGroup.LayoutParams params = view.getLayoutParams();
            if (params != null && params.height > 0) {
                height = params.height;
            }
        }
        if (height <= 0) {
            height = AndoWSignageApp.getDeviceHeight();
        }
        return height;
    }
}
