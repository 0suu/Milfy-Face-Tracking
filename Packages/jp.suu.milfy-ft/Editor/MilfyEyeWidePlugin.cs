using System;
using System.Linq;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using nadena.dev.ndmf.fluent;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

[assembly: ExportsPlugin(typeof(Suu.MilfyFT.Editor.MilfyEyeWidePlugin))]

namespace Suu.MilfyFT.Editor
{
    internal sealed class MilfyEyeWidePlugin : Plugin<MilfyEyeWidePlugin>
    {
        public override string QualifiedName => "jp.suu.milfy-ft.eye-wide";
        public override string DisplayName => "Milfy FT Eye Widening";

        protected override void Configure()
        {
            InPhase(BuildPhase.Transforming)
                .WithRequiredExtension(typeof(AnimatorServicesContext), sequence => sequence
                    .AfterPlugin("nadena.dev.modular-avatar")
                    .BeforePlugin("suu.milfy.tracking-modes")
                    .Run("Add missing eye widening shapes", AddEyeWideShapes));
        }

        private static void AddEyeWideShapes(BuildContext context)
        {
            // This parameter is owned by Milfy_FT.prefab after MA remapping.
            var services = context.Extension<AnimatorServicesContext>().ControllerContext;
            var controllers = services.GetAllControllers()
                .Where(c => c.Parameters.ContainsKey("Milfy/GazeSync")).ToArray();
            if (controllers.Length == 0) return;

            var bodies = context.AvatarRootObject.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(r => r.name == "Body").ToArray();
            if (bodies.Length != 1 || bodies[0].sharedMesh == null)
                throw new InvalidOperationException("Milfy FT: eye widening requires exactly one Body mesh.");

            var body = bodies[0];
            var widened = CreateEyeWideMesh(body.sharedMesh);
            Mesh mesh = null;
            try
            {
                mesh = CreatePupilMesh(widened);
            }
            finally
            {
                if (widened != body.sharedMesh && widened != mesh) Object.DestroyImmediate(widened);
            }
            var contractedPupils = mesh;
            mesh = null;
            try
            {
                mesh = CreateIrisMesh(contractedPupils);
            }
            finally
            {
                if (contractedPupils != body.sharedMesh && contractedPupils != mesh)
                    Object.DestroyImmediate(contractedPupils);
            }
            if (mesh != body.sharedMesh)
            {
                context.AssetSaver.SaveAsset(mesh);
                body.sharedMesh = mesh;
            }

            // Explicitly drive widening and pupil contraction: a zero-weight Direct BlendTree
            // can retain its last values with WD Off instead of releasing them on FT-off.
            var path = AnimationUtility.CalculateTransformPath(body.transform, context.AvatarRootObject.transform);
            var supportsTrackedPupils = mesh.GetBlendShapeIndex("EyeConstrict") >= 0 &&
                                        mesh.GetBlendShapeIndex("EyeDilation") >= 0;
            var widening = CreateWideningController(path, supportsTrackedPupils);
            foreach (var controller in controllers)
            foreach (var layer in services.Clone(widening).Layers)
                controller.AddLayer(new LayerPriority(1001), layer);
        }

