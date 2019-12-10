using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace SimpleBinding
{
    internal class PropertyChangingEventManager : WeakEventManager
    {
        public static void AddHandler(INotifyPropertyChanging source, EventHandler<PropertyChangingEventArgs> handler)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            CurrentManager.ProtectedAddHandler(source, handler);
        }

        public static void RemoveHandler(INotifyPropertyChanging source, EventHandler<PropertyChangingEventArgs> handler)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            CurrentManager.ProtectedRemoveHandler(source, handler);
        }

        private static PropertyChangingEventManager CurrentManager
        {
            get
            {
                Type managerType = typeof(PropertyChangingEventManager);
                PropertyChangingEventManager manager =
                    (PropertyChangingEventManager)GetCurrentManager(managerType);

                // at first use, create and register a new manager
                if (manager == null)
                {
                    manager = new PropertyChangingEventManager();
                    SetCurrentManager(managerType, manager);
                }

                return manager;
            }
        }

        protected override ListenerList NewListenerList()
        {
            return new ListenerList<PropertyChangingEventArgs>();
        }

        protected override void StartListening(object source)
        {
            if (!(source is INotifyPropertyChanging s))
                throw new ArgumentException($"{nameof(source)} is not an instance of INotifyPropertyChanging");

            s.PropertyChanging += new PropertyChangingEventHandler(OnPropertyChanging);
        }

        protected override void StopListening(object source)
        {
            if (!(source is INotifyPropertyChanging s))
                throw new ArgumentException($"{nameof(source)} is not an instance of INotifyPropertyChanging");

            s.PropertyChanging -= new PropertyChangingEventHandler(OnPropertyChanging);
        }

        private void OnPropertyChanging(object sender, PropertyChangingEventArgs e)
        {
            DeliverEvent(sender, e);
        }
    }
}
