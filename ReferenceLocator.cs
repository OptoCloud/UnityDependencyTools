using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace OptoCloud
{
    internal class ReferenceLocator : EditorWindow
    {
        #region Constants

        private const string TRASH_PATH = "Assets/Project/Components/Unused/";
        private const string TEXTURES_PATH = "Assets/Project/Components/Textures/";
        private const string TEXTURE_GRADIENTS_PATH = "Assets/Project/Components/Textures/Gradients/";
        private const string ANIMATIONS_PATH = "Assets/Project/Components/Animations/";

        #endregion

        // Deduplication functions
        #region Dedupers
        private static void Deduplicate(Texture2D texture)
        {
            HashSet<Texture2D> allTextures = new HashSet<Texture2D>(FindAssetsByType<Texture2D>());
            HashSet<Texture2D> duplicateTextures = new HashSet<Texture2D>();
        }
        private static void Deduplicate(AnimationClip animationClip)
        {
            // Get all clips that matches this one
            HashSet<AnimationClip> matchingClips = new HashSet<AnimationClip>(FindAssetsByType<AnimationClip>().Where(c => !AssetDatabase.GetAssetPath(c).StartsWith(TRASH_PATH) && c.ContentCompare(animationClip)));

            // Get the path to the target clip
            string clipPath = AssetDatabase.GetAssetPath(animationClip);

            // Check if there is any clip that is already in the common directory, these would be preferred above the current clip
            AnimationClip preferredClip = animationClip;
            if (!clipPath.StartsWith(ANIMATIONS_PATH))
            {
                AnimationClip betterClip = matchingClips.Where(c => c != animationClip && AssetDatabase.GetAssetPath(c).StartsWith(ANIMATIONS_PATH)).FirstOrDefault();
                if (betterClip != null)
                {
                    preferredClip = betterClip;
                }
                else
                {
                    // If we cant find a better clip, then try to move the target clip to the common directory
                    string dstPath = PathUtils.GenerateNonConflictingPath(ANIMATIONS_PATH + PathUtils.GetFileName(clipPath));
                    if (PathUtils.TryMoveFile(clipPath, dstPath))
                    {
                        AssetDatabase.Refresh();
                    }
                }
            }

            // Go trough all animation controllers and swap out clips matching the current one
            foreach (AnimatorController controller in FindAssetsByType<AnimatorController>("AnimatorController"))
            {
                Crawl(controller, (AnimationClip clip) => clip != preferredClip && clip.ContentCompare(preferredClip) ? preferredClip : clip);
            }

            // Move all other duplicate clips to the trash directory (I dont want to delete them in case I have some bug in my code)
            foreach (var duplicate in matchingClips.Where(c => c != preferredClip))
            {
                try
                {
                    string path = AssetDatabase.GetAssetPath(duplicate);
                    string dstPath = PathUtils.GenerateNonConflictingPath(TRASH_PATH + PathUtils.GetFileName(clipPath));

                    if (PathUtils.TryMoveFile(clipPath, dstPath))
                    {
                        AssetDatabase.Refresh();
                    }
                }
                catch (System.Exception)
                {
                }
            }
        }
        #endregion
        
        // Functions used for retrieving objects from unity's filesystem
        #region Finders
        
        /// <summary>
        /// Gets assets from the project directory by typename, use this if FindAssetsByType<T>() doesnt work
        /// </summary>
        /// <typeparam name="T">The type you want returned</typeparam>
        /// <param name="typename">The filtername of the type you want to get</param>
        /// <returns></returns>
        static IEnumerable<T> FindAssetsByType<T>(string typename) where T : UnityEngine.Object
        {
            foreach (string guid in AssetDatabase.FindAssets("t:" + typename))
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
                if (asset != null)
                {
                    yield return asset;
                }
            }
        }

        /// <summary>
        /// Gets assets from the project directory by type
        /// </summary>
        /// <typeparam name="T">The type you want returned</typeparam>
        /// <returns></returns>
        static IEnumerable<T> FindAssetsByType<T>() where T : UnityEngine.Object
        {
            return FindAssetsByType<T>(typeof(T).ToString().Replace("UnityEngine.", ""));
        }
        
        /// <summary>
        /// Finds all materials in project that uses the provided texture
        /// </summary>
        /// <param name="selectedTexture"></param>
        /// <returns></returns>
        static IEnumerable<Material> FindLinkedMaterials(Texture2D selectedTexture)
        {
            foreach (Material material in FindAssetsByType<Material>())
            {
                foreach (string texName in material.GetTexturePropertyNames())
                {
                    if (material.GetTexture(texName) is Texture2D texture && texture == selectedTexture)
                    {
                        yield return material;
                    }
                }
            }
        }

        /// <summary>
        /// Finds all textures in project that is used within the provided material
        /// </summary>
        /// <param name="selectedMaterial"></param>
        /// <returns></returns>
        static IEnumerable<Texture2D> FindLinkedTextures(Material selectedMaterial)
        {
            foreach (string texName in selectedMaterial.GetTexturePropertyNames())
            {
                if (selectedMaterial.GetTexture(texName) is Texture2D texture)
                {
                    yield return texture;
                }
            }
        }

        /// <summary>
        /// Finds all textures in project matching the provided texture
        /// </summary>
        /// <param name="selectedTexture"></param>
        /// <returns></returns>
        static IEnumerable<Texture2D> FindDuplicateTextures(Texture2D selectedTexture)
        {
            foreach (Texture2D texture in FindAssetsByType<Texture2D>())
            {
                if (texture.imageContentsHash == selectedTexture.imageContentsHash)
                {
                    yield return texture;
                }
            }
        }

        /// <summary>
        /// Finds all animationclips in project that matched the provided animationclip
        /// </summary>
        /// <param name="selectedAnimationClip"></param>
        /// <returns></returns>
        static IEnumerable<AnimationClip> FindDuplicateAnimationClips(AnimationClip selectedAnimationClip)
        {
            foreach (AnimationClip animationClip in FindAssetsByType<AnimationClip>())
                if (animationClip.ContentCompare(selectedAnimationClip))
                    yield return animationClip;
        }

        #endregion

        // These crawl trough unity objects, replacing containing data using the provided item processor
        #region Crawlers

        public delegate T ItemProcessorDelegate<T>(T item);

        private static AnimationClip Crawl<T>(AnimationClip animationClip, ItemProcessorDelegate<T> getReplacement)
        {
            if (getReplacement is ItemProcessorDelegate<AnimationClip> getAnimationClipReplacement)
                return getAnimationClipReplacement(animationClip);

            return animationClip;
        }
        private static BlendTree Crawl<T>(BlendTree blendTree, ItemProcessorDelegate<T> getReplacement)
        {
            if (getReplacement is ItemProcessorDelegate<BlendTree> getBlendTreeReplacement)
                return getBlendTreeReplacement(blendTree);

            ChildMotion[] childMotions = blendTree.children;
            for (int i = 0; i < childMotions.Length; i++)
            {
                childMotions[i] = Crawl(childMotions[i], getReplacement);
            }
            blendTree.children = childMotions;

            return blendTree;
        }
        private static Motion Crawl<T>(Motion motion, ItemProcessorDelegate<T> getReplacement)
        {
            if (getReplacement is ItemProcessorDelegate<Motion> getMotionReplacement)
                return getMotionReplacement(motion);

            if (motion is AnimationClip clip)
            {
                motion = Crawl(clip, getReplacement);
            }
            else if (motion is BlendTree blendTree)
            {
                motion = Crawl(blendTree, getReplacement);
            }

            return motion;
        }
        private static ChildMotion Crawl<T>(ChildMotion childMotion, ItemProcessorDelegate<T> getReplacement)
        {
            if (getReplacement is ItemProcessorDelegate<ChildMotion> getChildMotionReplacement)
                return getChildMotionReplacement(childMotion);

            childMotion.motion = Crawl(childMotion.motion, getReplacement);

            return childMotion;
        }
        private static AnimatorState Crawl<T>(AnimatorState animatorState, ItemProcessorDelegate<T> getReplacement)
        {
            if (getReplacement is ItemProcessorDelegate<AnimatorState> getAnimatorStateReplacement)
                return getAnimatorStateReplacement(animatorState);

            animatorState.motion = Crawl(animatorState.motion, getReplacement);
            return animatorState;
        }
        private static ChildAnimatorState Crawl<T>(ChildAnimatorState childAnimatorState, ItemProcessorDelegate<T> getReplacement)
        {
            if (getReplacement is ItemProcessorDelegate<ChildAnimatorState> getChildAnimatorStateReplacement)
                return getChildAnimatorStateReplacement(childAnimatorState);

            childAnimatorState.state = Crawl(childAnimatorState.state, getReplacement);
            return childAnimatorState;
        }
        private static ChildAnimatorStateMachine Crawl<T>(ChildAnimatorStateMachine childAnimatorStateMachine, ItemProcessorDelegate<T> getReplacement)
        {
            if (getReplacement is ItemProcessorDelegate<ChildAnimatorStateMachine> getChildAnimatorStateMachineReplacement)
                return getChildAnimatorStateMachineReplacement(childAnimatorStateMachine);

            childAnimatorStateMachine.stateMachine = Crawl(childAnimatorStateMachine.stateMachine, getReplacement);
            return childAnimatorStateMachine;
        }
        private static AnimatorStateMachine Crawl<T>(AnimatorStateMachine animatorStateMachine, ItemProcessorDelegate<T> getReplacement)
        {
            if (getReplacement is ItemProcessorDelegate<AnimatorStateMachine> getAnimatorStateMachineReplacement)
                return getAnimatorStateMachineReplacement(animatorStateMachine);

            ChildAnimatorState[] states = animatorStateMachine.states;
            for (int i = 0; i < states.Length; i++)
            {
                states[i] = Crawl(states[i], getReplacement);
            }
            animatorStateMachine.states = states;

            ChildAnimatorStateMachine[] machines = animatorStateMachine.stateMachines;
            for (int i = 0; i < machines.Length; i++)
            {
                machines[i] = Crawl(machines[i], getReplacement);
            }
            animatorStateMachine.stateMachines = machines;

            return animatorStateMachine;
        }
        private static AvatarMask Crawl<T>(AvatarMask avatarMask, ItemProcessorDelegate<T> getReplacement)
        {
            if (getReplacement is ItemProcessorDelegate<AvatarMask> getAvatarMaskReplacement)
                return getAvatarMaskReplacement(avatarMask);

            // UNIMPELEMENTED
            return avatarMask;
        }
        private static AnimatorControllerLayer Crawl<T>(AnimatorControllerLayer animatorControllerLayer, ItemProcessorDelegate<T> getReplacement)
        {
            if (getReplacement is ItemProcessorDelegate<AnimatorControllerLayer> getAnimatorControllerReplacement)
                return getAnimatorControllerReplacement(animatorControllerLayer);

            animatorControllerLayer.stateMachine = Crawl(animatorControllerLayer.stateMachine, getReplacement);
            animatorControllerLayer.avatarMask = Crawl(animatorControllerLayer.avatarMask, getReplacement);
            return animatorControllerLayer;
        }
        private static AnimatorController Crawl<T>(AnimatorController animatorController, ItemProcessorDelegate<T> getReplacement)
        {
            if (getReplacement is ItemProcessorDelegate<AnimatorController> getAnimatorControllerReplacement)
                return getAnimatorControllerReplacement(animatorController);

            AnimatorControllerLayer[] layers = animatorController.layers;
            for (int i = 0; i < layers.Length; i++)
            {
                layers[i] = Crawl(layers[i], getReplacement);
            }
            animatorController.layers = layers;

            return animatorController;
        }
        #endregion

        // Menu items for Unity GUI
        #region Menu Items

        #region Linked Material Selector

        [MenuItem("Assets/Refloc/Linked Materials", true)]
        public static bool FindSelectionLinkedMaterialsValidator()
        {
            return Selection.activeObject is Texture2D;
        }
        [MenuItem("Assets/Refloc/Linked Materials")]
        public static void FindSelectionLinkedMaterials()
        {
            Selection.objects = FindLinkedMaterials(Selection.activeObject as Texture2D).Cast<Object>().ToArray();
        }

        #endregion
        #region Linked Texture Selector

        [MenuItem("Assets/Refloc/Linked Textures", true)]
        public static bool FindSelectionLinkedTexturesValidator()
        {
            return Selection.activeObject is Material;
        }
        [MenuItem("Assets/Refloc/Linked Textures")]
        public static void FindSelectionLinkedTextures()
        {
            Selection.objects = FindLinkedTextures(Selection.activeObject as Material).Cast<Object>().ToArray();
        }

        #endregion
        #region Duplicate Selector

        [MenuItem("Assets/Refloc/Duplicates", true)]
        public static bool FindSelectionDuplicatesValidator()
        {
            return Selection.activeObject is Texture2D || Selection.activeObject is AnimationClip;
        }
        [MenuItem("Assets/Refloc/Duplicates")]
        public static void FindSelectionDuplicates()
        {
            if (Selection.activeObject is Texture2D texture)
            {
                Selection.objects = FindDuplicateTextures(texture).Cast<Object>().ToArray();
            }
            else if (Selection.activeObject is AnimationClip animationClip)
            {
                Selection.objects = FindDuplicateAnimationClips(animationClip).Cast<Object>().Where(a => a != animationClip).ToArray();
            }
        }

        #endregion
        #region Deduplicator

        [MenuItem("Assets/Refloc/Deduplicate", true)]
        public static bool DeDuplicateSelectionValidator()
        {
            return Selection.activeObject is Texture2D || Selection.activeObject is AnimationClip;
        }
        [MenuItem("Assets/Refloc/Deduplicate")]
        public static void DeDuplicateSelection()
        {
            if (Selection.activeObject is Texture2D selectedTexture)
            {
                Deduplicate(selectedTexture);
            }
            else if (Selection.activeObject is AnimationClip selectedClip)
            {
                Deduplicate(selectedClip);
            }
        }

        #endregion

        #endregion
    }
}