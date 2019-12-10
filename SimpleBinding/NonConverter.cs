using System;
using System.Diagnostics;

namespace SimpleBinding
{
    internal class NonConverter : IBindingConverter
    {
        public object ConvertSourceToTarget(object source, Type targetType)
        {
            return source;
        }

        public object ConvertTargetToSource(object target, Type sourceType)
        {
            return target;
        }
    }
}