        internal static AnimatorController CreateWideningController(string path, bool supportsTrackedPupils)
        {
            var controller = new AnimatorController();
            controller.AddParameter("EyeTrackingActive", AnimatorControllerParameterType.Float);
            controller.AddParameter("EyeDilationEnable", AnimatorControllerParameterType.Float);
            foreach (var side in new[] { "Left", "Right" })
            {
                var input = "OSCm/Proxy/FT/v2/EyeLid" + side;
                controller.AddParameter(input, AnimatorControllerParameterType.Float);
                var neutral = new AnimationClip { name = "Milfy Eye Wide " + side + " Off" };
                neutral.SetCurve(path, typeof(SkinnedMeshRenderer), "blendShape.EyeWide" + side,
                    AnimationCurve.Constant(0, 1, 0));
                neutral.SetCurve(path, typeof(SkinnedMeshRenderer), "blendShape.MilfyEyeWidePupil" + side,
                    AnimationCurve.Constant(0, 1, 0));
                neutral.SetCurve(path, typeof(SkinnedMeshRenderer), "blendShape.MilfyEyeWideIris" + side,
                    AnimationCurve.Constant(0, 1, 0));
                var wide = Object.Instantiate(neutral);
                wide.name = "Milfy Eye Wide " + side + " On";
                wide.SetCurve(path, typeof(SkinnedMeshRenderer), "blendShape.EyeWide" + side,
                    AnimationCurve.Constant(0, 1, 100));
                wide.SetCurve(path, typeof(SkinnedMeshRenderer), "blendShape.MilfyEyeWidePupil" + side,
                    AnimationCurve.Constant(0, 1, 100));
                wide.SetCurve(path, typeof(SkinnedMeshRenderer), "blendShape.MilfyEyeWideIris" + side,
                    AnimationCurve.Constant(0, 1, 100));
                var lid = new BlendTree
                {
                    name = "Milfy Eye Wide " + side, blendType = BlendTreeType.Simple1D,
                    blendParameter = input, useAutomaticThresholds = false,
                    children = new[]
                    {
                        new ChildMotion { motion = neutral, threshold = .75f, timeScale = 1 },
                        new ChildMotion { motion = wide, threshold = 1, timeScale = 1 }
                    }
                };
                Motion pupilMotion = lid;
                if (supportsTrackedPupils)
                {
                    // Eye Dilation Tracking owns actual pupil size when enabled. Only suppress
                    // the stylized contraction; keep the eyelid widening unchanged.
                    var trackedWide = Object.Instantiate(wide);
                    trackedWide.name = "Milfy Eye Wide " + side + " Tracked Pupil";
                    trackedWide.SetCurve(path, typeof(SkinnedMeshRenderer), "blendShape.MilfyEyeWidePupil" + side,
                        AnimationCurve.Constant(0, 1, 0));
                    var trackedLid = Object.Instantiate(lid);
                    trackedLid.name = "Milfy Eye Wide " + side + " Tracked Lid";
                    var trackedChildren = trackedLid.children;
                    trackedChildren[1].motion = trackedWide;
                    trackedLid.children = trackedChildren;
                    pupilMotion = new BlendTree
                    {
                        name = "Milfy Eye Wide " + side + " Pupil Ownership", blendType = BlendTreeType.Simple1D,
                        blendParameter = "EyeDilationEnable", useAutomaticThresholds = false,
                        children = new[]
                        {
                            new ChildMotion { motion = lid, threshold = 0, timeScale = 1 },
                            new ChildMotion { motion = trackedLid, threshold = 1, timeScale = 1 }
                        }
                    };
                }
                var gate = new BlendTree
                {
                    name = "Milfy Eye Wide " + side + " Gate", blendType = BlendTreeType.Simple1D,
                    blendParameter = "EyeTrackingActive", useAutomaticThresholds = false,
                    children = new[]
                    {
                        new ChildMotion { motion = neutral, threshold = 0, timeScale = 1 },
                        new ChildMotion { motion = pupilMotion, threshold = 1, timeScale = 1 }
                    }
                };
                var machine = new AnimatorStateMachine { name = "Milfy FT Eye Wide " + side };
                var state = machine.AddState("Widening");
                state.motion = gate;
                state.writeDefaultValues = false;
                machine.defaultState = state;
                controller.AddLayer(new AnimatorControllerLayer
                {
                    name = machine.name, stateMachine = machine, defaultWeight = 1
                });
            }
            return controller;
        }

        internal static Mesh CreatePupilMesh(Mesh source)
        {
            // pupul_small is the spelling in the original Milfy data.
            return CreateSplitEyeMesh(source, "pupul_small", "MilfyEyeWidePupil", .5f);
        }

        internal static Mesh CreateIrisMesh(Mesh source)
        {
            // This variant scales the iris and its highlights together. The final character is full-width 2.
            return CreateSplitEyeMesh(source, "eye_op_small_２", "MilfyEyeWideIris", .4f);
        }

        private static Mesh CreateSplitEyeMesh(Mesh source, string nativeName, string targetPrefix, float strength)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var missingSides = new[] { "Left", "Right" }
                .Where(side => source.GetBlendShapeIndex(targetPrefix + side) < 0).ToArray();
            if (missingSides.Length == 0) return source;
            var index = source.GetBlendShapeIndex(nativeName);
            if (index < 0)
                throw new InvalidOperationException("Milfy FT: eye contraction requires the native " + nativeName + " shape.");

