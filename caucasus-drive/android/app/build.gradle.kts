plugins {
    id("com.android.application")
    id("org.jetbrains.kotlin.android")
}

android {
    namespace = "com.caucasusdrive.game"
    compileSdk = 34

    defaultConfig {
        applicationId = "com.caucasusdrive.game"
        minSdk = 24          // Android 7.0+: WebView на Chromium с WebGL 2
        targetSdk = 34
        versionCode = 8
        versionName = "1.7.0"
    }

    buildTypes {
        release {
            isMinifyEnabled = true
            isShrinkResources = true
            proguardFiles(getDefaultProguardFile("proguard-android-optimize.txt"), "proguard-rules.pro")
        }
    }
    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }
    kotlinOptions { jvmTarget = "17" }
    buildFeatures { buildConfig = true }

    // Веб-сборка (../dist) копируется в сгенерированную папку ассетов → assets/www/
    sourceSets["main"].assets.srcDir(layout.buildDirectory.dir("generated/webassets"))
    // сжатые текстуры и так сжаты — не тратим время на повторное сжатие в APK
    androidResources { noCompress += listOf("ktx2", "basis") }
}

val copyWebAssets by tasks.registering(Sync::class) {
    from(rootProject.file("../dist"))
    into(layout.buildDirectory.dir("generated/webassets/www"))
    doFirst {
        if (!rootProject.file("../dist/index.html").exists()) {
            throw GradleException("Нет ../dist/index.html — выполните `npm run build` в caucasus-drive/")
        }
    }
}
tasks.named("preBuild") { dependsOn(copyWebAssets) }

dependencies {
    implementation("androidx.core:core-ktx:1.13.1")
    implementation("androidx.activity:activity-ktx:1.9.2")
    implementation("androidx.webkit:webkit:1.11.0")
}
