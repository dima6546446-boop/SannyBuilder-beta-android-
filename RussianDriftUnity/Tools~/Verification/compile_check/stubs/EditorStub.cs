using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace UnityEditor
{
    public class MenuItemAttribute : Attribute { public MenuItemAttribute(string p) { } public int priority { get; set; } }
    public class InitializeOnLoadAttribute : Attribute { }
    public static class EditorApplication
    {
        public static bool isPlayingOrWillChangePlaymode, isCompiling, isUpdating, isBatchMode;
        public static event Action delayCall;
        public static void Exit(int c) { }
    }
    public static class SessionState { public static bool GetBool(string k, bool d) { return d; } public static void SetBool(string k, bool v) { } }
    public static class EditorPrefs { public static bool GetBool(string k, bool d) { return d; } public static void SetBool(string k, bool v) { } }
    public static class EditorUtility
    {
        public static void DisplayProgressBar(string a, string b, float c) { }
        public static void ClearProgressBar() { }
        public static bool DisplayDialog(string a, string b, string c) { return true; }
        public static void SetDirty(UnityEngine.Object o) { }
        public static void CopySerialized(UnityEngine.Object a, UnityEngine.Object b) { }
        public static void RevealInFinder(string p) { }
    }
    public static class AssetDatabase
    {
        public static bool IsValidFolder(string p) { return true; }
        public static string CreateFolder(string a, string b) { return ""; }
        public static T LoadAssetAtPath<T>(string p) where T : UnityEngine.Object { return null; }
        public static UnityEngine.Object[] LoadAllAssetsAtPath(string p) { return null; }
        public static void CreateAsset(UnityEngine.Object o, string p) { }
        public static void AddObjectToAsset(UnityEngine.Object o, UnityEngine.Object a) { }
        public static bool DeleteAsset(string p) { return true; }
        public static bool Contains(UnityEngine.Object o) { return false; }
        public static void SaveAssets() { }
        public static void Refresh() { }
    }
    public enum SerializedPropertyType { Integer, Boolean, Float, String, Enum, ObjectReference }
    public class SerializedProperty
    {
        public SerializedPropertyType propertyType; public int intValue; public bool boolValue; public float floatValue; public string stringValue; public int enumValueIndex;
        public UnityEngine.Object objectReferenceValue; public int arraySize;
        public SerializedProperty FindPropertyRelative(string n) { return null; }
        public SerializedProperty GetArrayElementAtIndex(int i) { return null; }
        public void InsertArrayElementAtIndex(int i) { }
        public void DeleteArrayElementAtIndex(int i) { }
    }
    public class SerializedObject
    {
        public SerializedObject(UnityEngine.Object o) { }
        public SerializedProperty FindProperty(string n) { return null; }
        public bool ApplyModifiedProperties() { return true; }
        public bool ApplyModifiedPropertiesWithoutUndo() { return true; }
    }
    [Flags] public enum BuildOptions { None = 0, Development = 1, AllowDebugging = 2 }
    public enum BuildTarget { Android }
    public enum BuildTargetGroup { Android }
    public enum UIOrientation { AutoRotation }
    public enum ScriptingImplementation { IL2CPP }
    public enum ManagedStrippingLevel { Medium }
    public enum AndroidSdkVersions { AndroidApiLevel26, AndroidApiLevelAuto }
    public enum AndroidArchitecture { ARM64 }
    public enum MobileTextureSubtarget { ASTC }
    public enum Il2CppCompilerConfiguration { Release }
    public static class PlayerSettings
    {
        public static string companyName, productName, bundleVersion;
        public static ColorSpace colorSpace;
        public static UIOrientation defaultInterfaceOrientation;
        public static bool allowedAutorotateToLandscapeLeft, allowedAutorotateToLandscapeRight, allowedAutorotateToPortrait, allowedAutorotateToPortraitUpsideDown, gcIncremental, MTRendering, stripEngineCode;
        public static void SetApplicationIdentifier(Build.NamedBuildTarget t, string id) { }
        public static void SetScriptingBackend(Build.NamedBuildTarget t, ScriptingImplementation s) { }
        public static void SetManagedStrippingLevel(Build.NamedBuildTarget t, ManagedStrippingLevel s) { }
        public static void SetIl2CppCompilerConfiguration(Build.NamedBuildTarget t, Il2CppCompilerConfiguration s) { }
        public static void SetUseDefaultGraphicsAPIs(BuildTarget t, bool b) { }
        public static void SetGraphicsAPIs(BuildTarget t, GraphicsDeviceType[] a) { }
        public static class Android
        {
            public static AndroidSdkVersions minSdkVersion, targetSdkVersion; public static AndroidArchitecture targetArchitectures; public static bool optimizedFramePacing, useCustomKeystore;
            public static int bundleVersionCode; public static string keystoreName, keystorePass, keyaliasName, keyaliasPass;
        }
    }
    public static class EditorUserBuildSettings
    {
        public static MobileTextureSubtarget androidBuildSubtarget; public static BuildTarget activeBuildTarget; public static bool buildAppBundle;
        public static bool SwitchActiveBuildTarget(BuildTargetGroup g, BuildTarget t) { return true; }
    }
    public class EditorBuildSettingsScene { public EditorBuildSettingsScene(string p, bool e) { } }
    public static class EditorBuildSettings { public static EditorBuildSettingsScene[] scenes; }
    public struct BuildPlayerOptions { public string[] scenes; public string locationPathName; public BuildTarget target; public BuildOptions options; }
    namespace Build
    {
        public struct NamedBuildTarget { public static NamedBuildTarget Android; }
        namespace Reporting
        {
            public enum BuildResult { Succeeded, Failed }
            public class BuildSummary { public BuildResult result; public ulong totalSize; public TimeSpan totalTime; public int totalErrors; }
            public class BuildReport { public BuildSummary summary; }
        }
    }
    public static class BuildPipeline { public static Build.Reporting.BuildReport BuildPlayer(BuildPlayerOptions o) { return null; } }
    namespace SceneManagement
    {
        public enum NewSceneSetup { EmptyScene } public enum NewSceneMode { Single }
        public static class EditorSceneManager
        {
            public static UnityEngine.SceneManagement.Scene NewScene(NewSceneSetup s, NewSceneMode m) { return default(UnityEngine.SceneManagement.Scene); }
            public static bool SaveScene(UnityEngine.SceneManagement.Scene s, string p) { return true; }
            public static UnityEngine.SceneManagement.Scene OpenScene(string p) { return default(UnityEngine.SceneManagement.Scene); }
        }
    }
    public static class PrefabUtility { public static GameObject SaveAsPrefabAsset(GameObject g, string p) { return g; } }
}
