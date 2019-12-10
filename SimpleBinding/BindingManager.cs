using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq.Expressions;
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
        /// Creates a new binding and returns its ID within the BindingManager
        /// </summary>
        /// <typeparam name="TSource">The type of the source object</typeparam>
        /// <typeparam name="TSourceProp">The type of the source object's bound property</typeparam>
        /// <typeparam name="TTarget">The type of the target object</typeparam>
        /// <typeparam name="TTargetProp">The type of the target object's bound property</typeparam>
        /// <param name="source">The source object</param>
        /// <param name="sourceProp">Expression describing the property of the source object to be bound</param>
        /// <param name="target">The target object</param>
        /// <param name="targetProp">Expression describing the property of the target object to be bound</param>
        /// <returns>the integer ID of the Binding within the manager's context</returns>
        public static int
            CreateBinding<TSource, TSourceProp, TTarget, TTargetProp>
            (TSource source, Expression<Func<TSource, TSourceProp>> sourceProp, 
                    TTarget target, Expression<Func<TTarget, TTargetProp>> targetProp, BindingMode 
                    mode=BindingMode.TwoWay, IBindingConverter converter=null) 
            where TSource : INotifyPropertyChanged
            where TTarget : INotifyPropertyChanged
        {
            bool bindingAdded;
            int id = Interlocked.Increment(ref _bindingCounter);

            //First thing to do is attempt to create the binding
            TypedBinding<TSource, TSourceProp, TTarget, TTargetProp> binding =
                new TypedBinding<TSource, TSourceProp, TTarget, TTargetProp>(id, source, sourceProp, target, targetProp, mode, converter);
            
            //Once a new binding has been created successfully,
            //It can be added to the context for tracking and (when necessary) disposal
            bindingAdded = _bindings.TryAdd(id, binding);
            if (!bindingAdded)
                throw new BindingException("Unable to create binding because ");

            //The BindingManager needs to be notified when a binding becomes inactive so that its resources can be disposed of
            binding.PropertyChanged += Binding_OnIsActiveChanged;
                
            return id;
        }

        public static void DestroyBinding(int key)
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
                DestroyBinding(b.BindingID);
            }
        }
    }
}
