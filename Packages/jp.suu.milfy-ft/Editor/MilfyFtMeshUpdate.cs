using System;
using System.Collections.Generic;
using System.Linq;
using Triturbo.BlendShapeShare.BlendShapeData;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using Object = UnityEngine.Object;

namespace Suu.MilfyFT.Editor
{
    internal static class MilfyFtMeshUpdate
    {
        private const string DataPath = "Packages/jp.suu.milfy-ft/Runtime/BlendShare/Milfy_FT_BlendShapes.asset";
        private static readonly string[] WideNames = { "EyeWideLeft", "EyeWideRight" };

        [MenuItem("Tools/suu_MifyFT/Update selected FT mesh to 1.2.0")]
        private static void UpdateSelected()
        {
            try
            {
                var path = UpdateAvatar(Selection.activeGameObject);
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Mesh>(path));
                EditorUtility.DisplayDialog("Milfy FT Mesh Update", "複製したMeshへ見開き形状を更新しました。元Meshは保持しています。\n" + path +
                    "\n\nSceneは自動保存しません。Undoで割当を戻せます（生成Meshは残ります）。", "OK");
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("Milfy FT Mesh Update", exception.Message, "OK");
            }
        }

        internal static string UpdateAvatar(GameObject avatar, string outputPath = null)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Play Modeを停止してから更新してください。");
            if (avatar == null || EditorUtility.IsPersistent(avatar) || !avatar.scene.IsValid() ||
                EditorSceneManager.IsPreviewScene(avatar.scene) ||
                avatar.GetComponent<VRCAvatarDescriptor>() == null)
                throw new InvalidOperationException("Hierarchy上のセットアップ済みMilfyアバタールートを選択してください。");
            var bodies = avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.name == "Body").ToArray();
            if (bodies.Length != 1 || bodies[0].sharedMesh == null)
                throw new InvalidOperationException("BodyのSkinnedMeshRendererを1つだけ持つアバターが必要です。");
            var body = bodies[0];
            var source = body.sharedMesh;
            if (source.GetBlendShapeIndex("EyeClosedLeft") < 0 || source.GetBlendShapeIndex("EyeClosedRight") < 0)
                throw new InvalidOperationException("FTセットアップ済みのMeshが必要です。未導入の場合はsetupを使用してください。");
            var data = AssetDatabase.LoadAssetAtPath<BlendShapeDataSO>(DataPath);
            if (data == null) throw new InvalidOperationException("Milfy FTのBlendShareデータが見つかりません。");
            var mesh = CreateUpdatedMesh(source, data);
            if (outputPath == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/suu_MilfyFT")) AssetDatabase.CreateFolder("Assets", "suu_MilfyFT");
                if (!AssetDatabase.IsValidFolder("Assets/suu_MilfyFT/Generated")) AssetDatabase.CreateFolder("Assets/suu_MilfyFT", "Generated");
                outputPath = AssetDatabase.GenerateUniqueAssetPath("Assets/suu_MilfyFT/Generated/Milfy_FT_1_2_0_Mesh.asset");
            }
            if (!outputPath.StartsWith("Assets/", StringComparison.Ordinal) ||
                !outputPath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) ||
                AssetDatabase.LoadMainAssetAtPath(outputPath) != null || System.IO.File.Exists(outputPath))
            {
                Object.DestroyImmediate(mesh);
                throw new InvalidOperationException("出力先にはAssets内の新しい.assetパスを指定してください。");
            }
            var weights = Enumerable.Range(0, source.blendShapeCount)
                .ToDictionary(source.GetBlendShapeName, body.GetBlendShapeWeight);
            try { AssetDatabase.CreateAsset(mesh, outputPath); }
            catch { Object.DestroyImmediate(mesh); throw; }
            Undo.RecordObject(body, "Update Milfy FT mesh to 1.2.0");
            body.sharedMesh = mesh;
            for (var i = 0; i < mesh.blendShapeCount; i++)
                body.SetBlendShapeWeight(i, weights.TryGetValue(mesh.GetBlendShapeName(i), out var value) ? value : 0);
            PrefabUtility.RecordPrefabInstancePropertyModifications(body);
            EditorUtility.SetDirty(body);
            EditorSceneManager.MarkSceneDirty(avatar.scene);
            return outputPath;
        }

        internal static Mesh CreateUpdatedMesh(Mesh source, BlendShapeDataSO data)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (data == null || data.m_MeshDataList == null || data.m_MeshDataList.Count != 1 ||
                !data.m_MeshDataList[0].IsValidTarget(source))
                throw new InvalidOperationException("Milfyの頂点構成が配布BlendShareデータと一致しません。");
            var shapes = data.m_MeshDataList[0];
            var replacements = new Dictionary<string, UnityBlendShapeData>();
            foreach (var name in WideNames)
            {
                var shape = shapes.GetBlendShape(name)?.m_UnityBlendShapeData;
                if (shape?.m_Frames == null || shape.m_Frames.Length == 0 ||
                    !shape.m_Frames.Any(f => f?.m_VertexIndices != null && f.m_DeltaVertices != null &&
                                            f.m_VertexIndices.Count > 0 && f.m_VertexIndices.Count == f.m_DeltaVertices.Count))
                    throw new InvalidOperationException(name + " の配布データが不正です。");
                replacements.Add(name, shape);
            }
            var result = Object.Instantiate(source);
            result.name = source.name + " (Milfy FT 1.2.0)";
            try
            {
                result.ClearBlendShapes();
                var vertices = new Vector3[source.vertexCount];
                var normals = new Vector3[source.vertexCount];
                var tangents = new Vector3[source.vertexCount];
                for (var i = 0; i < source.blendShapeCount; i++)
                {
                    var name = source.GetBlendShapeName(i);
                    if (replacements.TryGetValue(name, out var replacement))
                    {
                        foreach (var frame in replacement.m_Frames) frame.AddBlendShapeFrame(ref result, name);
                    }
                    else
                    {
                        for (var frame = 0; frame < source.GetBlendShapeFrameCount(i); frame++)
                        {
                            source.GetBlendShapeFrameVertices(i, frame, vertices, normals, tangents);
                            result.AddBlendShapeFrame(name, source.GetBlendShapeFrameWeight(i, frame), vertices, normals, tangents);
                        }
                    }
                }
                foreach (var name in WideNames.Where(n => source.GetBlendShapeIndex(n) < 0))
                    foreach (var frame in replacements[name].m_Frames) frame.AddBlendShapeFrame(ref result, name);
                return result;
            }
            catch
            {
                Object.DestroyImmediate(result);
                throw;
            }
        }
    }
}
