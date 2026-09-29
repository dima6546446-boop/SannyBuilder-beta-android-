package ru.autovaz.sim

import android.annotation.SuppressLint
import android.graphics.Color
import android.os.Bundle
import android.view.View
import android.view.WindowManager
import android.webkit.WebChromeClient
import android.webkit.WebResourceRequest
import android.webkit.WebResourceResponse
import android.webkit.WebSettings
import android.webkit.WebView
import androidx.activity.ComponentActivity
import androidx.activity.OnBackPressedCallback
import androidx.core.view.WindowCompat
import androidx.core.view.WindowInsetsCompat
import androidx.core.view.WindowInsetsControllerCompat
import androidx.webkit.WebViewAssetLoader
import androidx.webkit.WebViewClientCompat

/**
 * Нативная оболочка: полноэкранный WebView с аппаратным ускорением.
 *
 * Игра грузится через WebViewAssetLoader с https-origin
 * (https://appassets.androidplatform.net/assets/www/index.html), а не file:// —
 * иначе ES-модули и WebAudio блокируются политиками безопасности WebView.
 */
class MainActivity : ComponentActivity() {

    private lateinit var webView: WebView

    @SuppressLint("SetJavaScriptEnabled")
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        window.addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)
        WindowCompat.setDecorFitsSystemWindows(window, false)

        val assetLoader = WebViewAssetLoader.Builder()
            .addPathHandler("/assets/", WebViewAssetLoader.AssetsPathHandler(this))
            .build()

        webView = WebView(this).apply {
            setBackgroundColor(Color.BLACK)
            setLayerType(View.LAYER_TYPE_HARDWARE, null)
            overScrollMode = View.OVER_SCROLL_NEVER
            isVerticalScrollBarEnabled = false
            isHorizontalScrollBarEnabled = false
            settings.apply {
                javaScriptEnabled = true
                domStorageEnabled = true                  // localStorage (настройки качества)
                mediaPlaybackRequiresUserGesture = false  // WebAudio после первого тапа
                allowFileAccess = false
                allowContentAccess = false
                cacheMode = WebSettings.LOAD_DEFAULT
                setSupportZoom(false)
                builtInZoomControls = false
                displayZoomControls = false
                useWideViewPort = true
                loadWithOverviewMode = true
            }
            webViewClient = object : WebViewClientCompat() {
                override fun shouldInterceptRequest(view: WebView, request: WebResourceRequest): WebResourceResponse? =
                    assetLoader.shouldInterceptRequest(request.url)
            }
            webChromeClient = WebChromeClient()
        }
        setContentView(webView)
        hideSystemBars()

        if (BuildConfig.DEBUG) WebView.setWebContentsDebuggingEnabled(true) // chrome://inspect

        onBackPressedDispatcher.addCallback(this, object : OnBackPressedCallback(true) {
            override fun handleOnBackPressed() {
                webView.evaluateJavascript("window.__onNativePause && window.__onNativePause()", null)
                moveTaskToBack(true)
            }
        })

        if (savedInstanceState == null) {
            webView.loadUrl("https://appassets.androidplatform.net/assets/www/index.html")
        } else {
            webView.restoreState(savedInstanceState)
        }
    }

    private fun hideSystemBars() {
        WindowInsetsControllerCompat(window, window.decorView).apply {
            hide(WindowInsetsCompat.Type.systemBars())
            systemBarsBehavior = WindowInsetsControllerCompat.BEHAVIOR_SHOW_TRANSIENT_BARS_BY_SWIPE
        }
    }

    override fun onWindowFocusChanged(hasFocus: Boolean) {
        super.onWindowFocusChanged(hasFocus)
        if (hasFocus) hideSystemBars()
    }

    override fun onPause() {
        // останавливаем игровой цикл и звук, освобождаем GPU для системы
        webView.evaluateJavascript("window.__onNativePause && window.__onNativePause()", null)
        webView.onPause()
        webView.pauseTimers()
        super.onPause()
    }

    override fun onResume() {
        super.onResume()
        webView.onResume()
        webView.resumeTimers()
        webView.evaluateJavascript("window.__onNativeResume && window.__onNativeResume()", null)
    }

    override fun onSaveInstanceState(outState: Bundle) {
        super.onSaveInstanceState(outState)
        webView.saveState(outState)
    }

    override fun onDestroy() {
        webView.destroy()
        super.onDestroy()
    }
}
