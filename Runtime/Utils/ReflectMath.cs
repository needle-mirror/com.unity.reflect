// Extension for the System.Math class
// Duplicates functionalities available in UnityEngine.Mathf without requiring UnityEngine
namespace Unity.Reflect
{
    public static class ReflectMath
    {
        public static float Min(params float[] values)
        {
            var min = float.MaxValue;

            var length = values.Length;
            if (length > 0)
            {
                for (var i = 0; i < length; ++i)
                {
                    if (values[i] < min)
                        min = values[i];
                }
            }

            return min;
        }
        
        public static float Max(params float[] values)
        {
            var max = float.MinValue;
            
            var length = values.Length;
            if (length > 0)
            {
                for (var i = 0; i < values.Length; ++i)
                {
                    if (values[i] > max)
                        max = values[i];
                }
            }

            return max;
        }
        
        public static int Min(params int[] values)
        {
            var min = int.MaxValue;

            var length = values.Length;
            if (length > 0)
            {
                for (var i = 0; i < length; ++i)
                {
                    if (values[i] < min)
                        min = values[i];
                }
            }

            return min;
        }
        
        public static int Max(params int[] values)
        {
            var max = int.MinValue;
            
            var length = values.Length;
            if (length > 0)
            {
                for (var i = 0; i < values.Length; ++i)
                {
                    if (values[i] > max)
                        max = values[i];
                }
            }

            return max;
        }
    }
}
