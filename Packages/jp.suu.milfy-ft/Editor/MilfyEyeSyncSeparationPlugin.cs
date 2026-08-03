using System.Linq;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using nadena.dev.ndmf.fluent;
using UnityEngine;

[assembly: ExportsPlugin(typeof(Suu.MilfyFT.Editor.MilfyEyeSyncSeparationPlugin))]

namespace Suu.MilfyFT.Editor
{
    /// <summary>
    /// Jerry's Shared controller uses FT/EyeSync for both gaze and eyelids.
    /// Milfy remaps FT/EyeSync to Milfy/GazeSync, so separate the eyelid use
    /// after Modular Avatar has applied parameter remapping and merged layers.
    /// </summary>
    internal sealed class MilfyEyeSyncSeparationPlugin : Plugin<MilfyEyeSyncSeparationPlugin>
    {
        private const string GazeSyncParameter = "Milfy/GazeSync";
        private const string EyelidSyncAlwaysOffParameter = "Milfy/Internal/EyelidSyncAlwaysOff";
        private const string JerryEyelidSyncTreeName = "EyeSync EyeLids";

        public override string QualifiedName => "jp.suu.milfy-ft.eye-sync-separation";
        public override string DisplayName => "Milfy FT Eye Sync Separation";

        protected override void Configure()
        {
            InPhase(BuildPhase.Transforming)
                .WithRequiredExtension(typeof(AnimatorServicesContext), sequence =>
                    sequence
                        .AfterPlugin("nadena.dev.modular-avatar")
                        .Run("Separate gaze and eyelid synchronization", PatchEyeSync));
        }

        private static void PatchEyeSync(BuildContext context)
        {
            var controllers = context.Extension<AnimatorServicesContext>()
                .ControllerContext
                .GetAllControllers();

            foreach (var controller in controllers)
            {
                // GazeSync is package-owned and identifies a merged Milfy FT controller.
                if (!controller.Parameters.ContainsKey(GazeSyncParameter))
                {
                    continue;
                }

                var eyelidSyncTrees = controller.AllReachableNodes()
                    .OfType<VirtualBlendTree>()
                    .Where(tree => tree.Name == JerryEyelidSyncTreeName &&
                                   tree.BlendParameter == GazeSyncParameter)
                    .ToArray();

                if (eyelidSyncTrees.Length == 0)
                {
                    continue;
                }

                // This parameter is intentionally not an Expression Parameter. Its fixed default
                // keeps Jerry's eyelid synchronization OFF while GazeSync remains user-controllable.
                controller.SetParameter(EyelidSyncAlwaysOffParameter, new AnimatorControllerParameter
                {
                    name = EyelidSyncAlwaysOffParameter,
                    type = AnimatorControllerParameterType.Float,
                    defaultFloat = 0f
                });

                foreach (var tree in eyelidSyncTrees)
                {
                    tree.BlendParameter = EyelidSyncAlwaysOffParameter;
                }
            }
        }
    }
}
