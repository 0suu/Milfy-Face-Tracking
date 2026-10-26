using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Triturbo.BlendShapeShare.BlendShapeData;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

namespace Suu.MilfyFT.Tests
{
    public sealed class MilfyEyeWideTests
    {
        private BlendShapeDataSO _data;
        private readonly List<Object> _objects = new List<Object>();
        private readonly List<string> _paths = new List<string>();
        [SetUp] public void SetUp() => _data = AssetDatabase.LoadAssetAtPath<BlendShapeDataSO>("Packages/jp.suu.milfy-ft/Runtime/BlendShare/Milfy_FT_BlendShapes.asset");
        [TearDown] public void TearDown()
        {
            foreach (var value in _objects) if (value != null && !EditorUtility.IsPersistent(value)) Object.DestroyImmediate(value);
            foreach (var path in _paths) AssetDatabase.DeleteAsset(path);
            _objects.Clear(); _paths.Clear();
        }
        private object Call(string method, params object[] args)
        {
            var type = typeof(Editor.MilfyFtSetupService).Assembly.GetType("Suu.MilfyFT.Editor.MilfyFtMeshUpdate", true);
            try { return type.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args); }
            catch (TargetInvocationException exception) { throw exception.InnerException; }
        }
        private Mesh Update(Mesh source) { var result = (Mesh)Call("CreateUpdatedMesh", source, _data); _objects.Add(result); return result; }
        private SkinnedMeshRenderer Body(Mesh mesh)
        {
            var root = new GameObject("Milfy update test"); _objects.Add(root); root.AddComponent<Animator>(); root.AddComponent(Type.GetType("VRC.SDK3.Avatars.Components.VRCAvatarDescriptor, VRCSDK3A", true));
            var body = new GameObject("Body"); body.transform.SetParent(root.transform); var renderer = body.AddComponent<SkinnedMeshRenderer>(); renderer.sharedMesh = mesh; return renderer;
        }
        private string OutputPath() { var path = "Assets/MilfyFT_UpdateTest_" + Guid.NewGuid().ToString("N") + ".asset"; _paths.Add(path); return path; }
        [Test] public void PackageContains41ShapesAndBothDeltaFormats()
        {
            var names = _data.m_MeshDataList.Single().m_ShapeNames;
            Assert.That(names.Count, Is.EqualTo(41)); Assert.That(names.Distinct().Count(), Is.EqualTo(41));
            Assert.That(names.Any(n => n.StartsWith("MilfyEyeWide", StringComparison.Ordinal)), Is.False);
            foreach (var side in new[] { "Left", "Right" })
            {
                var shape = _data.m_MeshDataList[0].GetBlendShape("EyeWide" + side);
                Assert.That(shape.m_UnityBlendShapeData.m_Frames[0].m_VertexIndices.Count, Is.GreaterThan(0));
                Assert.That(shape.m_FbxBlendShapeData.m_Frames[0].m_PointsIndices.Count, Is.GreaterThan(0));
            }
        }
        [TestCase("Left")][TestCase("Right")]
        public void BakedWideMatchesNativeComposition(string side)
        {
            var mesh = _data.m_MeshDataList[0].m_OriginMesh; var positions = mesh.vertices;
            var frame = _data.m_MeshDataList[0].GetBlendShape("EyeWide" + side).m_UnityBlendShapeData.m_Frames.Single();
            var expected = new[] { new Vector3[mesh.vertexCount], new Vector3[mesh.vertexCount], new Vector3[mesh.vertexCount] };
            var temporary = new[] { new Vector3[mesh.vertexCount], new Vector3[mesh.vertexCount], new Vector3[mesh.vertexCount] };
            var sourceNames = new[] { "eye_surprised_" + (side == "Left" ? "L" : "R"), "pupul_small", "eye_op_small_２" };
            for (var shape = 0; shape < sourceNames.Length; shape++)
            {
                mesh.GetBlendShapeFrameVertices(mesh.GetBlendShapeIndex(sourceNames[shape]), 0, temporary[0], temporary[1], temporary[2]);
                for (var vertex = 0; vertex < mesh.vertexCount; vertex++)
                for (var channel = 0; channel < 3; channel++)
                    if (shape == 0 || (side == "Left" ? positions[vertex].x < 0 : positions[vertex].x > 0))
                        expected[channel][vertex] += temporary[channel][vertex] * (shape == 0 ? 1 : shape == 1 ? .5f : .4f);
            }
            var actual = new[] { frame.GetDeltaVertices(mesh.vertexCount), frame.GetDeltaNormals(mesh.vertexCount), frame.GetDeltaTangents(mesh.vertexCount) };
            for (var channel = 0; channel < 3; channel++)
                Assert.That(Enumerable.Range(0, mesh.vertexCount).Max(i => (actual[channel][i] - expected[channel][i]).magnitude), Is.LessThan(1e-7f));
        }
        [TestCase(false)][TestCase(true)]
        public void MigrationPreservesOriginalShapesAndReplacesPreviewWide(bool existingWide)
        {
            var source = Object.Instantiate(_data.m_MeshDataList[0].m_OriginMesh); _objects.Add(source);
            if (existingWide) foreach (var side in new[] { "Left", "Right" })
                source.AddBlendShapeFrame("EyeWide" + side, 100, Enumerable.Repeat(Vector3.up, source.vertexCount).ToArray(), null, null);
            var count = source.blendShapeCount; var result = Update(source);
            Assert.That(source.blendShapeCount, Is.EqualTo(count)); Assert.That(result.vertices, Is.EqualTo(source.vertices));
            Assert.That(result.bindposes, Is.EqualTo(source.bindposes)); Assert.That(result.triangles, Is.EqualTo(source.triangles));
            for (var i = 0; i < count; i++) Assert.That(result.GetBlendShapeName(i), Is.EqualTo(source.GetBlendShapeName(i)));
            foreach (var side in new[] { "Left", "Right" })
            {
                var actual = new Vector3[result.vertexCount]; result.GetBlendShapeFrameVertices(result.GetBlendShapeIndex("EyeWide" + side), 0, actual, null, null);
                Assert.That(actual, Is.EqualTo(_data.m_MeshDataList[0].GetBlendShape("EyeWide" + side).m_UnityBlendShapeData.m_Frames[0].GetDeltaVertices(result.vertexCount)));
            }
            foreach (var name in new[] { "EyeClosedLeft", "BrowInnerUpLeft", "pupul_small", "eye_op_small_２" })
            {
                var original = new Vector3[source.vertexCount]; var actual = new Vector3[source.vertexCount];
                source.GetBlendShapeFrameVertices(source.GetBlendShapeIndex(name), 0, original, null, null);
                result.GetBlendShapeFrameVertices(result.GetBlendShapeIndex(name), 0, actual, null, null);
                Assert.That(actual, Is.EqualTo(original));
            }
        }
        [Test] public void InvalidTopologyFailsBeforeSourceChanges()
        {
            var mesh = new Mesh { vertices = new[] { Vector3.zero, Vector3.right, Vector3.up } }; _objects.Add(mesh);
            Assert.Throws<InvalidOperationException>(() => Update(mesh)); Assert.That(mesh.blendShapeCount, Is.Zero);
        }
        [Test] public void InvalidWideDataFailsBeforeSourceChanges()
        {
            _data = Object.Instantiate(_data); _objects.Add(_data); _data.m_MeshDataList[0].GetBlendShape("EyeWideLeft").m_UnityBlendShapeData = null;
            var source = _data.m_MeshDataList[0].m_OriginMesh; var count = source.blendShapeCount;
            Assert.Throws<InvalidOperationException>(() => Update(source)); Assert.That(source.blendShapeCount, Is.EqualTo(count));
        }
        [Test] public void MigrationPreservesWeightsAndUndoRestoresOriginalMesh()
        {
            var source = _data.m_MeshDataList[0].m_OriginMesh; var body = Body(source); body.SetBlendShapeWeight(0, 37);
            var path = OutputPath(); Undo.IncrementCurrentGroup(); Call("UpdateAvatar", body.transform.parent.gameObject, path); Undo.FlushUndoRecordObjects();
            Assert.That(body.sharedMesh, Is.Not.SameAs(source)); Assert.That(body.GetBlendShapeWeight(0), Is.EqualTo(37));
            Undo.PerformUndo(); Assert.That(body.sharedMesh, Is.SameAs(source)); Assert.That(body.GetBlendShapeWeight(0), Is.EqualTo(37));
            Assert.That(AssetDatabase.LoadAssetAtPath<Mesh>(path), Is.Not.Null); Undo.ClearUndo(body);
        }
        [Test] public void ActivePreviewCorrectionIsRejectedWithoutAssignment()
        {
            var source = Object.Instantiate(_data.m_MeshDataList[0].m_OriginMesh); _objects.Add(source);
            source.AddBlendShapeFrame("MilfyEyeWideIrisLeft", 100, Enumerable.Repeat(Vector3.up, source.vertexCount).ToArray(), null, null);
            var body = Body(source); body.SetBlendShapeWeight(source.GetBlendShapeIndex("MilfyEyeWideIrisLeft"), 10);
            Assert.Throws<InvalidOperationException>(() => Call("UpdateAvatar", body.transform.parent.gameObject, OutputPath()));
            Assert.That(body.sharedMesh, Is.SameAs(source));
        }
        [Test] public void PreviewSceneUpdateIsRejected()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            try
            {
                var body = Body(_data.m_MeshDataList[0].m_OriginMesh);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(body.transform.parent.gameObject, scene);
                Assert.Throws<InvalidOperationException>(() => Call("UpdateAvatar", body.transform.parent.gameObject, OutputPath()));
            }
            finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
        }
        [Test] public void ExistingSixShapesDoNotAccumulateAndAllEightRelease()
        {
            var mesh = Update(_data.m_MeshDataList[0].m_OriginMesh); var body = Body(mesh);
            var adapter = AssetDatabase.LoadAssetAtPath<AnimatorController>("Packages/jp.suu.milfy-ft/Runtime/Animators/Milfy_FT_ShapeAdapter.controller");
            var inputs = new[] { "BrowExpressionLeft", "BrowExpressionRight", "SmileFrownLeft", "SmileFrownRight", "MouthLowerDown" };
            var targets = new[] { "BrowInnerUpLeft", "BrowInnerUpRight", "MouthSadLeft", "MouthSadRight", "MouthLowerDownLeft" };
            var names = new[] { "BrowInnerUpLeft", "BrowInnerUpRight", "MouthSadLeft", "MouthSadRight", "MouthLowerDownLeft", "MouthLowerDownRight", "EyeWideLeft", "EyeWideRight" };
            var graph = PlayableGraph.Create("Adapter ownership regression");
            try
            {
                var playable = AnimatorControllerPlayable.Create(graph, adapter);
                AnimationPlayableOutput.Create(graph, "Body", body.transform.parent.GetComponent<Animator>()).SetSourcePlayable(playable);
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual); graph.Play();
                for (var selected = 0; selected < inputs.Length; selected++)
                {
                    foreach (var input in inputs) playable.SetFloat("OSCm/Proxy/FT/v2/" + input, 0);
                    playable.SetFloat("OSCm/Proxy/FT/v2/" + inputs[selected], selected == 2 || selected == 3 ? -1 : 1);
                    playable.SetFloat("EyeTrackingActive", 1); playable.SetFloat("LipTrackingActive", 1);
                    for (var frame = 0; frame < 8; frame++) graph.Evaluate(1f / 60f);
                    Assert.That(body.GetBlendShapeWeight(mesh.GetBlendShapeIndex(targets[selected])), Is.EqualTo(100).Within(.01f));
                    foreach (var name in names) Assert.That(body.GetBlendShapeWeight(mesh.GetBlendShapeIndex(name)), Is.InRange(-.01f, 100.01f));
                    playable.SetFloat("EyeTrackingActive", 0); playable.SetFloat("LipTrackingActive", 0);
                    for (var frame = 0; frame < 4; frame++) graph.Evaluate(1f / 60f);
                    foreach (var name in names) Assert.That(body.GetBlendShapeWeight(mesh.GetBlendShapeIndex(name)), Is.EqualTo(0).Within(.01f));
                }
            }
            finally { graph.Destroy(); }
        }
        [TestCase("Left")][TestCase("Right")]
        public void ExistingAdapterMatchesJerry2DAndExplicitlyReleasesTracking(string side)
        {
            var mesh = Update(_data.m_MeshDataList[0].m_OriginMesh); var actual = Body(mesh); var expected = Body(mesh);
            var adapter = AssetDatabase.LoadAssetAtPath<AnimatorController>("Packages/jp.suu.milfy-ft/Runtime/Animators/Milfy_FT_ShapeAdapter.controller");
            Assert.That(adapter.layers.Length, Is.EqualTo(2));
            var vendor = "Packages/adjerry91.vrcft.templates/Animators/Unified Expressions Blendshapes/FX - Face Tracking - UE Blendshapes.controller";
            var tree = AssetDatabase.LoadAllAssetsAtPath(vendor).OfType<BlendTree>().Single(t => t.name == side + " Eye Lid Blend");
            var reference = new AnimatorController(); _objects.Add(reference);
            foreach (var input in new[] { "OSCm/Proxy/FT/v2/EyeLid" + side, "OSCm/Proxy/FT/v2/EyeSquint" + side }) reference.AddParameter(input, AnimatorControllerParameterType.Float);
            var machine = new AnimatorStateMachine(); _objects.Add(machine); var state = machine.AddState("Jerry reference"); state.motion = tree; state.writeDefaultValues = false; machine.defaultState = state;
            reference.AddLayer(new AnimatorControllerLayer { name = "Reference", stateMachine = machine, defaultWeight = 1 });
            var graph = PlayableGraph.Create("Milfy Jerry compatibility");
            try
            {
                var a = AnimatorControllerPlayable.Create(graph, adapter); var b = AnimatorControllerPlayable.Create(graph, reference);
                AnimationPlayableOutput.Create(graph, "Adapter", actual.transform.parent.GetComponent<Animator>()).SetSourcePlayable(a);
                AnimationPlayableOutput.Create(graph, "Jerry", expected.transform.parent.GetComponent<Animator>()).SetSourcePlayable(b);
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual); graph.Play();
                foreach (var lid in new[] { 1f, .875f, .75f, 0f }) foreach (var squint in new[] { 0f, .5f, 1f }) foreach (var active in new[] { 1f, 0f, 1f })
                {
                    a.SetFloat("EyeTrackingActive", active);
                    foreach (var playable in new[] { a, b }) { playable.SetFloat("OSCm/Proxy/FT/v2/EyeLid" + side, lid); playable.SetFloat("OSCm/Proxy/FT/v2/EyeSquint" + side, squint); }
                    graph.Evaluate(1f / 60f); graph.Evaluate(1f / 60f);
                    Assert.That(actual.GetBlendShapeWeight(mesh.GetBlendShapeIndex("EyeWide" + side)), Is.EqualTo(expected.GetBlendShapeWeight(mesh.GetBlendShapeIndex("EyeWide" + side)) * active).Within(.01f));
                }
            }
            finally { graph.Destroy(); }
        }
    }
}
