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
        private Mesh CreateFtMesh()
        {
            var mesh = Object.Instantiate(_data.m_MeshDataList[0].m_OriginMesh);
            _objects.Add(mesh);
            foreach (var shape in _data.m_MeshDataList[0].BlendShapes)
            {
                if (shape.m_ShapeName == "EyeWideLeft" || shape.m_ShapeName == "EyeWideRight" ||
                    mesh.GetBlendShapeIndex(shape.m_ShapeName) >= 0) continue;
                foreach (var frame in shape.m_UnityBlendShapeData.m_Frames)
                    frame.AddBlendShapeFrame(ref mesh, shape.m_ShapeName);
            }
            return mesh;
        }
        private SkinnedMeshRenderer Body(Mesh mesh)
        {
            var root = new GameObject("Milfy update test"); _objects.Add(root); root.AddComponent<Animator>(); root.AddComponent(Type.GetType("VRC.SDK3.Avatars.Components.VRCAvatarDescriptor, VRCSDK3A", true));
            var body = new GameObject("Body"); body.transform.SetParent(root.transform); var renderer = body.AddComponent<SkinnedMeshRenderer>(); renderer.sharedMesh = mesh; return renderer;
        }
        private string OutputPath() { var path = "Assets/MilfyFT_UpdateTest_" + Guid.NewGuid().ToString("N") + ".asset"; _paths.Add(path); return path; }
        [TestCase(false)][TestCase(true)]
        public void MigrationPreservesOriginalShapesAndReplacesExistingWide(bool existingWide)
        {
            var source = CreateFtMesh();
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
        [Test] public void MigrationPreservesWeightsAndUndoRestoresOriginalMesh()
        {
            var source = CreateFtMesh(); var body = Body(source); body.SetBlendShapeWeight(0, 37);
            var path = OutputPath(); Undo.IncrementCurrentGroup(); Call("UpdateAvatar", body.transform.parent.gameObject, path); Undo.FlushUndoRecordObjects();
            Assert.That(body.sharedMesh, Is.Not.SameAs(source)); Assert.That(body.GetBlendShapeWeight(0), Is.EqualTo(37));
            Undo.PerformUndo(); Assert.That(body.sharedMesh, Is.SameAs(source)); Assert.That(body.GetBlendShapeWeight(0), Is.EqualTo(37));
            Assert.That(AssetDatabase.LoadAssetAtPath<Mesh>(path), Is.Not.Null); Undo.ClearUndo(body);
        }
        [Test] public void ExistingSixShapesDoNotAccumulateAndAllEightRelease()
        {
            var mesh = Update(CreateFtMesh()); var body = Body(mesh);
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
            var mesh = Update(CreateFtMesh()); var actual = Body(mesh); var expected = Body(mesh);
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
