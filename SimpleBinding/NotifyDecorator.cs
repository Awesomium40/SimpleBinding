using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using BindingManager.Annotations;
using System.Linq.Expressions;
using System.Reflection;

namespace SimpleBinding
{
    public class NotifyDecorator : INotifyPropertyChanged
    {

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

       private Delegate CreateDelegate<TObj, TProp>(MemberExpression memberExpression, DelegateType type)
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

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
