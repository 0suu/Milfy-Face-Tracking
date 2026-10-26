using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

namespace Suu.MilfyFT.Tests
{
    public sealed class MilfyEyeWideTests
    {
        private Mesh _source;
        private Mesh _result;

        [SetUp]
        public void SetUp()
        {
            _source = new Mesh { name = "Original Milfy" };
            _source.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
            _source.triangles = new[] { 0, 1, 2 };
            AddShape("EyeClosedLeft", 100, Vector3.down);
            AddShape("EyeClosedRight", 100, Vector3.down);
        }

        [TearDown]
        public void TearDown()
        {
            if (_result != null && _result != _source) Object.DestroyImmediate(_result);
            Object.DestroyImmediate(_source);
        }

        private void AddShape(string name, float weight, Vector3 delta)
        {
            _source.AddBlendShapeFrame(name, weight,
                new[] { delta, Vector3.zero, Vector3.zero },
                new[] { Vector3.forward, Vector3.zero, Vector3.zero },
                new[] { Vector3.right, Vector3.zero, Vector3.zero });
        }

        private Mesh Build()
        {
            var type = typeof(Editor.MilfyFtSetupService).Assembly
                .GetType("Suu.MilfyFT.Editor.MilfyEyeWidePlugin", true);
            try
            {
                _result = (Mesh)type.GetMethod("CreateEyeWideMesh", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { _source });
                return _result;
            }
            catch (TargetInvocationException exception)
            {
                throw exception.InnerException;
            }
        }

        [Test]
        public void AddsBothSidesWithoutChangingSourceOrExistingIndices()
        {
            AddShape("eye_surprised_L", 100, Vector3.up);
            AddShape("eye_surprised_R", 100, Vector3.left);
            var result = Build();
            Assert.That(result, Is.Not.SameAs(_source));
            Assert.That(_source.blendShapeCount, Is.EqualTo(4));
            Assert.That(result.blendShapeCount, Is.EqualTo(6));
            Assert.That(result.vertices, Is.EqualTo(_source.vertices));
            Assert.That(result.triangles, Is.EqualTo(_source.triangles));
            for (var i = 0; i < _source.blendShapeCount; i++)
                AssertShapeEqual(_source, i, result, i);
            AssertShapeEqual(_source, 2, result, 4);
            AssertShapeEqual(_source, 3, result, 5);
        }

        [Test]
        public void PreservesAllFramesWeightsNormalsAndTangents()
        {
            AddShape("eye_surprised_L", 50, Vector3.up * .5f);
            AddShape("eye_surprised_L", 100, Vector3.up);
            AddShape("eye_surprised_R", 100, Vector3.left);
            var result = Build();
            AssertShapeEqual(_source, 2, result, result.GetBlendShapeIndex("EyeWideLeft"));
        }

