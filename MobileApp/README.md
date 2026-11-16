# ASTRA: Mission Control — Mobile

Touch-first Flutter edition of ASTRA. The Android app keeps the existing four mission/planet assignments and their distinct duties. Each mission uses the game loop **learn → decide → see the result → understand why**. Mission progress and accessibility preferences are saved on the device.

Mission briefings and steps show interactive, rotatable NASA GLB models bundled in the app. Models and source notices are in `assets/models/`; the credit list is `assets/models/NASA_ATTRIBUTION.txt`. ASTRA is an independent educational game and is not endorsed by NASA.

## Missions

1. First Light — Moon
2. Moon Greenhouse — Moon
3. Storm Watch — Mars
4. Shadow Scout — Europa

Mission progress and accessibility choices are stored on-device.

## Android build

Requires Flutter 3.50 stable, JDK 17, Android SDK, and Android System WebView for the in-app 3D model viewer. Minimum Android version is API 24.

```powershell
flutter pub get
flutter run -d <android-device-id>
flutter build apk --release
```

The Android runner is included under `android/`. A debug APK is written to `build/app/outputs/flutter-apk/app-debug.apk`; a release APK is written beside it as `app-release.apk`.

The mobile experience presents touch-sized choices and does not require the Unity keyboard or joystick controls. Science outcomes and resource values are labelled as simulations where applicable.
