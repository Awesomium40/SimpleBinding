<H1>SimpleBinding</H1>

<p>WPF makes the process of data binding UI controls to the properties of objects in the code-behind simple and intuitive. Unfortnately, C# doesn't make it quite as easy to bind objects in the code behind to other objects. (i.e. binding the properties of your model objects to the corresponding properties of your view model object)</p>

<H2>Requirements</H2>
<p>In order to bind the properties of two objects, the following is required:</p>
<ul>
  <li>Both the source and target objects must implement INotifyPropertyChanged</li>
  <li>The properties to be data bound must be publicly accessible properties of their respective objects</li>
</ul>
<H2>How to use it</H2>
<p>To bind together the properties of a source and target object, simply invoke the static CreateBinding method of the BindingManager class:</p>
<code>int bindingId = BindingManager.CreateBinding(sourceObject, s => s.SourceProperty, targetObject, t => t.TargetProperty, bindingMode, converter);</code></p>
<p>The <code>bindingMode</code> parameter is a member of the <code>BindingMode</code> enum. You can specify the following:</p>
<ul>
  <li><code>BindingMode.TwoWay</code>(The default if no argument is provided)</li>
  <li><code>BindingMode.OneWay</code></li>
  <li><code>BindingMode.OneWayToSource</code></li>
</ul>
<p>The <code>converter</code> parameter is an object which implements the <code>IBindingConverter</code> interface, which is part of the framework. You may opt not to provide a converter, but if no converter is specified and the source and target properties are of incompatible types, an exception will be raised.</p>

<p>The <code>CreateBinding()</code> method returns an integer key which is used to track and manage the binding internally. You can use this key later to dispose of the binding by providing it to the static <code>DestroyBinding()</code> method.</p>
<p>To remove a data binding, invoke the static DestroyBinding() method and provide the integer key of the binding to be destroyed:</p>
<p>BindingManager.DestroyBinding(bindingId);</p>

<H2>How it works</H2>
//TODO: FILL IN THIS SECTION
