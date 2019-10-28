using System;
using UnityEngine;

namespace Unity.Labs.Utils.GUI
{
    /// <inheritdoc />
    /// <summary>
    /// Used with a special property drawer that can limit which enum options are displayed
    /// </summary>
    public sealed class EnumDisplayAttribute : PropertyAttribute
    {
        public string[] names;
        public int[] values;

        public EnumDisplayAttribute(params object[] enums)
        {
            names = new string[enums.Length];
            values = new int[enums.Length];

            var valueCounter = 0;
            while (valueCounter < values.Length)
            {
                var asEnum = enums[valueCounter] as Enum;

                if (asEnum == null)
                {
                    Debug.LogErrorFormat("Not-enum passed into EnumDisplay Attribute: {0}", enums[valueCounter]);
                    continue;
                }

                names[valueCounter] = asEnum.ToString();
                values[valueCounter] = Convert.ToInt32(asEnum);
                valueCounter++;
            }
        }
    }
}
