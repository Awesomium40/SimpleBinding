using System;
using System.CodeDom;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SimpleBinding;
using SimpleBinding.UnitTest.Annotations;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Windows;

namespace SimpleBinding.UnitTest
{
    [TestClass]
    public class BindingTest
    {
        [TestMethod]
        public void TestBindingOptionPerformance()
        {
            MethodInfo getter;
            MethodInfo setter;
            Func<BindingTestObject, int> getDelegate;
            Action<BindingTestObject, int> setDelegate;
            BindingTestObject bto = new BindingTestObject();
            int iterations = 1000;
            long elapsedTicks = 0;
            int x;
            //The way I figure it, there are a couple of ways we can handle the process of bindings
            //First, we can use reflection to grab the get/set methods via PropertyInfo.GetxxxMethod() and invoke those on the bound objects
            //Second we can create and cache delegates to the getters and setters
            //Might be other ways, but these are the two that come to mind at the moment

            //First, let's use reflection to grab the getters and setter
            getter = GetMethodInfo(bto, b => b.SourceIntProperty, "get");
            getDelegate = (Func<BindingTestObject, int>) CreateDelegate(bto, b => b.SourceIntProperty, "get");

            setter = GetMethodInfo(bto, b => b.SourceIntProperty, "set");
            setDelegate = (Action<BindingTestObject, int>) CreateDelegate(bto, b => b.SourceIntProperty, "set");

            Trace.Write("Testing performance of setting via MethodInfo.Invoke()\n");
            for (int i = 0; i < iterations; i++)
            {
                var timer = Stopwatch.StartNew();
                setter.Invoke(bto, new object[] {i});
                timer.Stop();
                elapsedTicks += timer.ElapsedTicks;
            }

            Trace.Write($"{iterations} iterations of setting via MethodInfo.Invoke took {elapsedTicks} ticks\n");
            Trace.Write("***********************************************************************************\n");
            Trace.Write("Testing performance of setting via cached delegate...\n");
            elapsedTicks = 0;

            //Now test cached delegate setting
            for (int i = 0; i < iterations; i++)
            {
                var timer = Stopwatch.StartNew();
                setDelegate(bto, i);
                timer.Stop();
                elapsedTicks += timer.ElapsedTicks;
            }


            Trace.Write($"{iterations} iterations of setting via cached delegate took {elapsedTicks} ticks\n");
            Trace.Write("***********************************************************************************\n");
            Trace.Write("Testing performance of setting via property get/set methods...\n");
            elapsedTicks = 0;

            //Finally, test the simple property getter
            for (int i = 0; i < iterations; i++)
            {
                var timer = Stopwatch.StartNew();
                bto.SourceIntProperty = i;
                elapsedTicks += timer.ElapsedTicks;
            }

            Trace.Write($"{iterations} iterations of setting via property get/set took {elapsedTicks} ticks\n");
            Trace.Write("***********************************************************************************\n");
            Trace.Write("Testing performance of getting via MethodInfo.Invoke...\n");
            elapsedTicks = 0;

            //Now test cached MethodInvo.invoke getting
            for (int i = 0; i < iterations; i++)
            {
                var timer = Stopwatch.StartNew();
                getter.Invoke(bto, new object[] { });
                timer.Stop();
                elapsedTicks += timer.ElapsedTicks;
            }

            Trace.Write($"{iterations} iterations of getting via MethodInfo.invoke took {elapsedTicks} ticks\n");
            Trace.Write("***********************************************************************************\n");
            Trace.Write("Testing performance of getting via cached delegate...\n");
            elapsedTicks = 0;

            //Now test cached MethodInvo.invoke getting
            for (int i = 0; i < iterations; i++)
            {
                var timer = Stopwatch.StartNew();
                getDelegate(bto);
                timer.Stop();
                elapsedTicks += timer.ElapsedTicks;
            }

            Trace.Write($"{iterations} iterations of getting via cached delegate took {elapsedTicks} ticks\n");
            Trace.Write("***********************************************************************************\n");
            Trace.Write("Testing performance of getting via property get/set methods...\n");
            elapsedTicks = 0;

            //Finally, test the simple property getter
            for (int i = 0; i < iterations; i++)
            {
                var timer = Stopwatch.StartNew();
                x = bto.SourceIntProperty;
                elapsedTicks += timer.ElapsedTicks;
            }

            Trace.Write($"{iterations} iterations of getting via property get/set took {elapsedTicks} ticks\n");
            Trace.Write("***********************************************************************************\n");

            //According to the trace output, there is virtually no difference in performance between these two methods for getting and setting (approximately 25 ticks per operation)
            //Interestingly enough, these methods also don't differ from using the property get/set methods themselves, 
            //so there should be no noticeable performance degradation from the binding process. 
        }

