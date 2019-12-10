<H1>SimpleBinding</H1>
<p>SimpleBinding is intended to be an easy-to-use solution for binding C# models to their respective view models.</p>

<p>In my attempt to learn the MVVM pattern in C# WPF, one of the more frustrating jobs was having to write a significant amount of boilerplate to keep view model properties in sync with their respective model properties ; while WPF makes the data binding process between the UI and code-behind objects simple, the same can't be said for C# objects. After looking around and not finding any solution to this problem, I decided to try and solve it myself</p>

<p>SimpleBinding makes the process of keeping code-behind objects in sync..well..simple. It requires only that:</p>
<ol>
  <li>The source and target objects to be bound each implement INotifyPropertyChanged</li>
  <li>The properties of the source and target objects to be bound are publicly accessible properties of the objects</li>
</ol>

<p>Creating and destroying bindings happens through the static methods of the <code>BindingManager</code> class</p>
<p>
<code>
BindingManager.CreateBinding(someSourceObject, s => s.SomeSourceProperty, someTargetObject, t => t.SomeTargetProperty, mode=BindingMode.TwoWay, converter=null);
</code>
</p>
<p> where <code>someSourceObject</code> and <code>someTargetObject</code> are objects which implement <code>INotifyPropertyChanged</code>. </p>
<p>The lambda expressions such as <code>s => s.SomeSourceProperty</code> indicate the properties of the source/target to be bound.</p> 
<p>the 'mode' parameter specifies the mode for binding (BindingMode.TwoWay, BindingMode.OneWay, and BindingMode.OneWayToSource). The default for this parameter is BindingMode.TwoWay.</p> 
<p>Finally, the 'converter' parameter allows you to provide an instance of IBindingConverter (part of the framework) which will convert between the source and target values. Setting this parameter to null will cause no converter to be used. If no converter is specified and the types of the source and target properties are incompatible, an exception will be raised</p>
