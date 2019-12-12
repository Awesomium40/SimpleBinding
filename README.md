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

<H3>Caveats to usage</H3>
<p>Note that binding to nested properties does work (ie binding outer.inner.property to someObject.property), provided that:</p>
<ul>
  <li>inner implements INotifyPropertyChanged (it is not actually necessary for outer to implement INotifyPropertyChanged in this instance)</li>
  <li>the call to CreateBinding is <code>BindingManager.CreateBinding(outer.inner, i => i.property, someObject, someObject.property...)</code></li>
</ul>
<p>In other words, attempting to bind as such:</p>
<p><code>BindingManager.CreateBinding(outer, o => o.inner.property, target, t => t.Property...)</code> <b>will not work</b>, and although I am working on making this work, it is not high on my list of priorities.</p>
<p>To understand why it makes sense that this binding does not work, consider:</p>
<ul>
  <li>In order for outer to notify the binding manager that a property of inner has changed, inner would need to notify outer that said property has changed</li>
  <li>In order for inner to notify outer, inner would need to implement INotifyPropertyChanged and outer would need to subscribe to inners NotifyPropertyChanged event</li>
</ul>

<p>Thus, in any scenario involving a nested binding, the inner object whose property is to be bound must implement INotifyPropertychanged, which means we may as well bind directly to inner rather than going through its containing object first.</p>

<H2>How it works</H2>
//TODO: FILL IN THIS SECTION