        private MethodInfo GetMethodInfo<TObject, TProperty>(TObject target,
            Expression<Func<TObject, TProperty>> expression, string methodType = "get")
        {
            MethodInfo methodInfo;

            if (!(expression.Body is MemberExpression me && me.Member is PropertyInfo pi))
                throw new ArgumentException(nameof(expression));

            switch (methodType)
            {
                case "set":
                    methodInfo = pi.SetMethod;
                    break;
                default:
                    methodInfo = pi.GetMethod;
                    break;
            }

            return methodInfo;
        }

        private Delegate CreateDelegate<TObj, TProp>(TObj target, Expression<Func<TObj, TProp>> expression,
            string type = "get")
        {
            Delegate d;
            MethodInfo mi;
            Type methodType = type == "get"
                ? typeof(Func<TObj, TProp>)
                : typeof(Action<TObj, TProp>);

            if (!(expression.Body is MemberExpression memberExpression && memberExpression.Member is PropertyInfo prop))
                throw new ArgumentException(nameof(expression));

            mi = type == "get"
                ? prop.GetMethod
                : type == "set"
                    ? prop.SetMethod
                    : throw new ArgumentException("parameter DelegateType must be valid member of DelegateType enum");

            if (mi == null)
                throw new NotImplementedException("Unable to create binding because " +
                                                  $"property {prop.Name} has no publicly available {type} method");

            d = Delegate.CreateDelegate(methodType, mi);

            return d;
        }

        [TestMethod]
        public void TestOneWayBindingNoConverter()
        {
            int bindingId;
            BindingTestObject source = new BindingTestObject();
            BindingTestObject target = new BindingTestObject();
            source.SourceIntProperty = -1;
            target.SourceIntProperty = 200;

            bindingId = BindingManager.Register(source, s => s.SourceIntProperty, target, t => t.SourceIntProperty, default(int), BindingMode.OneWay);

            //After binding, the source and target properties should be in sync
            Assert.AreEqual(source.SourceIntProperty, target.SourceIntProperty);

            //After the binding is created, changes to source should propogate to target
            source.SourceIntProperty = 99;
            Assert.AreEqual(source.SourceIntProperty, target.SourceIntProperty);

            //However, with a OneWay binding, changes to the target should NOT propogate to the source
            target.SourceIntProperty = 66;
            Assert.AreNotEqual(source.SourceIntProperty, target.SourceIntProperty);
            Assert.AreEqual(source.SourceIntProperty, 99);

            //Disposing of a binding should cause updates to stop propogating
            BindingManager.Unregister(bindingId);

            source.SourceIntProperty = 0;
            Assert.AreNotEqual(source.SourceIntProperty, target.SourceIntProperty);
            Assert.AreEqual(target.SourceIntProperty, 66);

        }

        [TestMethod]
        public void TestOneWayBindingWithConverter()
        {
            int bindingId;
            BindingTestObject source = new BindingTestObject();
            BindingTestObject target = new BindingTestObject();
            StringToIntConverter converter = new StringToIntConverter();

            bindingId = BindingManager.Register(source, s => s.SourceStringProperty, target,
                t => t.SourceIntProperty, default(string), BindingMode.OneWay, converter);

            //After the binding, changes to the source property should be converted to their integer counterparts 
            //and propogated to the target
            source.SourceStringProperty = "99";
            Assert.AreEqual(target.SourceIntProperty, 99);

            //However, values that cannot be converted to an integer should cause the fallback value to be used. 
            //Since no fallback value was specified, the value should be set to the default of integer type
            source.SourceStringProperty = "Toasty";
            Assert.AreEqual(target.SourceIntProperty, default(int));

            //Also, changes to the target should not affect the source in a OneWay binding
            target.SourceIntProperty = 55;
            Assert.AreNotEqual(source.SourceStringProperty, "55");

            //Disposing of the bindings should cease updates to stop propogating
            BindingManager.Unregister(bindingId);

            source.SourceStringProperty = "-1";
            Assert.AreNotEqual(target.SourceIntProperty, -1);
            Assert.AreEqual(target.SourceIntProperty, 55);
        }

