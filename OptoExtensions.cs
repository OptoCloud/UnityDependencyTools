using UnityEditor;
using UnityEngine;

namespace OptoCloud
{
    internal static class OptoExtensions
    {
        // Functions used to compare the contents of unity objects
        #region ContentCompare Functions

        public static bool ContentCompare(this Keyframe self, Keyframe other)
        {
            return
                Mathf.Approximately(self.time, other.time) &&
                Mathf.Approximately(self.value, other.value) &&
                Mathf.Approximately(self.inTangent, other.inTangent) &&
                Mathf.Approximately(self.inWeight, other.inWeight) &&
                Mathf.Approximately(self.outTangent, other.outTangent) &&
                Mathf.Approximately(self.outWeight, other.outWeight);
        }
        public static bool ContentCompare(this AnimationCurve self, AnimationCurve other)
        {
            if (self != other)
            {
                if (self.length != other.length)
                    return false;

                for (int j = 0; j < self.length; j++)
                {
                    if (
                        !self[j].ContentCompare(other[j]) ||
                        AnimationUtility.GetKeyLeftTangentMode(self, j) != AnimationUtility.GetKeyLeftTangentMode(other, j) ||
                        AnimationUtility.GetKeyRightTangentMode(self, j) != AnimationUtility.GetKeyRightTangentMode(other, j)
                        )
                    {
                        return false;
                    }
                }
            }

            return true;
        }
        public static bool ContentCompare(this AnimationClip self, AnimationClip other)
        {
            if (self == other || (self.empty && other.empty))
                return true;

            if (!Mathf.Approximately(self.length, other.length) || !Mathf.Approximately(self.frameRate, other.frameRate))
                return false;

            string path = AssetDatabase.GetAssetPath(self);
            if (!path.StartsWith("Assets"))
                return false;

            EditorCurveBinding[] selfBindings = AnimationUtility.GetCurveBindings(self);
            EditorCurveBinding[] otherBindings = AnimationUtility.GetCurveBindings(other);

            if (selfBindings.Length != otherBindings.Length)
                return false;

            for (int i = 0; i < selfBindings.Length; i++)
            {
                EditorCurveBinding selfBinding = selfBindings[i];
                EditorCurveBinding otherBinding = otherBindings[i];

                if (selfBinding.path != otherBinding.path || selfBinding.propertyName != otherBinding.propertyName)
                    return false;

                AnimationCurve selfCurve = AnimationUtility.GetEditorCurve(self, selfBinding);
                AnimationCurve otherCurve = AnimationUtility.GetEditorCurve(other, otherBinding);

                if (!selfCurve.ContentCompare(otherCurve))
                    return false;
            }

            return true;
        }

        #endregion

        // Checks if a texture is a gradient based on its dimensions
        public static bool ProbablyIsGradient(this Texture2D self)
        {
            int maxDim = Mathf.Max(self.height, self.width);
            int minDim = Mathf.Min(self.height, self.width);
            return (minDim == 1 || minDim == 4) && maxDim >= 32;
        }
    }
}