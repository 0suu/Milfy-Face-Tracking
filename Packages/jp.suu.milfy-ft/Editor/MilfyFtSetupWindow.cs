using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using nadena.dev.modular_avatar.core;
using Triturbo.BlendShapeShare.BlendShapeData;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using Object = UnityEngine.Object;

namespace Suu.MilfyFT.Editor
{
    internal sealed class MilfyFtSetupWindow : EditorWindow
    {
        private GameObject _sourceAvatar;
        private bool _enableMouthDefaultCompensation = true;
        private Vector2 _scrollPosition;

        [MenuItem("Tools/suu_MifyFT/setup")]
        private static void Open()
        {
            var window = GetWindow<MilfyFtSetupWindow>("Milfy FT Setup");
            window.minSize = new Vector2(440f, 300f);

            if (Selection.activeGameObject != null)
            {
                window._sourceAvatar = Selection.activeGameObject;
            }

            window.Show();
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Milfy Face Tracking Setup", EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);
            EditorGUILayout.HelpBox(
                "Hierarchy上の未セットアップMilfyを指定してください。\n" +
                "元のGameObjectとFBXは変更せず、複製したGameObjectだけにFT用MeshとMilfy_FT.prefabを設定します。",
                MessageType.Info);

            EditorGUILayout.Space(8f);
            _sourceAvatar = EditorGUILayout.ObjectField(
                new GUIContent("Milfy GameObject", "Project内のPrefabではなく、Hierarchy上のMilfyを指定します。"),
                _sourceAvatar,
                typeof(GameObject),
                true) as GameObject;

            _enableMouthDefaultCompensation = EditorGUILayout.ToggleLeft(
                new GUIContent(
                    "口開き時に既定口を相殺",
                    "現在のMilfyで非ゼロのmouth_* BlendShapeを取得し、wide/narrowを除いてJawOpenに比例して0へ補間します。"),
                _enableMouthDefaultCompensation);

            EditorGUILayout.Space(8f);

            bool isValid = MilfyFtSetupService.TryValidate(
                _sourceAvatar,
                out string validationMessage);

            EditorGUILayout.HelpBox(
                validationMessage,
                isValid ? MessageType.Info : MessageType.Warning);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("実行内容", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("1. 指定したMilfyをHierarchy上で複製");
            EditorGUILayout.LabelField("2. BlendShareでFT用Meshアセットを新規生成");
            EditorGUILayout.LabelField("3. 生成Meshを複製側のBodyだけへ割り当て");
            EditorGUILayout.LabelField("4. 複製側へMilfy_FT.prefabを追加");
            if (_enableMouthDefaultCompensation)
            {
                EditorGUILayout.LabelField("5. 現在の既定口をJawOpenに合わせて相殺");
            }

            EditorGUILayout.Space(12f);

            using (new EditorGUI.DisabledScope(!isValid))
            {
                if (GUILayout.Button("複製を作成してセットアップ", GUILayout.Height(36f)))
                {
                    RunSetup();
                }
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(
                "生成物は Assets/suu_MilfyFT/Generated に保存されます。" +
                "元のFBXへBlendShapeを直接書き込む処理は使用しません。\n" +
                "UnityのUndoで複製を取り消しても、生成したMeshアセットは自動削除されません。",
                MessageType.None);

            EditorGUILayout.EndScrollView();
        }

        private void RunSetup()
        {
            if (!MilfyFtSetupService.TryValidate(
                    _sourceAvatar,
                    out string validationMessage))
            {
                EditorUtility.DisplayDialog("Milfy FT Setup", validationMessage, "OK");
                return;
            }

            bool confirmed = EditorUtility.DisplayDialog(
                "Milfy FT Setup",
                $"「{_sourceAvatar.name}」を複製してFace Trackingをセットアップします。\n\n" +
                "元のGameObjectとFBXは変更しません。",
                "セットアップ",
                "キャンセル");

            if (!confirmed)
            {
                return;
            }

            try
            {
                MilfyFtSetupResult result = MilfyFtSetupService.Setup(
                    _sourceAvatar,
                    _enableMouthDefaultCompensation);
                Selection.activeGameObject = result.AvatarClone;
                EditorGUIUtility.PingObject(result.AvatarClone);

                EditorUtility.DisplayDialog(
                    "Milfy FT Setup",
                    "セットアップが完了しました。\n\n" +
                    $"複製: {result.AvatarClone.name}\n" +
                    $"FT用Mesh: {result.GeneratedMeshAssetPath}" +
                    (result.MouthDefaultCompensationControllerPath == null
                        ? string.Empty
                        : $"\n既定口相殺: {result.MouthDefaultCompensationBlendShapeCount}形状"),
                    "OK");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog(
                    "Milfy FT Setup",
                    "セットアップに失敗しました。生成途中の複製とMeshアセットのロールバックを試行しました。\n\n" +
                    exception.Message,
                    "OK");
            }
        }
    }

    public sealed class MilfyFtSetupResult
    {
        public GameObject AvatarClone { get; }
        public GeneratedMeshAssetSO GeneratedMeshAsset { get; }
        public string GeneratedMeshAssetPath { get; }
        public string MouthDefaultCompensationControllerPath { get; }
        public int MouthDefaultCompensationBlendShapeCount { get; }

        internal MilfyFtSetupResult(
            GameObject avatarClone,
            GeneratedMeshAssetSO generatedMeshAsset,
            string generatedMeshAssetPath,
            string mouthDefaultCompensationControllerPath,
            int mouthDefaultCompensationBlendShapeCount)
        {
            AvatarClone = avatarClone;
            GeneratedMeshAsset = generatedMeshAsset;
            GeneratedMeshAssetPath = generatedMeshAssetPath;
            MouthDefaultCompensationControllerPath =
                mouthDefaultCompensationControllerPath;
            MouthDefaultCompensationBlendShapeCount =
                mouthDefaultCompensationBlendShapeCount;
        }
    }

    public static class MilfyFtSetupService
    {
        private const string BlendShapeDataPath =
            "Packages/jp.suu.milfy-ft/Runtime/BlendShare/Milfy_FT_BlendShapes.asset";

        private const string MilfyFtPrefabPath =
            "Packages/jp.suu.milfy-ft/Runtime/Prefabs/Milfy_FT.prefab";

        private const string GeneratedRootFolder = "Assets/suu_MilfyFT/Generated";

        private const string JawOpenParameterName =
            "OSCm/Proxy/FT/v2/JawOpen";

        private const string LipTrackingParameterName = "LipTrackingActive";

        public static bool TryValidate(GameObject sourceAvatar, out string message)
        {
            if (sourceAvatar == null)
            {
                message = "Hierarchy上のMilfy GameObjectを指定してください。";
                return false;
            }

            if (EditorUtility.IsPersistent(sourceAvatar) ||
                !sourceAvatar.scene.IsValid() ||
                !sourceAvatar.scene.isLoaded)
            {
                message = "Project内のPrefabではなく、Hierarchy上のMilfy GameObjectを指定してください。";
                return false;
            }

            if (EditorSceneManager.IsPreviewScene(sourceAvatar.scene))
            {
                message = "Preview Scene上のGameObjectは対象にできません。通常のHierarchy上のMilfyを指定してください。";
                return false;
            }

            bool hasAvatarDescriptor = sourceAvatar
                .GetComponents<Component>()
                .Any(component =>
                    component != null &&
                    component.GetType().FullName ==
                    "VRC.SDK3.Avatars.Components.VRCAvatarDescriptor");

            if (!hasAvatarDescriptor)
            {
                message = "Milfyのアバタールートを指定してください。指定したGameObject直下にVRC Avatar Descriptorがありません。";
                return false;
            }

            if (!TryGetSingleBodyRenderer(
                    sourceAvatar,
                    out SkinnedMeshRenderer bodyRenderer,
                    out message))
            {
                return false;
            }

            if (bodyRenderer.sharedMesh == null)
            {
                message = "BodyにMeshが設定されていません。";
                return false;
            }

            string sourceMeshPath = AssetDatabase.GetAssetPath(bodyRenderer.sharedMesh);
            if (string.IsNullOrEmpty(sourceMeshPath) ||
                !string.Equals(
                    Path.GetExtension(sourceMeshPath),
                    ".fbx",
                    StringComparison.OrdinalIgnoreCase))
            {
                message = "Bodyが元のMilfy FBXを参照していません。未セットアップのMilfyを指定してください。";
                return false;
            }

            var sourceFbx = AssetDatabase.LoadMainAssetAtPath(sourceMeshPath) as GameObject;
            if (sourceFbx == null)
            {
                message = "Bodyが参照しているMilfy FBXを読み込めません。";
                return false;
            }

            var blendShapeData =
                AssetDatabase.LoadAssetAtPath<BlendShapeDataSO>(BlendShapeDataPath);

            if (blendShapeData == null)
            {
                message = "Milfy FT用BlendShareデータが見つかりません。パッケージを再導入してください。";
                return false;
            }

            if (!TryValidateBlendShapeData(blendShapeData, out message))
            {
                return false;
            }

            if (!BlendShapeAppender.IsAllMeshesValid(
                    new[] { blendShapeData },
                    new[] { bodyRenderer.sharedMesh }))
            {
                message = "Milfy v1.5.0で、FBXデータが改変されていないことを確認してください。";
                return false;
            }

            var ftPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MilfyFtPrefabPath);
            if (ftPrefab == null)
            {
                message = "Milfy_FT.prefabが見つかりません。パッケージを再導入してください。";
                return false;
            }

            if (ContainsMilfyFtPrefab(sourceAvatar, ftPrefab))
            {
                message = "指定したGameObjectには既にMilfy_FT.prefabが設定されています。" +
                          "未セットアップのMilfyを指定してください。";
                return false;
            }

            message = "セットアップ可能です。元のGameObjectとFBXは変更されません。";
            return true;
        }

        public static MilfyFtSetupResult Setup(
            GameObject sourceAvatar,
            bool enableMouthDefaultCompensation = true)
        {
            if (!TryValidate(sourceAvatar, out string validationMessage))
            {
                throw new InvalidOperationException(validationMessage);
            }

            TryGetSingleBodyRenderer(
                sourceAvatar,
                out SkinnedMeshRenderer sourceBodyRenderer,
                out _);

            Mesh originalBodyMesh = sourceBodyRenderer.sharedMesh;
            var originalRendererMeshes = sourceAvatar
                .GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .ToDictionary(renderer => renderer, renderer => renderer.sharedMesh);
            string sourceMeshPath = AssetDatabase.GetAssetPath(originalBodyMesh);
            var sourceFbx = AssetDatabase.LoadMainAssetAtPath(sourceMeshPath) as GameObject;
            var blendShapeData =
                AssetDatabase.LoadAssetAtPath<BlendShapeDataSO>(BlendShapeDataPath);
            var ftPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MilfyFtPrefabPath);

            AssetDatabase.Refresh();
            EnsureAssetFolder(GeneratedRootFolder);

            string generatedMeshAssetPath = AssetDatabase.GenerateUniqueAssetPath(
                $"{GeneratedRootFolder}/{SanitizeFileName(sourceAvatar.name)}_FT_Mesh.asset");

            if (AssetDatabase.LoadMainAssetAtPath(generatedMeshAssetPath) != null)
            {
                throw new InvalidOperationException(
                    "FT用Meshの一意な保存先を確保できませんでした。");
            }

            List<MouthDefaultTarget> mouthDefaultTargets =
                enableMouthDefaultCompensation
                    ? CollectMouthDefaultTargets(sourceAvatar)
                    : null;

            if (enableMouthDefaultCompensation &&
                mouthDefaultTargets.Count == 0)
            {
                throw new InvalidOperationException(
                    "既定口の相殺を有効にしましたが、wide/narrow以外で非ゼロのmouth_* BlendShapeが見つかりません。" +
                    "Milfyの既定口を確認するか、このオプションをオフにしてください。");
            }

            GameObject avatarClone = null;
            var generatedAssetPaths = new List<string>
            {
                generatedMeshAssetPath,
            };
            Object[] previousSelection = Selection.objects;
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Milfy Face Tracking");

            try
            {
                GeneratedMeshAssetSO generatedMeshAsset =
                    BlendShapeAppender.CreateMeshAsset(
                        sourceFbx,
                        new[] { blendShapeData },
                        generatedMeshAssetPath);

                if (generatedMeshAsset == null)
                {
                    throw new InvalidOperationException(
                        "BlendShareによるFT用Meshの生成に失敗しました。" +
                        "Consoleのエラーを確認してください。");
                }

                Mesh generatedBodyMesh = ValidateGeneratedMeshAsset(
                    generatedMeshAsset,
                    blendShapeData,
                    originalBodyMesh);

                Selection.objects = new Object[] { sourceAvatar };
                Unsupported.DuplicateGameObjectsUsingPasteboard();
                avatarClone = Selection.activeGameObject;

                if (avatarClone == null ||
                    avatarClone == sourceAvatar ||
                    avatarClone.scene != sourceAvatar.scene ||
                    avatarClone.transform.parent != sourceAvatar.transform.parent)
                {
                    throw new InvalidOperationException(
                        "Hierarchy上でMilfyを安全に複製できませんでした。");
                }

                avatarClone.name = $"{sourceAvatar.name}_FT";
                avatarClone.transform.SetSiblingIndex(
                    sourceAvatar.transform.GetSiblingIndex() + 1);

                if (PrefabUtility.GetPrefabInstanceStatus(sourceAvatar) ==
                        PrefabInstanceStatus.Connected &&
                    PrefabUtility.GetPrefabInstanceStatus(avatarClone) !=
                        PrefabInstanceStatus.Connected)
                {
                    throw new InvalidOperationException(
                        "複製側のPrefab接続を維持できなかったため処理を中止しました。");
                }

                if (!TryGetSingleBodyRenderer(
                        avatarClone,
                        out SkinnedMeshRenderer cloneBodyRenderer,
                        out string cloneBodyError))
                {
                    throw new InvalidOperationException(cloneBodyError);
                }

                var ftInstance = PrefabUtility.InstantiatePrefab(
                    ftPrefab,
                    avatarClone.transform) as GameObject;

                if (ftInstance == null)
                {
                    throw new InvalidOperationException(
                        "Milfy_FT.prefabを複製側へ追加できませんでした。");
                }

                Undo.RegisterCreatedObjectUndo(ftInstance, "Add Milfy FT prefab");
                ftInstance.transform.localPosition = Vector3.zero;
                ftInstance.transform.localRotation = Quaternion.identity;
                ftInstance.transform.localScale = Vector3.one;

                generatedMeshAsset.ApplyMesh(avatarClone.transform);

                if (cloneBodyRenderer.sharedMesh != generatedBodyMesh ||
                    AssetDatabase.GetAssetPath(cloneBodyRenderer.sharedMesh) !=
                    generatedMeshAssetPath)
                {
                    throw new InvalidOperationException(
                        "生成したFT用Meshを複製側のBodyへ割り当てられませんでした。");
                }

                bool sourceChanged = originalRendererMeshes.Any(
                    pair => pair.Key == null || pair.Key.sharedMesh != pair.Value);
                if (sourceChanged)
                {
                    throw new InvalidOperationException(
                        "元のアバター側のMesh参照が変更されたため処理を中止しました。");
                }

                MouthDefaultCompensationResult mouthCompensation = null;
                if (enableMouthDefaultCompensation)
                {
                    mouthCompensation = CreateMouthDefaultCompensation(
                        avatarClone,
                        mouthDefaultTargets,
                        generatedAssetPaths);
                }

                EditorSceneManager.MarkSceneDirty(avatarClone.scene);
                Undo.CollapseUndoOperations(undoGroup);

                return new MilfyFtSetupResult(
                    avatarClone,
                    generatedMeshAsset,
                    generatedMeshAssetPath,
                    mouthCompensation?.ControllerPath,
                    mouthCompensation?.BlendShapeCount ?? 0);
            }
            catch (Exception setupException)
            {
                bool sceneRollbackSucceeded = true;
                bool assetCleanupSucceeded = true;

                try
                {
                    Undo.RevertAllDownToGroup(undoGroup);
                }
                catch (Exception rollbackException)
                {
                    sceneRollbackSucceeded = false;
                    Debug.LogException(rollbackException);
                }

                if (avatarClone != null)
                {
                    try
                    {
                        Object.DestroyImmediate(avatarClone);
                    }
                    catch (Exception cleanupException)
                    {
                        sceneRollbackSucceeded = false;
                        Debug.LogException(cleanupException);
                    }
                }

                Selection.objects = previousSelection;

                foreach (string generatedAssetPath in generatedAssetPaths.Distinct())
                {
                    if (AssetDatabase.LoadMainAssetAtPath(generatedAssetPath) != null)
                    {
                        assetCleanupSucceeded &=
                            AssetDatabase.DeleteAsset(generatedAssetPath);
                    }
                }

                foreach (KeyValuePair<SkinnedMeshRenderer, Mesh> pair in
                         originalRendererMeshes)
                {
                    if (pair.Key != null && pair.Key.sharedMesh != pair.Value)
                    {
                        pair.Key.sharedMesh = pair.Value;
                        EditorUtility.SetDirty(pair.Key);
                        sceneRollbackSucceeded = false;
                    }
                }

                AssetDatabase.SaveAssets();

                string cleanupMessage =
                    sceneRollbackSucceeded && assetCleanupSucceeded
                        ? "生成途中の複製とMeshアセットは削除済みです。"
                        : "ロールバックが完了していない可能性があります。" +
                          $"残存物を確認してください: {generatedMeshAssetPath}";

                throw new InvalidOperationException(
                    $"{setupException.Message}\n\n{cleanupMessage}",
                    setupException);
            }
        }

