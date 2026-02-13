#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using System.Diagnostics.CodeAnalysis;

namespace AbyssMoth
{
    [SuppressMessage("ReSharper", "MergeIntoPattern")]
    [SuppressMessage("ReSharper", "ForCanBeConvertedToForeach")]
    public static class NodeFrameworkMenuItems
    {
        private const string menuRootEdit = "Edit/AbyssMoth Node Framework/";
        private const string menuRootGameObject = "GameObject/AbyssMoth Node Framework/";
        private const string menuRootAssetsCreate = "Assets/Create/AbyssMoth Node Framework/";

        private const string defaultFrameworkRoot = "Assets/AbyssMothNodeFramework";
        private const string defaultResourcesRoot = defaultFrameworkRoot + "/Resources";
        private const string defaultResourcesPath = defaultResourcesRoot + "/AbyssMothNodeFramework";
        private const string defaultProjectRootPrefabName = "ProjectRootConnector";
        private const string defaultProjectRootPrefabPath = defaultResourcesPath + "/" + defaultProjectRootPrefabName + ".prefab";
        private const string defaultDebugConfigAssetName = "ConnectorDebugConfig";
        private const string defaultDebugConfigAssetPath = defaultResourcesPath + "/" + defaultDebugConfigAssetName + ".asset";
       
        private const string SceneConnectorDebugConfigFieldName = "debugConfig";

        [MenuItem(menuRootEdit + "Initialize Project", priority = 1)]
        public static void InitializeProject()
        {
            EnsureProjectArtifacts(focusAsset: true);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem(menuRootEdit + "Validate Current Scenes", priority = 50)]
        public static void ValidateCurrentScenes()
        {
            EnsureProjectArtifacts(focusAsset: false);

            RunOnCurrentScenes(saveAfter: true, () =>
            {
                var ok = true;

                for (var i = 0; i < SceneManager.sceneCount; i++)
                {
                    var scene = SceneManager.GetSceneAt(i);

                    if (!scene.isLoaded)
                    {
                        continue;
                    }

                    ok &= ValidateScene(
                        scene,
                        autoFix: true,
                        autoCollectSceneConnectors: true,
                        autoCollectLocalNodes: true);
                }

                if (ok)
                {
                    Debug.Log("Validation finished: no errors");
                }
            });
        }

        [MenuItem(menuRootEdit + "Validate All Build Scenes", priority = 51)]
        public static void ValidateAllBuildScenes()
        {
            EnsureProjectArtifacts(focusAsset: false);

            RunPreserveSceneSetup(() =>
            {
                var scenes = EditorBuildSettings.scenes;
                var ok = true;

                for (var i = 0; i < scenes.Length; i++)
                {
                    var buildScene = scenes[i];

                    if (!buildScene.enabled)
                    {
                        continue;
                    }

                    var path = buildScene.path;

                    if (string.IsNullOrEmpty(path))
                    {
                        continue;
                    }

                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    ok &= ValidateScene(
                        scene,
                        autoFix: true,
                        autoCollectSceneConnectors: true,
                        autoCollectLocalNodes: true);

                    EditorSceneManager.SaveScene(scene);
                }

                if (ok)
                {
                    Debug.Log("All build scenes validated successfully");
                }
            });
        }

        [MenuItem(menuRootEdit + "> Validate Full Current Scenes", priority = 49)]
        public static void ValidateFullCurrentScenes()
        {
            EnsureProjectArtifacts(focusAsset: false);

            RunOnCurrentScenes(saveAfter: true, () =>
            {
                var ok = true;

                for (var i = 0; i < SceneManager.sceneCount; i++)
                {
                    var scene = SceneManager.GetSceneAt(i);

                    if (!scene.isLoaded)
                    {
                        continue;
                    }

                    ok &= ValidateScene(scene, autoFix: true, autoCollectSceneConnectors: true,
                        autoCollectLocalNodes: true);
                }

                if (ok)
                {
                    Debug.Log("Full validation finished: no errors");
                }
            });
        }

