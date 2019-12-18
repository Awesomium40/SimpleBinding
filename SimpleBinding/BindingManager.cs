using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;


namespace SimpleBinding
{
    public class BindingManager
    {
        private static readonly ConcurrentDictionary<int, Binding> _bindings 
            = new ConcurrentDictionary<int, Binding>();

        private static int _bindingCounter = 0;
        private static SynchronizationContext _context;

        public static SynchronizationContext SynchronizationContext
        {
            get => _context;
            set => _context = value;
        }

        /// <summary>
        /// Registers a binding between sourceProperty on the object source and targetProperty on the object target
        /// </summary>
        /// <typeparam name="TSource">The type of the source object</typeparam>
        /// <typeparam name="TSourceProp">The type of the source object's bound property</typeparam>
        /// <typeparam name="TTarget">The type of the target object</typeparam>
        /// <typeparam name="TTargetProp"></typeparam>
        /// <param name="source">the source object</param>
        /// <param name="sourceProp">Expression describing the property on the source object to be bound</param>
        /// <param name="target">the target object</param>
        /// <param name="targetProp">Expression describing the property on the target object to be bound</param>
        /// <returns>The integer key of the registered binding in the manager's table</returns>
        public static int Register<TSource, TSourceProp, TTarget, TTargetProp>(TSource source, Expression<Func<TSource, TSourceProp>> sourceProp,
            TTarget target, Expression<Func<TTarget, TTargetProp>> targetProp)
            where TSource : INotifyPropertyChanged
            where TTarget : INotifyPropertyChanged
        {
            return Register(source, sourceProp, target, targetProp, default(TSourceProp), BindingMode.TwoWay, null);
        }

        /// <summary>
        /// Registers a binding between sourceProperty on the object source and targetProperty on the object target
        /// </summary>
        /// <typeparam name="TSource">The type of the source object</typeparam>
        /// <typeparam name="TSourceProp">The type of the source object's bound property</typeparam>
        /// <typeparam name="TTarget">The type of the target object</typeparam>
        /// <typeparam name="TTargetProp"></typeparam>
        /// <param name="source">the source object</param>
        /// <param name="sourceProp">Expression describing the property on the source object to be bound</param>
        /// <param name="target">the target object</param>
        /// <param name="targetProp">Expression describing the property on the target object to be bound</param>
        /// <param name="fallback">The fallback value in case the source/target value is null</param>
        /// <returns>The integer key of the registered binding in the manager's table</returns>
        public static int Register<TSource, TSourceProp, TTarget, TTargetProp>(TSource source, Expression<Func<TSource, TSourceProp>> sourceProp,
            TTarget target, Expression<Func<TTarget, TTargetProp>> targetProp, TSourceProp fallback)
            where TSource : INotifyPropertyChanged
            where TTarget : INotifyPropertyChanged
        {
            return Register(source, sourceProp, target, targetProp, fallback, BindingMode.TwoWay, null);
        }

        /// <summary>
        /// Registers a binding between sourceProperty on the object source and targetProperty on the object target
        /// </summary>
        /// <typeparam name="TSource">The type of the source object</typeparam>
        /// <typeparam name="TSourceProp">The type of the source object's bound property</typeparam>
        /// <typeparam name="TTarget">The type of the target object</typeparam>
        /// <typeparam name="TTargetProp"></typeparam>
        /// <param name="source">the source object</param>
        /// <param name="sourceProp">Expression describing the property on the source object to be bound</param>
        /// <param name="target">the target object</param>
        /// <param name="targetProp">Expression describing the property on the target object to be bound</param>
        /// <param name="fallback">The fallback value in case the source/target value is null</param>
        /// <param name="mode">BindingMode which specifies the mode of binding</param>
        /// <returns>The integer key of the registered binding in the manager's table</returns>
        public static int Register<TSource, TSourceProp, TTarget, TTargetProp>(TSource source, Expression<Func<TSource, TSourceProp>> sourceProp,
            TTarget target, Expression<Func<TTarget, TTargetProp>> targetProp, TSourceProp fallback, BindingMode mode)
            where TSource : INotifyPropertyChanged
            where TTarget : INotifyPropertyChanged
        {
            return Register(source, sourceProp, target, targetProp, fallback, mode, null);
        }

        /// <summary>
        /// Registers a binding between sourceProperty on the object source and targetProperty on the object target
        /// </summary>
        /// <typeparam name="TSource">The type of the source object</typeparam>
        /// <typeparam name="TSourceProp">The type of the source object's bound property</typeparam>
        /// <typeparam name="TTarget">The type of the target object</typeparam>
        /// <typeparam name="TTargetProp"></typeparam>
        /// <param name="source">the source object</param>
        /// <param name="sourceProp">Expression describing the property on the source object to be bound</param>
        /// <param name="target">the target object</param>
        /// <param name="targetProp">Expression describing the property on the target object to be bound</param>
        /// <param name="fallback">The fallback value in case the source/target value is null</param>
        /// <param name="mode">BindingMode which specifies the mode of binding</param>
        /// <param name="converter">Instance of IBindingConverter used to convert between source and target types</param>
        /// <returns>The integer key of the registered binding in the manager's table</returns>
        public static int Register<TSource, TSourceProp, TTarget, TTargetProp>(TSource source, Expression<Func<TSource, TSourceProp>> sourceProp,
            TTarget target, Expression<Func<TTarget, TTargetProp>> targetProp, TSourceProp fallback, BindingMode mode, IBindingConverter converter)
        where TSource : INotifyPropertyChanged
        where TTarget : INotifyPropertyChanged
        {
            bool bindingAdded;
            int id = Interlocked.Increment(ref _bindingCounter);

            //First thing to do is attempt to create the binding
            TypedBinding<TSource, TSourceProp, TTarget, TTargetProp> binding =
                new TypedBinding<TSource, TSourceProp, TTarget, TTargetProp>(id, source, sourceProp, target, targetProp, fallback, mode, converter);

            //Once a new binding has been created successfully,
            //It can be added to the context for tracking and (when necessary) disposal
            bindingAdded = _bindings.TryAdd(id, binding);
            if (!bindingAdded)
                throw new BindingException("Unable to create binding because ");

            //The BindingManager needs to be notified when a binding becomes inactive so that its resources can be disposed of
            binding.PropertyChanged += Binding_OnIsActiveChanged;
            return id;
        }

        public static void Unregister(int key)
        {
            bool bindingFound = _bindings.TryRemove(key, out Binding b);

            if (bindingFound)
            {
                b.Dispose();
                b.PropertyChanged -= Binding_OnIsActiveChanged;
            }
            else
            {
                throw new KeyNotFoundException($"Binding with key {key} not found.");
            }
        }

        private static void Binding_OnIsActiveChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is Binding b && !b.IsActive)
            {
                Unregister(b.BindingID);
            }
        }
    }
}
