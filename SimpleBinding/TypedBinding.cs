using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Windows;
using Expression = System.Linq.Expressions.Expression;

namespace SimpleBinding
{
    internal class TypedBinding<TSource, TSourceProp, TTarget, TTargetProp> : Binding
        where TSource : INotifyPropertyChanged
        where TTarget : INotifyPropertyChanged
    {

        #region backing fields
        private WeakReference _sourceObject;
        private WeakReference _targetObject;
        private Func<TSource, TSourceProp> _sourcePropertyGet;
        private Action<TSource, TSourceProp> _sourcePropertySet;
        private Func<TTarget, TTargetProp> _targetPropertyGet;
        private Action<TTarget, TTargetProp> _targetPropertySet;
        private readonly BindingMode _mode;
        private IBindingConverter _converter;
        #endregion

        #region public properties
        public BindingMode BindingMode => _mode;

        #endregion

        #region constructor
        internal TypedBinding(int id, TSource source, 
            Expression<Func<TSource, TSourceProp>> sourceProperty,
            TTarget target, Expression<Func<TTarget, TTargetProp>> targetProperty, 
            BindingMode mode=BindingMode.TwoWay, IBindingConverter converter=null)
        :base(id, typeof(TSource), typeof(TTarget))
        {
            //Make sure types are compatible when a converter is not specified
            //If a converter is specified, no need to worry about compatibility, as programming a working converter is the dev problem
            if (converter == null && typeof(TSourceProp) != typeof(TTargetProp))
                throw new ArgumentNullException("Unable to create binding because " + 
                                                $"{typeof(TTargetProp)} and {typeof(TSourceProp)} " + 
                                                "Are not the same type and no converter was specified");

            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            //Retrieving the MemberExpression objects indicated by the expressions provided to the constructor is vital to 
            //Constructing the delegates to getter/setter methods for the bound properties
            MemberExpression sourceMember = GetMemberExpression(sourceProperty.Body);
            MemberExpression targetMember = GetMemberExpression(targetProperty.Body);

            _sourcePropName = sourceMember.Member.Name;
            _targetPropName = targetMember.Member.Name;

            _sourceObject = new WeakReference(source);
            _targetObject = new WeakReference(target);
            _converter = converter ?? new NonConverter();
            _mode = mode;


            //Which delegates need to be created and which events need subscription depends on the nature of the binding
            if (BindingMode == BindingMode.OneWay || BindingMode == BindingMode.TwoWay)
            {
                _sourcePropertyGet = (Func<TSource, TSourceProp>)CreateDelegate<TSource, TSourceProp>(sourceMember, DelegateType.Get);
                _targetPropertySet = (Action<TTarget, TTargetProp>)CreateDelegate<TTarget, TTargetProp>(targetMember, DelegateType.Set);

                PropertyChangedEventManager.AddHandler(source, this.OnSourceChanged, sourceMember.Member.Name);
            }

            if (BindingMode == BindingMode.OneWayToSource || BindingMode == BindingMode.TwoWay)
            {
                _sourcePropertySet = (Action<TSource, TSourceProp>)CreateDelegate<TSource, TSourceProp>(sourceMember, DelegateType.Set);
                _targetPropertyGet = (Func<TTarget, TTargetProp>)CreateDelegate<TTarget, TTargetProp>(targetMember, DelegateType.Get);
                PropertyChangedEventManager.AddHandler(target, this.OnTargetChanged, targetMember.Member.Name);
            }
        }
        #endregion

        /// <summary>
        /// Creates an open-instance delegate to the property described by memberExpression, delegating to the method (get/set)
        /// corresponding to type
        /// </summary>
        /// <typeparam name="TObj">The type of the source object which is the target of the created delegate</typeparam>
        /// <typeparam name="TProp"></typeparam>
        /// <param name="memberExpression">MemberExpression instance that corresponds to the instance property for which to create a delegate</param>
        /// <param name="type">DelegateType enum specifying the method to which a delegate is to be created (getter/setter)</param>
        /// <returns>Delegate to a get or set method of the property described by memberExpression</returns>
        /// <exception cref="BindingException">Thrown when TObj does not expose a publicly accessible property as specified in expression</exception>
        /// <exception cref="ArgumentException">Thrown when invalid value specified for type</exception>
        /// <exception cref="NotImplementedException">Thrown when attempting to create a get/set delegate, but the property has no
        ///corresponding, publicly accessible get/set method</exception>
        protected Delegate CreateDelegate<TObj, TProp>(MemberExpression memberExpression, DelegateType type)
        {
            Delegate d;
            MethodInfo mi;
            Type methodType = type == DelegateType.Get
                ? typeof(Func<TObj, TProp>)
                : typeof(Action<TObj, TProp>);

            if (!(memberExpression.Member is PropertyInfo prop))
                throw new BindingException("Unable to create binding because " +
                                           $"{memberExpression.Member.Name} is not a publicly accessable property of {typeof(TObj)}");

            mi = type == DelegateType.Get 
                ? prop.GetMethod 
                : type == DelegateType.Set 
                    ? prop.SetMethod
                    : throw new ArgumentException("parameter DelegateType must be valid member of DelegateType enum");

            if (mi == null)
                throw new NotImplementedException("Unable to create binding because " +
                                                  $"property {prop.Name} has no publicly available {type} method");

            d = Delegate.CreateDelegate(methodType, mi);

            return d;
        }

        /// <summary>
        /// Retrieves the MemberExpression contained within the Expression e
        /// </summary>
        /// <param name="e">The expression from which the inner MemberExpression is to be extracted</param>
        /// <returns>MemberExpression</returns>
        /// <exception cref="BindingException">Thrown when e is not a member expression 
        /// and does not contain a MemberExpression</exception>
        protected MemberExpression GetMemberExpression(Expression e)
        {
            if (e is MemberExpression memberExpression)
            {
                return memberExpression;
            }
            else if (e is UnaryExpression unaryExpression &&
                     (unaryExpression.NodeType == ExpressionType.Convert ||
                      unaryExpression.NodeType == ExpressionType.ConvertChecked))
            {
                return GetMemberExpression(unaryExpression.Operand);
            }
            else
            {
                throw new BindingException("Unsupported Expression type");
            }
        }

        protected void UpdateTargetFromSource(object state)
        {
            if (_sourceObject.Target is TSource source && _targetObject.Target is TTarget target)
            {
                lock(source)
                lock (target)
                {
                    TSourceProp sourceValue = _sourcePropertyGet(source);
                    TTargetProp converted = (TTargetProp)_converter.ConvertSourceToTarget(sourceValue, typeof(TTargetProp));
                    _targetPropertySet(target, converted);
                }
                
            }
            else
            {
                IsActive = false;
            }
        }

        protected void UpdateSourceFromTarget(object state)
        {
            if (_sourceObject.Target is TSource source && _targetObject.Target is TTarget target)
            {
                lock(source)
                lock (target)
                {
                    TTargetProp targetValue = _targetPropertyGet(target);
                    TSourceProp converted = (TSourceProp)_converter.ConvertTargetToSource(targetValue, typeof(TSourceProp));
                    _sourcePropertySet(source, converted);
                }
            }
            else
            {
                IsActive = false;
            }
        }

        /// <summary>
        /// Event that cires when the bound source property has notified that its value has changed
        /// </summary>
        /// <param name="sender">the object raising the event</param>
        /// <param name="e">PropertyChangedEventArgs instance</param>
        protected void OnSourceChanged(object sender, PropertyChangedEventArgs e)
        {
            if ((_mode == BindingMode.OneWay || _mode == BindingMode.TwoWay)
                && !_isUpdating)
            {
                try
                {
                    _isUpdating = true;
                    if (BindingManager.SynchronizationContext != null)
                        BindingManager.SynchronizationContext.Send(UpdateTargetFromSource, null);

                    else
                        UpdateTargetFromSource(null);
                }
                catch
                {
                    Debug.Print($"Update of property {SourceType}.{SourceProperty} " +
                                $"to {TargetType}.{TargetProperty} failed. " + 
                                "Source property should be reverted to avoid inconsistent states");
                    throw;
                }
                finally
                {
                    _isUpdating = false;
                }
            }
        }

        /// <summary>
        /// Event that cires when the bound target property has notified that its value has changed
        /// </summary>
        /// <param name="sender">the object raising the event</param>
        /// <param name="e">PropertyChangedEventArgs instance</param>
        protected void OnTargetChanged(object sender, PropertyChangedEventArgs e)
        {
            if ((_mode == BindingMode.OneWayToSource || _mode == BindingMode.TwoWay) && !_isUpdating)
            {
                try
                {
                    _isUpdating = true;
                    if (BindingManager.SynchronizationContext != null)
                        BindingManager.SynchronizationContext.Send(UpdateSourceFromTarget, null);

                    else
                        UpdateSourceFromTarget(null);
                }
                catch
                {
                    Debug.Print($"Update of property {TargetType}.{TargetProperty} " +
                                $"to {SourceType}.{SourceProperty} failed. Source property was reverted");
                    throw;
                }
                finally
                {
                    _isUpdating = false;
                }
            }
        }

        /// <summary>
        /// Cleans up references in the current instance when a binding is no longer active
        /// </summary>
        public override void Dispose()
        {
            if (_sourceObject.Target is TSource source)
            {
                PropertyChangedEventManager.RemoveHandler(source, OnSourceChanged, SourceProperty);
            }

            if (_targetObject.Target is TTarget target)
            {
                PropertyChangedEventManager.RemoveHandler(target, OnTargetChanged, TargetProperty);
            }
                
            _sourceObject.Target = null;
            _targetObject.Target = null;
            _sourceObject = null;
            _targetObject = null;
            _converter = null;
            _sourcePropertyGet = null;
            _sourcePropertySet = null;
            _targetPropertyGet = null;
            _targetPropertySet = null;
        }
    }
}
