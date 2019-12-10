using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using BindingManager.Annotations;

namespace SimpleBinding
{
    internal abstract class Binding : INotifyPropertyChanged
    {
        #region backing fields
        protected bool _isActive;
        protected int _id;
        protected bool _isUpdating;
        protected string _sourcePropName;
        protected string _targetPropName;
        protected Type _sourceType;
        protected Type _targetType;
        #endregion

        #region public properties
        public int BindingID => _id;

        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive != value)
                {
                    _isActive = value;
                    OnPropertyChanged();
                }
            }
        }

        public string SourceProperty => _sourcePropName;
        public string TargetProperty => _targetPropName;

        public Type SourceType => _sourceType;
        public Type TargetType => _targetType;

        public bool IsUpdating => _isUpdating;
        #endregion

        #region constructors
        protected Binding(int id, Type sourceType, Type targetType)
        {
            _id = id;
            _sourceType = sourceType;
            _targetType = targetType;
        }
        #endregion

        #region implements INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

        public abstract void Dispose();
    }
}