        [TestMethod]
        public void TestTwoWayBindingNoConversion()
        {
            var bto1 = new BindingTestObject();
            var bto2 = new BindingTestObject();
            int bindingKey;

            Func<object> creator = () =>
            {
                var b1 = new BindingTestObject();
                var b2 = new BindingTestObject();

                BindingManager.Register(b1, b => b.SourceIntProperty, b2, b => b.SourceStringProperty);

                return null;
            };
            //Should be impossible to create a binding between two properties of different types without a converter
            Assert.ThrowsException<ArgumentException>(creator, "");

            //Can, however, create binding between properties of the same type
            bindingKey = BindingManager.Register(bto1, t => t.SourceIntProperty, bto2, t => t.SourceIntProperty);

            //Binding should cause target to update when source updates
            bto1.SourceIntProperty = 25;
            Assert.AreEqual(bto2.SourceIntProperty, bto1.SourceIntProperty);

            //Binding should also cause source to update when the target updates
            bto2.SourceIntProperty = 99;
            Assert.AreEqual(bto1.SourceIntProperty, bto2.SourceIntProperty);

            //setting unbound properties should cause no changes on target/source objects, as binding occurs
            //on a per-property basis and not for objects as a whole
            bto1.SourceStringProperty = "Toasty";
            bto2.SourceStringProperty = "Not Toasty";

            Assert.AreNotEqual(bto1.SourceStringProperty, bto2.SourceStringProperty);
            Assert.AreEqual(bto1.SourceStringProperty, "Toasty");
            Assert.AreEqual(bto2.SourceStringProperty, "Not Toasty");

            //Disposing of the binding should cease all propogation of updates
            BindingManager.Unregister(bindingKey);

            bto1.SourceIntProperty = -1;
            bto2.SourceIntProperty = 256;

            Assert.AreNotEqual(bto1.SourceIntProperty, bto2.SourceIntProperty);
            Assert.AreEqual(bto1.SourceIntProperty, -1);
            Assert.AreEqual(bto2.SourceIntProperty, 256);
        }
    }

    public class Notifier : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Method by which the Notifier instance updates its properties
        /// In order for the notifier to interface properly with other classes
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="field"></param>
        /// <param name="value"></param>
        /// <param name="name"></param>
        protected void SetPropertyField<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (!EqualityComparer<T>.Default.Equals(field, value))
            {
                T originalValue = field;
                try
                {
                    field = value;
                    OnPropertyChanged(name);
                }
                catch

                {
                    field = originalValue;
                    throw;
                }    
            }
        }
    }

    public class BindingTestObject : INotifyPropertyChanged
    {
        protected string _sourceStringProp;
        protected int _sourceIntProp;

        public string SourceStringProperty
        {
            get => _sourceStringProp;
            set => SetPropertyField(ref _sourceStringProp, value);
        }

        public int SourceIntProperty
        {
            get => _sourceIntProp;
            set => SetPropertyField(ref _sourceIntProp, value);
        }

        protected void SetPropertyField<T>(ref T field, T value, [CallerMemberName]string name = null)
        {
            T originalValue = field;

            if (!EqualityComparer<T>.Default.Equals(field, value))
            {
                try
                {
                    field = value;
                    OnPropertyChanged(name);
                }
                catch
                {

                    field = originalValue;
                    throw;
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [global::BindingManager.Annotations.NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }


    public class NestedBindingObject : BindingTestObject
    {
        protected BindingTestObject _inner;

        public BindingTestObject InnerObject
        {
            get => _inner;
            set => _inner = value;
        }
    }

    public class StringToIntConverter : SimpleBinding.IBindingConverter
    {
        public object ConvertSourceToTarget(object source, Type targetType)
        {                
            if (source != null && source is string s)
                return int.Parse(s);

            throw new ArgumentException($"Parameter {nameof(source)} expected type 'string'");
        }

        public object ConvertTargetToSource(object target, Type sourceType)
        {
            int t = (int) target;
            return t.ToString();
        }
    }

}
