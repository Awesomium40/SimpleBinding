using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows;
using BindingManager.Annotations;
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
        private readonly TSourceProp _fallback;
        private readonly BindingMode _mode;
        private IBindingConverter _converter;
        private readonly object _syncRoot = new object();
        #endregion

        #region public properties
        public BindingMode BindingMode => _mode;

        public TSourceProp FallbackValue => _fallback;
        #endregion

        #region constructor
        internal TypedBinding(int id, TSource source, Expression<Func<TSource, TSourceProp>> sourceProperty, TTarget target, 
            Expression<Func<TTarget, TTargetProp>> targetProperty, TSourceProp fallback, BindingMode mode, IBindingConverter converter)
            : base(id, typeof(TSource), typeof(TTarget))
        {
            if (converter == null && typeof(TSourceProp) != typeof(TTargetProp))
                throw new ArgumentException("Unable to create binding because " +
                                            $"{typeof(TTargetProp)} and {typeof(TSourceProp)} " +
                                            "Are not the same type and no converter was specified");

            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (sourceProperty == null)
                throw new ArgumentNullException(nameof(sourceProperty));
            if (targetProperty == null)
                throw new ArgumentNullException(nameof(targetProperty));

            _fallback = fallback == null ? default(TSourceProp) : fallback;


            //Retrieving the MemberExpression objects indicated by the expressions provided to the constructor is vital to 
            //Constructing the delegates to getter/setter methods for the bound properties
            MemberExpression sourceMember = GetMemberExpression(sourceProperty.Body);
            MemberExpression targetMember = GetMemberExpression(targetProperty.Body);

            _sourcePropName = sourceMember.Member.Name;
            _targetPropName = targetMember.Member.Name;

            _sourceObject = new WeakReference(source);
            _targetObject = new WeakReference(target);
            _converter = converter;
            _mode = mode;


            //Which delegates need to be created and which events need subscription depends on the nature of the binding
            if (BindingMode == BindingMode.OneWay || BindingMode == BindingMode.TwoWay)
            {
                _sourcePropertyGet = (Func<TSource, TSourceProp>)CreateDelegate<TSource, TSourceProp>(sourceMember, DelegateType.Get);
                _targetPropertySet = (Action<TTarget, TTargetProp>)CreateDelegate<TTarget, TTargetProp>(targetMember, DelegateType.Set);

                PropertyChangedEventManager.AddHandler(source, this.OnSourceChanged, sourceMember.Member.Name);

                //This step simply ensures that the properties are put in sync once the binding is made
                OnSourceChanged(source, new PropertyChangedEventArgs(_sourcePropName));
            }

            if (BindingMode == BindingMode.OneWayToSource || BindingMode == BindingMode.TwoWay)
            {
                _sourcePropertySet = (Action<TSource, TSourceProp>)CreateDelegate<TSource, TSourceProp>(sourceMember, DelegateType.Set);
                _targetPropertyGet = (Func<TTarget, TTargetProp>)CreateDelegate<TTarget, TTargetProp>(targetMember, DelegateType.Get);
                PropertyChangedEventManager.AddHandler(target, this.OnTargetChanged, targetMember.Member.Name);

                //Make sure the properties are put in sync once the binding is made
                if (BindingMode == BindingMode.OneWayToSource)
                    OnTargetChanged(target, new PropertyChangedEventArgs(_targetPropName));
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
                : prop.SetMethod;

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
            MemberExpression me;
            if (e is MemberExpression memberExpression)
            {
                me = memberExpression;
            }
            else if (e is UnaryExpression unaryExpression &&
                     (unaryExpression.NodeType == ExpressionType.Convert ||
                      unaryExpression.NodeType == ExpressionType.ConvertChecked))
            {
                me =  GetMemberExpression(unaryExpression.Operand);
            }
            else
            {
                throw new BindingException("Unsupported Expression type");
            }

            return me;
        }

        /// <summary>
        /// Attempts a conversion between toConvert and type TOut using conversionMethod 
        /// and returns the default value of type TOut on failure
        /// </summary>
        /// <typeparam name="TOut">the type after conversion</typeparam>
        /// <typeparam name="TIn">the type before conversion</typeparam>
        /// <param name="toConvert">the value to be converted</param>
        /// <param name="conversionMethod">the function which performs the conversion</param>
        /// <returns>instance of TOut</returns>
        protected TOut TryConvert<TOut, TIn>(TIn toConvert, Func<object, Type, object> conversionMethod)
        {
            TOut converted;

            try
            {
                converted = (TOut) conversionMethod(toConvert, typeof(TOut));
            }
            catch
            {
                converted = default(TOut);
            }

            return converted;
        }

        protected void UpdateTargetFromSource(object state)
        {
            TSourceProp sourceValue;
            TTargetProp converted;

            //If there exists no converter, then the conversion method sould be to attempt a straight type cast
            Func<object, Type, object> conversionMethod = _converter != null 
                ? _converter.ConvertSourceToTarget 
                : (Func<object, Type, object>)((o, type) => (TTargetProp) o);

            if (_sourceObject.Target is TSource source && _targetObject.Target is TTarget target)
            {
                lock(source)
                lock (target)
                {
                    sourceValue = _sourcePropertyGet(source);

                    //If both the source value and its fallback are null (i.e. no data and no fallback), use the default of the type
                    //Otherwise, attempt a conversion and use the result
                    converted = sourceValue == null && _fallback == null 
                        ? default(TTargetProp) 
                        : TryConvert<TTargetProp, TSourceProp>(sourceValue == null ? _fallback : sourceValue, conversionMethod);

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
            TTargetProp targetValue;
            TSourceProp converted;

            Func<object, Type, object> conversionMethod = _converter != null
                ? _converter.ConvertTargetToSource
                : (Func<object, Type, object>) ((o, type) => (TSourceProp)o);

            if (_sourceObject.Target is TSource source && _targetObject.Target is TTarget target)
            {
                lock(source)
                lock (target)
                {
                    targetValue = _targetPropertyGet(target);

                    //Lots of possibilities to consider here.
                    //First, the converter might be null, which should only happen when TSourceProp and TTargetProp are the same
                    //In those instances, we can simply cast straight from source to target or use the fallback as necessary
                    //Second, the target value may be null, in which case we simply return the fallback value
                    //Finally, a converter and a target value might exist. In those cases, attempt to convert using the converter
                    //and if that fails, use the fallback
                    converted = targetValue == null ? _fallback : TryConvert<TSourceProp, TTargetProp>(targetValue, conversionMethod);

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
            if (Monitor.TryEnter(_syncRoot))
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
                        Monitor.Exit(_syncRoot);
                    }
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
            if (Monitor.TryEnter(_syncRoot))
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
                        Monitor.Exit(_syncRoot);
                    }
                }
            }
        }

        /// <summary>
        /// Cleans up references in the current instance when a binding is no longer active
        /// </summary>
         ~TypedBinding()
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