        [MenuItem(menuRootEdit + "> Validate Full All Build Scenes", priority = 50)]
        public static void ValidateFullAllBuildScenes()
        {
            EnsureProjectArtifacts(focusAsset: false);

            RunPreserveSceneSetup(() =>
            {
                var scenes = EditorBuildSettings.scenes;
                var ok = true;

                for (var i = 0; i < scenes.Length; i++)
                {
                    var buildScene = scenes[i];

                    if (!buildScene.enabled)
                    {
                        continue;
                    }

                    var path = buildScene.path;

                    if (string.IsNullOrEmpty(path))
                    {
                        continue;
                    }

                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    ok &= ValidateScene(scene, autoFix: true, autoCollectSceneConnectors: true,
                        autoCollectLocalNodes: true);
                    EditorSceneManager.SaveScene(scene);
                }

                if (ok)
                {
                    Debug.Log("Full validation for all build scenes finished: no errors");
                }
            });
        }

        [MenuItem(menuRootEdit + "> Validate Prefabs (Collect LocalConnector Nodes)", priority = 52)]
        public static void ValidatePrefabs()
        {
            EnsureProjectArtifacts(focusAsset: false);

            var guids = AssetDatabase.FindAssets("t:Prefab");

            try
            {
                for (var i = 0; i < guids.Length; i++)
                {
                    var guid = guids[i];
                    var path = AssetDatabase.GUIDToAssetPath(guid);

                    EditorUtility.DisplayProgressBar(
                        "Validate Prefabs",
                        path,
                        guids.Length > 0 ? (float)i / guids.Length : 1f);

                    ValidatePrefabAtPath(path);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("Prefabs validated: LocalConnector nodes collected where applicable");
        }

        [MenuItem(menuRootEdit + "Validate Then Run", priority = 80)]
        public static void ValidateThenRun()
        {
            var ok = true;

            EnsureProjectArtifacts(focusAsset: false);

            RunOnCurrentScenes(saveAfter: true, () =>
            {
                for (var i = 0; i < SceneManager.sceneCount; i++)
                {
                    var scene = SceneManager.GetSceneAt(i);

                    if (!scene.isLoaded)
                    {
                        continue;
                    }

                    ok &= ValidateScene(
                        scene,
                        autoFix: true,
                        autoCollectSceneConnectors: true,
                        autoCollectLocalNodes: true);
                }
            });

            if (ok)
            {
                EditorApplication.isPlaying = true;
            }
        }

        [MenuItem(menuRootGameObject + "Create Scene Connector", priority = 10)]
        public static void CreateSceneConnector()
        {
            EnsureProjectArtifacts(focusAsset: false);

            var scene = SceneManager.GetActiveScene();

            if (!scene.isLoaded)
            {
                Debug.LogError("Active scene is not loaded");
                return;
            }

            var existing = FindSceneConnectors(scene);

            if (existing.Count > 0)
            {
                Selection.activeGameObject = existing[0].gameObject;
                EditorGUIUtility.PingObject(existing[0].gameObject);
                Debug.LogWarning($"SceneConnector already exists in scene: {scene.name}");
                return;
            }

            var go = new GameObject("SceneConnector");
            var connector = go.AddComponent<SceneConnector>();

            TryAssignDebugConfig(connector, scene);

            SceneManager.MoveGameObjectToScene(go, scene);
            Selection.activeGameObject = go;

            connector.CollectConnectors();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        [MenuItem(menuRootGameObject + "Add Local Connector To Selection", priority = 11)]
        public static void AddLocalConnectorToSelection()
        {
            var selected = Selection.activeGameObject;

            if (selected == null)
            {
                var go = new GameObject("LocalConnector");
                go.AddComponent<LocalConnector>();
                Selection.activeGameObject = go;
                return;
            }

            var local = selected.GetComponent<LocalConnector>();

            if (local == null)
            {
                local = selected.AddComponent<LocalConnector>();
            }

            local.CollectNodes();

            EditorSceneManager.MarkSceneDirty(selected.scene);
            EditorSceneManager.SaveScene(selected.scene);
            Selection.activeGameObject = selected;
        }

        [MenuItem(menuRootAssetsCreate + "Project Root Connector Prefab", priority = 40)]
        public static void CreateProjectRootPrefabFromProjectWindow()
        {
            var folder = TryGetSelectedFolderAssetPath();

            if (string.IsNullOrEmpty(folder))
            {
                EditorUtility.DisplayDialog("Error", "Select a folder in Project window first", "Ok");
                return;
            }

            if (!folder.Contains("/Resources", StringComparison.Ordinal))
            {
                EditorUtility.DisplayDialog(
                    "Error",
                    "ProjectRootConnector prefab must be placed inside a folder named 'Resources'",
                    "Ok");
                return;
            }

            var prefabPath = $"{folder}/{defaultProjectRootPrefabName}.prefab";

            if (File.Exists(GetAbsolutePath(prefabPath)))
            {
                var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                return;
            }

            var prefab = CreateProjectRootPrefabAtPath(prefabPath);
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);

            Debug.Log($"Created ProjectRootConnector at: {prefabPath}");
        }

        private static bool ValidateScene(Scene scene, bool autoFix, bool autoCollectSceneConnectors, bool autoCollectLocalNodes)
        {
            var ok = true;

            EnsureProjectRootExists(autoFix, ref ok);

            if (autoCollectLocalNodes)
            {
                CollectLocalNodesInScene(scene);
            }

            var connectors = FindSceneConnectors(scene);

            if (connectors.Count == 0)
            {
                if (!autoFix)
                {
                    Debug.LogError($"SceneConnector missing in scene: {scene.name}");
                    return false;
                }

                var go = new GameObject("SceneConnector");
                var connector = go.AddComponent<SceneConnector>();
                TryAssignDebugConfig(connector, scene);
                SceneManager.MoveGameObjectToScene(go, scene);

                if (autoCollectSceneConnectors)
                {
                    TryAssignDebugConfig(connector, scene);
                    connector.CollectConnectors();
                    EditorUtility.SetDirty(connector);
                }

                EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log($"SceneConnector created in scene: {scene.name}");
                return true;
            }

            if (connectors.Count > 1)
            {
                ok = false;
                Debug.LogError($"Multiple SceneConnector found in scene: {scene.name}. Count: {connectors.Count}");
            }

            if (autoCollectSceneConnectors)
            {
                for (var i = 0; i < connectors.Count; i++)
                {
                    var connector = connectors[i];

                    if (connector == null)
                    {
                        continue;
                    }

                    connector.CollectConnectors();
                    EditorUtility.SetDirty(connector);
                }

                EditorSceneManager.MarkSceneDirty(scene);
            }

            return ok;
        }

        private static void CollectLocalNodesInScene(Scene scene)
        {
            var roots = scene.GetRootGameObjects();

            for (var i = 0; i < roots.Length; i++)
            {
                var root = roots[i];

                if (root == null)
                {
                    continue;
                }

                var locals = root.GetComponentsInChildren<LocalConnector>(includeInactive: true);

                for (var j = 0; j < locals.Length; j++)
                {
                    var local = locals[j];

                    if (local == null)
                    {
                        continue;
                    }

                    if (local.gameObject.scene != scene)
                    {
                        continue;
                    }

                    if (local.GetComponentInParent<ProjectRootConnector>(includeInactive: true) != null)
                    {
                        continue;
                    }

                    local.CollectNodes();
                    EditorUtility.SetDirty(local);
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void EnsureProjectRootExists(bool autoFix, ref bool ok)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(defaultProjectRootPrefabPath);

            if (prefab != null)
                return;

            if (!autoFix)
            {
                ok = false;
                Debug.LogError("ProjectRootConnector prefab not found in project Assets. Run Initialize Project.");
                return;
            }

            EnsureProjectArtifacts(focusAsset: false);

            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(defaultProjectRootPrefabPath);

            if (prefab == null)
                ok = false;

            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(defaultProjectRootPrefabPath);

            if (prefab == null)
                ok = false;
        }

        private static void ValidatePrefabAtPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            if (!path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            GameObject root = null;

            try
            {
                root = PrefabUtility.LoadPrefabContents(path);

                if (root == null)
                {
                    return;
                }

                var locals = root.GetComponentsInChildren<LocalConnector>(true);

                if (locals == null || locals.Length == 0)
                {
                    return;
                }

                for (var i = 0; i < locals.Length; i++)
                {
                    var local = locals[i];

                    if (local == null)
                    {
                        continue;
                    }

                    local.CollectNodes();
                    EditorUtility.SetDirty(local);
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            catch (Exception e)
            {
                Debug.LogError($"Prefab validation failed: {path}\n{e}");
            }
            finally
            {
                if (root != null)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        private static List<SceneConnector> FindSceneConnectors(Scene scene)
        {
            var list = new List<SceneConnector>(capacity: 4);
            var roots = scene.GetRootGameObjects();

            for (var i = 0; i < roots.Length; i++)
            {
                var root = roots[i];

                if (root == null)
                {
                    continue;
                }

                var found = root.GetComponentsInChildren<SceneConnector>(includeInactive: true);

                for (var j = 0; j < found.Length; j++)
                {
                    var item = found[j];

                    if (item == null)
                    {
                        continue;
                    }

                    if (item.gameObject.scene == scene)
                    {
                        list.Add(item);
                    }
                }
            }

            return list;
        }

        private static List<string> FindProjectRootPrefabPaths()
        {
            var result = new List<string>(capacity: 4);
            var guids = AssetDatabase.FindAssets("t:Prefab");

            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);

                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab == null)
                {
                    continue;
                }

                if (prefab.GetComponent<ProjectRootConnector>() != null)
                {
                    result.Add(path);
                }
            }

            return result;
        }

        private static GameObject CreateProjectRootPrefabAtPath(string prefabPath)
        {
            var go = new GameObject(defaultProjectRootPrefabName);

            try
            {
                var root = go.GetComponent<ProjectRootConnector>();

                if (root == null)
                    root = go.AddComponent<ProjectRootConnector>();

                root.OnValidate();
                root.CollectNodes();

                var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static void RunPreserveSceneSetup(Action action)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var setup = EditorSceneManager.GetSceneManagerSetup();

            try
            {
                action?.Invoke();
            }
            finally
            {
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

        private static void RunOnCurrentScenes(bool saveAfter, Action action)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            action?.Invoke();

            if (!saveAfter)
            {
                return;
            }

            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);

                if (!scene.isLoaded)
                {
                    continue;
                }

                if (!scene.isDirty)
                {
                    continue;
                }

                EditorSceneManager.SaveScene(scene);
            }
        }

        private static void EnsureFolder(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return;
            }

            if (AssetDatabase.IsValidFolder(assetPath))
            {
                return;
            }

            var parent = Path.GetDirectoryName(assetPath)?.Replace("\\", "/");
            var name = Path.GetFileName(assetPath);

            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
            {
                return;
            }

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static string TryGetSelectedFolderAssetPath()
        {
            var objects = Selection.GetFiltered<UnityEngine.Object>(SelectionMode.Assets);

            if (objects == null || objects.Length == 0)
            {
                return null;
            }

            var path = AssetDatabase.GetAssetPath(objects[0]);

            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            if (AssetDatabase.IsValidFolder(path))
            {
                return path;
            }

            var folder = Path.GetDirectoryName(path)?.Replace("\\", "/");
            return folder;
        }

        private static string GetAbsolutePath(string assetPath)
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var full = Path.GetFullPath(Path.Combine(projectRoot, assetPath));
            return full;
        }

        private static void EnsureProjectArtifacts(bool focusAsset)
        {
            EnsureFolder(defaultFrameworkRoot);
            EnsureFolder(defaultResourcesRoot);
            EnsureFolder(defaultResourcesPath);

            EnsureProjectRootPrefab(focusAsset);
            EnsureDebugConfig();

            PurgePackageGeneratedAssets();
        }

        private static void PurgePackageGeneratedAssets()
        {
            PurgePackageProjectRootPrefabs();
            PurgePackageDebugConfigs();
        }

        private static void PurgePackageProjectRootPrefabs()
        {
            var paths = FindProjectRootPrefabPaths();

            for (var i = 0; i < paths.Count; i++)
            {
                var path = paths[i];

                if (!path.StartsWith("Packages/", StringComparison.Ordinal))
                    continue;

                if (string.Equals(path, defaultProjectRootPrefabPath, StringComparison.Ordinal))
                    continue;

                if (!string.Equals(Path.GetFileName(path), defaultProjectRootPrefabName + ".prefab",
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                TryDeleteAsset(path);
            }
        }

        private static void PurgePackageDebugConfigs()
        {
            var paths = FindAssetsPathsByType<ConnectorDebugConfig>();

            for (var i = 0; i < paths.Count; i++)
            {
                var path = paths[i];

                if (!path.StartsWith("Packages/", StringComparison.Ordinal))
                    continue;

                if (string.Equals(path, defaultDebugConfigAssetPath, StringComparison.Ordinal))
                    continue;

                if (!string.Equals(Path.GetFileName(path), defaultDebugConfigAssetName + ".asset",
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                TryDeleteAsset(path);
            }
        }

        private static void TryDeleteAsset(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return;

            if (assetPath.StartsWith("Packages/", StringComparison.Ordinal))
                return;

            AssetDatabase.DeleteAsset(assetPath);
        }

        private static void EnsureProjectRootPrefab(bool focusAsset)
        {
            var already = AssetDatabase.LoadAssetAtPath<GameObject>(defaultProjectRootPrefabPath);

            if (already != null)
            {
                if (focusAsset)
                {
                    Selection.activeObject = already;
                    EditorGUIUtility.PingObject(already);
                }

                return;
            }

            var existingPaths = FindProjectRootPrefabPaths();

            var assetsPaths = new List<string>(capacity: 2);
            var packagePaths = new List<string>(capacity: 2);

            for (var i = 0; i < existingPaths.Count; i++)
            {
                var path = existingPaths[i];

                if (path.StartsWith("Assets/", StringComparison.Ordinal))
                    assetsPaths.Add(path);
                else if (path.StartsWith("Packages/", StringComparison.Ordinal))
                    packagePaths.Add(path);
            }

            if (assetsPaths.Count > 1)
            {
                Debug.LogError($"Multiple ProjectRootConnector prefabs found in Assets. Count: {assetsPaths.Count}");
                return;
            }

            if (assetsPaths.Count == 1)
            {
                DeployAsset(assetsPaths[0], defaultProjectRootPrefabPath);
            }
            else if (packagePaths.Count >= 1)
            {
                DeployAsset(packagePaths[0], defaultProjectRootPrefabPath);
            }
            else
            {
                CreateProjectRootPrefabAtPath(defaultProjectRootPrefabPath);
            }

            var created = AssetDatabase.LoadAssetAtPath<GameObject>(defaultProjectRootPrefabPath);

            if (focusAsset && created != null)
            {
                Selection.activeObject = created;
                EditorGUIUtility.PingObject(created);
            }
        }

        private static void EnsureDebugConfig()
        {
            var already = AssetDatabase.LoadAssetAtPath<ConnectorDebugConfig>(defaultDebugConfigAssetPath);

            if (already != null)
                return;

            var found = FindAssetsPathsByType<ConnectorDebugConfig>();

            for (var i = 0; i < found.Count; i++)
            {
                var path = found[i];

                if (path.StartsWith("Assets/", StringComparison.Ordinal))
                {
                    DeployAsset(path, defaultDebugConfigAssetPath);
                    return;
                }
            }

            for (var i = 0; i < found.Count; i++)
            {
                var path = found[i];

                if (path.StartsWith("Packages/", StringComparison.Ordinal))
                {
                    DeployAsset(path, defaultDebugConfigAssetPath);
                    return;
                }
            }

            var config = ScriptableObject.CreateInstance<ConnectorDebugConfig>();
            AssetDatabase.CreateAsset(config, defaultDebugConfigAssetPath);
        }

        private static List<string> FindAssetsPathsByType<T>() where T : UnityEngine.Object
        {
            var result = new List<string>(capacity: 8);
            var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");

            for (var i = 0; i < guids.Length; i++)
            {
                var guid = guids[i];
                var path = AssetDatabase.GUIDToAssetPath(guid);

                if (!string.IsNullOrEmpty(path))
                    result.Add(path);
            }

            return result;
        }

        private static void DeployAsset(string from, string to)
        {
            if (string.Equals(from, to, StringComparison.Ordinal))
                return;

            if (from.StartsWith("Packages/", StringComparison.Ordinal) ||
                from.StartsWith("Library/PackageCache/", StringComparison.Ordinal))
            {
                var ok = AssetDatabase.CopyAsset(from, to);

                if (!ok)
                {
                    Debug.LogError($"CopyAsset failed\nFrom: {from}\nTo: {to}");
                    return;
                }

                // if (from.StartsWith("Packages/", StringComparison.Ordinal))
                //     TryDeleteAsset(from);
                // return;
            }

            var error = AssetDatabase.MoveAsset(from, to);

            if (!string.IsNullOrEmpty(error))
                Debug.LogError($"MoveAsset failed: {error}\nFrom: {from}\nTo: {to}");
        }
        
        private static void TryAssignDebugConfig(SceneConnector connector, Scene scene)
        {
            if (connector == null)
                return;

            var config = AssetDatabase.LoadAssetAtPath<ConnectorDebugConfig>(defaultDebugConfigAssetPath);
            if (config == null)
                return;

            var so = new SerializedObject(connector);
            var prop = so.FindProperty(SceneConnectorDebugConfigFieldName);
            if (prop == null)
                return;

            if (prop.objectReferenceValue != null)
                return;

            prop.objectReferenceValue = config;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(connector);

            if (scene.IsValid() && scene.isLoaded)
                EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
#endif