            var positions = source.vertices;
            var vertices = new Vector3[source.vertexCount];
            var normals = new Vector3[source.vertexCount];
            var tangents = new Vector3[source.vertexCount];
            foreach (var side in missingSides)
            {
                var hasDisplacement = false;
                for (var frame = 0; frame < source.GetBlendShapeFrameCount(index); frame++)
                {
                    source.GetBlendShapeFrameVertices(index, frame, vertices, normals, tangents);
                    for (var v = 0; v < positions.Length; v++)
                        if ((side == "Left" ? positions[v].x > 0 : positions[v].x < 0) &&
                            vertices[v] != Vector3.zero) hasDisplacement = true;
                }
                if (!hasDisplacement)
                    throw new InvalidOperationException("Milfy FT: " + nativeName + " has no " + side + " eye displacement.");
            }

            var result = Object.Instantiate(source);
            result.name = source.name + " (" + targetPrefix + ")";
            try
            {
                foreach (var side in missingSides)
                for (var frame = 0; frame < source.GetBlendShapeFrameCount(index); frame++)
                {
                    source.GetBlendShapeFrameVertices(index, frame, vertices, normals, tangents);
                    for (var v = 0; v < positions.Length; v++)
                    {
                        // Unmodified Milfy uses local +X for anatomical left, -X for right.
                        var scale = (side == "Left" ? positions[v].x > 0 : positions[v].x < 0) ? strength : 0;
                        vertices[v] *= scale;
                        normals[v] *= scale;
                        tangents[v] *= scale;
                    }
                    result.AddBlendShapeFrame(targetPrefix + side,
                        source.GetBlendShapeFrameWeight(index, frame), vertices, normals, tangents);
                }
                return result;
            }
            catch
            {
                Object.DestroyImmediate(result);
                throw;
            }
        }

        // Return the source unchanged when both shapes already exist. Never edit shared assets.
        internal static Mesh CreateEyeWideMesh(Mesh source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var missingSides = new[] { "Left", "Right" }
                .Where(side => source.GetBlendShapeIndex("EyeWide" + side) < 0).ToArray();
            if (missingSides.Length == 0) return source;

            var vertices = new Vector3[source.vertexCount];
            var normals = new Vector3[source.vertexCount];
            var tangents = new Vector3[source.vertexCount];
            foreach (var side in missingSides)
            {
                var nativeName = "eye_surprised_" + (side == "Left" ? "L" : "R");
                if (source.GetBlendShapeIndex("EyeClosed" + side) < 0 ||
                    source.GetBlendShapeIndex(nativeName) < 0)
                    throw new InvalidOperationException(
                        "Milfy FT: eye widening requires EyeClosed" + side + " and " + nativeName +
                        ". Use a supported Milfy mesh with the FT setup applied.");

                var index = source.GetBlendShapeIndex(nativeName);
                var hasDisplacement = false;
                for (var frame = 0; frame < source.GetBlendShapeFrameCount(index); frame++)
                {
                    source.GetBlendShapeFrameVertices(index, frame, vertices, normals, tangents);
                    hasDisplacement |= vertices.Any(delta => delta != Vector3.zero);
                }
                if (!hasDisplacement)
                    throw new InvalidOperationException("Milfy FT: " + nativeName + " has no eye widening displacement.");
            }

            var result = Object.Instantiate(source);
            result.name = source.name + " (Milfy FT Eye Wide)";
            try
            {
                foreach (var side in missingSides)
                {
                    var index = source.GetBlendShapeIndex("eye_surprised_" + (side == "Left" ? "L" : "R"));
                    for (var frame = 0; frame < source.GetBlendShapeFrameCount(index); frame++)
                    {
                        source.GetBlendShapeFrameVertices(index, frame, vertices, normals, tangents);
                        result.AddBlendShapeFrame("EyeWide" + side,
                            source.GetBlendShapeFrameWeight(index, frame), vertices, normals, tangents);
                    }
                }
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