        private static bool TryGetSingleBodyRenderer(
            GameObject avatar,
            out SkinnedMeshRenderer bodyRenderer,
            out string message)
        {
            var bodyRenderers = avatar
                .GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(renderer =>
                    renderer.sharedMesh != null &&
                    renderer.sharedMesh.name == "Body")
                .ToArray();

            if (bodyRenderers.Length != 1)
            {
                bodyRenderer = null;
                message = bodyRenderers.Length == 0
                    ? "指定したGameObject内にBodyが見つかりません。"
                    : "指定したGameObject内にBodyが複数あります。" +
                      "対応する未改変Milfyを指定してください。";
                return false;
            }

            bodyRenderer = bodyRenderers[0];
            if (bodyRenderer.gameObject.name != "Body")
            {
                message = "Body Meshを参照するGameObject名が`Body`ではありません。未改変のMilfyを指定してください。";
                bodyRenderer = null;
                return false;
            }

            message = string.Empty;
            return true;
        }

        private static bool ContainsMilfyFtPrefab(
            GameObject avatar,
            GameObject ftPrefab)
        {
            foreach (Transform child in
                     avatar.GetComponentsInChildren<Transform>(true))
            {
                if (child == avatar.transform)
                {
                    continue;
                }

                if (PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject) ==
                    ftPrefab)
                {
                    return true;
                }

                if (child.gameObject.name == ftPrefab.name)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryValidateBlendShapeData(
            BlendShapeDataSO blendShapeData,
            out string message)
        {
            if (blendShapeData.m_MeshDataList == null ||
                blendShapeData.m_MeshDataList.Count != 1 ||
                blendShapeData.m_MeshDataList[0].m_MeshName != "Body")
            {
                message = "Milfy FT用BlendShareデータのBody構成が不正です。パッケージを再導入してください。";
                return false;
            }

            List<string> shapeNames =
                blendShapeData.m_MeshDataList[0].m_ShapeNames;

            if (shapeNames == null ||
                shapeNames.Count != 39 ||
                shapeNames.Distinct().Count() != 39)
            {
                message = "Milfy FT用BlendShareデータが39形状ではありません。パッケージを再導入してください。";
                return false;
            }

            bool hasInvalidUnityBlendShapeData = blendShapeData
                .m_MeshDataList[0]
                .BlendShapes
                .Any(blendShape =>
                    blendShape.m_UnityBlendShapeData?.m_Frames == null ||
                    !blendShape.m_UnityBlendShapeData.m_Frames.Any(frame =>
                        frame?.m_VertexIndices != null &&
                        frame.m_DeltaVertices != null &&
                        frame.m_VertexIndices.Count > 0 &&
                        frame.m_DeltaVertices.Count ==
                        frame.m_VertexIndices.Count));

            if (hasInvalidUnityBlendShapeData)
            {
                message =
                    "Milfy FT用BlendShareデータの頂点差分が欠損しています。パッケージを再導入してください。";
                return false;
            }

            message = string.Empty;
            return true;
        }

        private static List<MouthDefaultTarget> CollectMouthDefaultTargets(
            GameObject avatar)
        {
            var targets = new List<MouthDefaultTarget>();
            if (!TryGetSingleBodyRenderer(
                    avatar,
                    out SkinnedMeshRenderer renderer,
                    out _))
            {
                return targets;
            }

            Mesh mesh = renderer.sharedMesh;
            if (mesh == null)
            {
                return targets;
            }

            string relativePath = AnimationUtility.CalculateTransformPath(
                renderer.transform,
                avatar.transform);

            for (int index = 0; index < mesh.blendShapeCount; index++)
            {
                string blendShapeName = mesh.GetBlendShapeName(index);
                if (!IsMouthDefaultCompensationTarget(blendShapeName))
                {
                    continue;
                }

                float defaultValue = renderer.GetBlendShapeWeight(index);
                if (Mathf.Approximately(defaultValue, 0f))
                {
                    continue;
                }

                targets.Add(new MouthDefaultTarget(
                    relativePath,
                    blendShapeName,
                    defaultValue));
            }

            return targets;
        }

        private static bool IsMouthDefaultCompensationTarget(
            string blendShapeName)
        {
            return blendShapeName.StartsWith(
                       "mouth_",
                       StringComparison.OrdinalIgnoreCase) &&
                   blendShapeName.IndexOf(
                       "wide",
                       StringComparison.OrdinalIgnoreCase) < 0 &&
                   blendShapeName.IndexOf(
                       "narrow",
                       StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static MouthDefaultCompensationResult
            CreateMouthDefaultCompensation(
                GameObject avatarClone,
                IReadOnlyList<MouthDefaultTarget> targets,
                ICollection<string> generatedAssetPaths)
        {
            string fileNamePrefix = SanitizeFileName(avatarClone.name);
            string defaultClipPath = AssetDatabase.GenerateUniqueAssetPath(
                $"{GeneratedRootFolder}/{fileNamePrefix}_MouthDefault.anim");
            string cancelClipPath = AssetDatabase.GenerateUniqueAssetPath(
                $"{GeneratedRootFolder}/{fileNamePrefix}_MouthCancel.anim");
            string controllerPath = AssetDatabase.GenerateUniqueAssetPath(
                $"{GeneratedRootFolder}/{fileNamePrefix}_MouthCompensation.controller");

            generatedAssetPaths.Add(defaultClipPath);
            generatedAssetPaths.Add(cancelClipPath);
            generatedAssetPaths.Add(controllerPath);

            AnimationClip defaultClip = CreateMouthDefaultClip(
                defaultClipPath,
                targets,
                false);
            AnimationClip cancelClip = CreateMouthDefaultClip(
                cancelClipPath,
                targets,
                true);
            AnimatorController controller = CreateMouthDefaultController(
                controllerPath,
                defaultClip,
                cancelClip);

            var mergeAnimator = Undo.AddComponent<ModularAvatarMergeAnimator>(
                avatarClone);
            mergeAnimator.animator = controller;
            mergeAnimator.layerType = VRCAvatarDescriptor.AnimLayerType.FX;
            mergeAnimator.deleteAttachedAnimator = false;
            mergeAnimator.pathMode = MergeAnimatorPathMode.Absolute;
            mergeAnimator.matchAvatarWriteDefaults = false;
            mergeAnimator.layerPriority = 900;
            mergeAnimator.mergeAnimatorMode = MergeAnimatorMode.Append;
            EditorUtility.SetDirty(mergeAnimator);

            AssetDatabase.SaveAssets();

            return new MouthDefaultCompensationResult(
                controllerPath,
                targets.Count);
        }

        private static AnimationClip CreateMouthDefaultClip(
            string assetPath,
            IEnumerable<MouthDefaultTarget> targets,
            bool cancelDefaultMouth)
        {
            var clip = new AnimationClip
            {
                name = Path.GetFileNameWithoutExtension(assetPath),
                frameRate = 60f,
            };

            foreach (MouthDefaultTarget target in targets)
            {
                var binding = EditorCurveBinding.FloatCurve(
                    target.RelativePath,
                    typeof(SkinnedMeshRenderer),
                    "blendShape." + target.BlendShapeName);
                float value = cancelDefaultMouth ? 0f : target.DefaultValue;
                AnimationUtility.SetEditorCurve(
                    clip,
                    binding,
                    AnimationCurve.Constant(0f, 1f / 60f, value));
            }

            AssetDatabase.CreateAsset(clip, assetPath);
            return clip;
        }

        private static AnimatorController CreateMouthDefaultController(
            string controllerPath,
            AnimationClip defaultClip,
            AnimationClip cancelClip)
        {
            AnimatorController controller =
                AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            controller.AddParameter(
                JawOpenParameterName,
                AnimatorControllerParameterType.Float);
            controller.AddParameter(
                LipTrackingParameterName,
                AnimatorControllerParameterType.Float);

            AnimatorControllerLayer layer = controller.layers[0];
            layer.name = "Milfy FT Mouth Default Compensation";
            layer.defaultWeight = 1f;

            AnimatorStateMachine stateMachine = layer.stateMachine;
            stateMachine.name = layer.name;
            stateMachine.states = Array.Empty<ChildAnimatorState>();
            stateMachine.anyStateTransitions =
                Array.Empty<AnimatorStateTransition>();

            var blendTree = new BlendTree
            {
                name = "Cancel default mouth by JawOpen",
                blendType = BlendTreeType.Simple1D,
                blendParameter = JawOpenParameterName,
                useAutomaticThresholds = false,
            };
            AssetDatabase.AddObjectToAsset(blendTree, controller);
            blendTree.AddChild(defaultClip, 0f);
            blendTree.AddChild(cancelClip, 1f);

            AnimatorState offState = stateMachine.AddState("Off", new Vector3(240f, 80f));
            offState.writeDefaultValues = false;

            AnimatorState activeState = stateMachine.AddState(
                "Cancel default mouth by JawOpen",
                new Vector3(240f, 220f));
            activeState.motion = blendTree;
            activeState.writeDefaultValues = false;
            stateMachine.defaultState = offState;

            AnimatorStateTransition activate =
                AddImmediateTransition(offState, activeState);
            activate.AddCondition(
                AnimatorConditionMode.Greater,
                0.5f,
                LipTrackingParameterName);
            activate.AddCondition(
                AnimatorConditionMode.Greater,
                0.001f,
                JawOpenParameterName);

            AnimatorStateTransition deactivateByJaw =
                AddImmediateTransition(activeState, offState);
            deactivateByJaw.AddCondition(
                AnimatorConditionMode.Less,
                0.001f,
                JawOpenParameterName);

            AnimatorStateTransition deactivateByTracking =
                AddImmediateTransition(activeState, offState);
            deactivateByTracking.AddCondition(
                AnimatorConditionMode.Less,
                0.5f,
                LipTrackingParameterName);

            controller.layers = new[] { layer };
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static AnimatorStateTransition AddImmediateTransition(
            AnimatorState from,
            AnimatorState to)
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.duration = 0f;
            transition.hasFixedDuration = true;
            transition.canTransitionToSelf = false;
            return transition;
        }

        private static Mesh ValidateGeneratedMeshAsset(
            GeneratedMeshAssetSO generatedMeshAsset,
            BlendShapeDataSO blendShapeData,
            Mesh originalBodyMesh)
        {
            string generatedPath = AssetDatabase.GetAssetPath(generatedMeshAsset);
            Mesh bodyMesh = AssetDatabase
                .LoadAllAssetRepresentationsAtPath(generatedPath)
                .OfType<Mesh>()
                .SingleOrDefault(mesh => mesh.name == "Body");

            if (bodyMesh == null)
            {
                throw new InvalidOperationException(
                    "生成したMeshアセット内にBodyがありません。");
            }

            if (bodyMesh.vertexCount != originalBodyMesh.vertexCount ||
                bodyMesh.subMeshCount != originalBodyMesh.subMeshCount ||
                bodyMesh.bindposes.Length != originalBodyMesh.bindposes.Length)
            {
                throw new InvalidOperationException(
                    "生成したBody Meshの頂点・SubMesh・BindPose構成が元のBodyと一致しません。");
            }

            var generatedShapeNames = new HashSet<string>();
            for (int index = 0; index < bodyMesh.blendShapeCount; index++)
            {
                generatedShapeNames.Add(bodyMesh.GetBlendShapeName(index));
            }

            string[] missingShapeNames = blendShapeData.m_MeshDataList
                .Where(meshData => meshData.m_MeshName == "Body")
                .SelectMany(meshData => meshData.m_ShapeNames)
                .Distinct()
                .Where(shapeName => !generatedShapeNames.Contains(shapeName))
                .ToArray();

            if (missingShapeNames.Length > 0)
            {
                throw new InvalidOperationException(
                    "FT用Meshに必要なBlendShapeを生成できませんでした: " +
                    string.Join(", ", missingShapeNames));
            }

            string[] emptyShapeNames = blendShapeData.m_MeshDataList
                .Where(meshData => meshData.m_MeshName == "Body")
                .SelectMany(meshData => meshData.m_ShapeNames)
                .Distinct()
                .Where(shapeName =>
                {
                    int shapeIndex = bodyMesh.GetBlendShapeIndex(shapeName);
                    int frameCount =
                        bodyMesh.GetBlendShapeFrameCount(shapeIndex);

                    for (int frameIndex = 0;
                         frameIndex < frameCount;
                         frameIndex++)
                    {
                        var deltaVertices =
                            new Vector3[bodyMesh.vertexCount];
                        var deltaNormals =
                            new Vector3[bodyMesh.vertexCount];
                        var deltaTangents =
                            new Vector3[bodyMesh.vertexCount];

                        bodyMesh.GetBlendShapeFrameVertices(
                            shapeIndex,
                            frameIndex,
                            deltaVertices,
                            deltaNormals,
                            deltaTangents);

                        if (deltaVertices.Any(delta =>
                                delta != Vector3.zero))
                        {
                            return false;
                        }
                    }

                    return true;
                })
                .ToArray();

            if (emptyShapeNames.Length > 0)
            {
                throw new InvalidOperationException(
                    "FT用Meshに頂点変位のないBlendShapeが生成されました: " +
                    string.Join(", ", emptyShapeNames));
            }

            string[] missingOriginalShapeNames = Enumerable
                .Range(0, originalBodyMesh.blendShapeCount)
                .Select(originalBodyMesh.GetBlendShapeName)
                .Where(shapeName => !generatedShapeNames.Contains(shapeName))
                .ToArray();

            if (missingOriginalShapeNames.Length > 0)
            {
                throw new InvalidOperationException(
                    "生成したBody Meshから元のBlendShapeが欠落しています: " +
                    string.Join(", ", missingOriginalShapeNames));
            }

            return bodyMesh;
        }

        private static void EnsureAssetFolder(string folderPath)
        {
            string[] segments = folderPath.Split('/');
            string current = segments[0];

            for (int index = 1; index < segments.Length; index++)
            {
                string next = $"{current}/{segments[index]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[index]);
                }

                current = next;
            }
        }

        private static string SanitizeFileName(string value)
        {
            char[] invalidCharacters = Path.GetInvalidFileNameChars();
            var sanitized = new string(
                value.Select(character =>
                        invalidCharacters.Contains(character) ? '_' : character)
                    .ToArray());

            return string.IsNullOrWhiteSpace(sanitized) ? "Milfy" : sanitized;
        }

        private sealed class MouthDefaultTarget
        {
            public string RelativePath { get; }
            public string BlendShapeName { get; }
            public float DefaultValue { get; }

            public MouthDefaultTarget(
                string relativePath,
                string blendShapeName,
                float defaultValue)
            {
                RelativePath = relativePath;
                BlendShapeName = blendShapeName;
                DefaultValue = defaultValue;
            }
        }

        private sealed class MouthDefaultCompensationResult
        {
            public string ControllerPath { get; }
            public int BlendShapeCount { get; }

            public MouthDefaultCompensationResult(
                string controllerPath,
                int blendShapeCount)
            {
                ControllerPath = controllerPath;
                BlendShapeCount = blendShapeCount;
            }
        }
    }
}
