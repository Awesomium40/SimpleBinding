using System;

namespace SimpleBinding
{

    public interface IBindingConverter
    {
        /// <summary>
        /// Performs conversion from the source value to the target value type
        /// </summary>
        /// <param name="source">the source value to convert</param>
        /// <param name="targetType">The type of the property on the target to which to convert</param>
        /// <returns></returns>
        object ConvertSourceToTarget(object source, Type targetType);

        /// <summary>
        /// Performs conversion from the target value to the source value type
        /// </summary>
        /// <param name="target">the target value to convert</param>
        /// <param name="sourceType">The type to which to convert the value</param>
        /// <returns></returns>
        object ConvertTargetToSource(object target, Type sourceType);
    }
}