        [TestCase("Left", "R")]
        [TestCase("Right", "L")]
        public void PreservesCustomWideAndOnlyRequiresMissingSidesSource(string existingSide, string missingNativeSide)
        {
            AddShape("EyeWide" + existingSide, 100, Vector3.back);
            AddShape("eye_surprised_" + missingNativeSide, 100, Vector3.up);
            var result = Build();
            Assert.That(result.blendShapeCount, Is.EqualTo(5));
            AssertShapeEqual(_source, 2, result, 2);
            AssertShapeEqual(_source, 3, result, 4);
            Assert.That(result.GetBlendShapeIndex("EyeWideLeft"), Is.GreaterThanOrEqualTo(0));
            Assert.That(result.GetBlendShapeIndex("EyeWideRight"), Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void AlreadySupportedMeshIsUnchangedAndIdempotent()
        {
            AddShape("EyeWideLeft", 100, Vector3.up);
            AddShape("EyeWideRight", 100, Vector3.left);
            Assert.That(Build(), Is.SameAs(_source));
            Assert.That(Build(), Is.SameAs(_source));
        }

        [Test]
        public void MissingNativeSideFailsBeforeMutatingSource()
        {
            AddShape("eye_surprised_L", 100, Vector3.up);
            Assert.Throws<InvalidOperationException>(() => Build());
            Assert.That(_source.blendShapeCount, Is.EqualTo(3));
        }

        [Test]
        public void ZeroDisplacementFailsBeforeMutatingSource()
        {
            AddShape("eye_surprised_L", 100, Vector3.up);
            AddShape("eye_surprised_R", 100, Vector3.zero);
            Assert.Throws<InvalidOperationException>(() => Build());
            Assert.That(_source.blendShapeCount, Is.EqualTo(4));
        }

        [TestCase(false, 0f)]
        [TestCase(false, 1f)]
        [TestCase(true, 0f)]
        [TestCase(true, 1f)]
        public void DisablingTrackingClearsPreviouslyWidenedEyesWithWriteDefaultsOff(bool supportsTrackedPupils, float pupilTracking)
        {
            AddShape("EyeWideLeft", 100, Vector3.up);
            AddShape("EyeWideRight", 100, Vector3.left);
            AddShape("MilfyEyeWidePupilLeft", 100, Vector3.right);
            AddShape("MilfyEyeWidePupilRight", 100, Vector3.left);
            var root = new GameObject("Eye wide test");
            var graph = PlayableGraph.Create("Eye wide reset test");
            AnimatorController controller = null;
            var stale = new AnimationClip();
            var machine = new AnimatorStateMachine();
            try
            {
                var body = new GameObject("Body");
                body.transform.SetParent(root.transform);
                var renderer = body.AddComponent<SkinnedMeshRenderer>();
                renderer.sharedMesh = _source;
                var animator = root.AddComponent<Animator>();
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var type = typeof(Editor.MilfyFtSetupService).Assembly
                    .GetType("Suu.MilfyFT.Editor.MilfyEyeWidePlugin", true);
                controller = (AnimatorController)type.GetMethod("CreateWideningController", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { "Body", supportsTrackedPupils });
                foreach (var side in new[] { "Left", "Right" })
                    stale.SetCurve("Body", typeof(SkinnedMeshRenderer), "blendShape.EyeWide" + side,
                        AnimationCurve.Constant(0, 1, 100));
                var state = machine.AddState("Previously widened");
                state.motion = stale;
                state.writeDefaultValues = false;
                machine.defaultState = state;
                controller.layers = new[]
                {
                    new AnimatorControllerLayer { name = "Previous FT output", stateMachine = machine, defaultWeight = 1 }
                }.Concat(controller.layers).ToArray();
                var playable = AnimatorControllerPlayable.Create(graph, controller);
                AnimationPlayableOutput.Create(graph, "Eyes", animator).SetSourcePlayable(playable);
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                graph.Play();
                foreach (var active in new[] { 1f, 0f, 1f, 0f })
                foreach (var pupilTrackingValue in new[] { pupilTracking, 1 - pupilTracking, pupilTracking })
                {
                    playable.SetFloat("EyeTrackingActive", active);
                    playable.SetFloat("EyeDilationEnable", pupilTrackingValue);
                    playable.SetFloat("OSCm/Proxy/FT/v2/EyeLidLeft", 1);
                    playable.SetFloat("OSCm/Proxy/FT/v2/EyeLidRight", 1);
                    graph.Evaluate(1f / 60f);
                    graph.Evaluate(1f / 60f);
                    foreach (var side in new[] { "Left", "Right" })
                    {
                        Assert.That(renderer.GetBlendShapeWeight(_source.GetBlendShapeIndex("EyeWide" + side)),
                            Is.EqualTo(active * 100).Within(.01f));
                        Assert.That(renderer.GetBlendShapeWeight(_source.GetBlendShapeIndex("MilfyEyeWidePupil" + side)),
                            Is.EqualTo(active * 100 * (supportsTrackedPupils ? 1 - pupilTrackingValue : 1)).Within(.01f));
                    }
                }
            }
            finally
            {
                graph.Destroy();
                Object.DestroyImmediate(root);
                if (controller != null)
                {
                    var motions = new System.Collections.Generic.HashSet<Motion>();
                    var pending = new System.Collections.Generic.Stack<Motion>(controller.layers.Select(l => l.stateMachine.defaultState.motion));
                    while (pending.Count > 0)
                    {
                        var motion = pending.Pop();
                        if (!motions.Add(motion)) continue;
                        if (motion is BlendTree tree) foreach (var child in tree.children) pending.Push(child.motion);
                    }
                    foreach (var motion in motions.Where(m => m != stale)) Object.DestroyImmediate(motion);
                    foreach (var layer in controller.layers.Where(l => l.stateMachine != machine))
                    {
                        Object.DestroyImmediate(layer.stateMachine);
                    }
                    Object.DestroyImmediate(controller);
                }
                Object.DestroyImmediate(machine);
                Object.DestroyImmediate(stale);
            }
        }

        private void AddNativePupil()
        {
            _source.vertices = new[] { Vector3.left, Vector3.right, Vector3.up };
            foreach (var weight in new[] { 50f, 100f })
                _source.AddBlendShapeFrame("pupul_small", weight,
                    new[] { Vector3.right * weight / 100, Vector3.left * weight / 100, Vector3.zero },
                    new[] { Vector3.forward, Vector3.back, Vector3.zero },
                    new[] { Vector3.up, Vector3.down, Vector3.zero });
        }

        private Mesh BuildPupils()
        {
            var type = typeof(Editor.MilfyFtSetupService).Assembly
                .GetType("Suu.MilfyFT.Editor.MilfyEyeWidePlugin", true);
            try
            {
                return _result = (Mesh)type.GetMethod("CreatePupilMesh", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { _source });
            }
            catch (TargetInvocationException exception) { throw exception.InnerException; }
        }

        [Test]
        public void SplitsPupilsIndependentlyAtHalfStrengthAndPreservesFrames()
        {
            AddNativePupil();
            var result = BuildPupils();
            Assert.That(_source.blendShapeCount, Is.EqualTo(3));
            Assert.That(result.blendShapeCount, Is.EqualTo(5));
            AssertShapeEqual(_source, 2, result, 2);
            foreach (var side in new[] { "Left", "Right" })
            {
                var index = result.GetBlendShapeIndex("MilfyEyeWidePupil" + side);
                Assert.That(result.GetBlendShapeFrameCount(index), Is.EqualTo(2));
                for (var frame = 0; frame < 2; frame++)
                {
                    var original = Enumerable.Range(0, 3).Select(_ => new Vector3[3]).ToArray();
                    var actual = Enumerable.Range(0, 3).Select(_ => new Vector3[3]).ToArray();
                    _source.GetBlendShapeFrameVertices(2, frame, original[0], original[1], original[2]);
                    result.GetBlendShapeFrameVertices(index, frame, actual[0], actual[1], actual[2]);
                    Assert.That(result.GetBlendShapeFrameWeight(index, frame), Is.EqualTo(_source.GetBlendShapeFrameWeight(2, frame)));
                    for (var channel = 0; channel < 3; channel++)
                    for (var vertex = 0; vertex < 3; vertex++)
                        Assert.That(actual[channel][vertex], Is.EqualTo(vertex == (side == "Left" ? 1 : 0)
                            ? original[channel][vertex] * .5f : Vector3.zero));
                }
            }
        }

        [Test]
        public void PupilGenerationIsIdempotent()
        {
            AddNativePupil();
            var result = BuildPupils();
            var type = typeof(Editor.MilfyFtSetupService).Assembly
                .GetType("Suu.MilfyFT.Editor.MilfyEyeWidePlugin", true);
            Assert.That(type.GetMethod("CreatePupilMesh", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { result }), Is.SameAs(result));
        }

        [Test]
        public void MissingPupilSourceFailsWithoutChangingSource()
        {
            Assert.Throws<InvalidOperationException>(() => BuildPupils());
            Assert.That(_source.blendShapeCount, Is.EqualTo(2));
        }

        [Test]
        public void MissingOnePupilSideFailsWithoutChangingSource()
        {
            _source.vertices = new[] { Vector3.left, Vector3.right, Vector3.up };
            AddShape("pupul_small", 100, Vector3.up);
            Assert.Throws<InvalidOperationException>(() => BuildPupils());
            Assert.That(_source.blendShapeCount, Is.EqualTo(3));
        }

        private static void AssertShapeEqual(Mesh source, int sourceIndex, Mesh target, int targetIndex)
        {
            Assert.That(target.GetBlendShapeFrameCount(targetIndex), Is.EqualTo(source.GetBlendShapeFrameCount(sourceIndex)));
            for (var frame = 0; frame < source.GetBlendShapeFrameCount(sourceIndex); frame++)
            {
                Assert.That(target.GetBlendShapeFrameWeight(targetIndex, frame), Is.EqualTo(source.GetBlendShapeFrameWeight(sourceIndex, frame)));
                var original = Enumerable.Range(0, 3).Select(_ => new Vector3[source.vertexCount]).ToArray();
                var copy = Enumerable.Range(0, 3).Select(_ => new Vector3[source.vertexCount]).ToArray();
                source.GetBlendShapeFrameVertices(sourceIndex, frame, original[0], original[1], original[2]);
                target.GetBlendShapeFrameVertices(targetIndex, frame, copy[0], copy[1], copy[2]);
                for (var channel = 0; channel < 3; channel++) Assert.That(copy[channel], Is.EqualTo(original[channel]));
            }
        }
    }
}
