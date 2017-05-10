using System;

namespace Gigya.Microdot.SharedLogic.Utils
{
    public class Lazy<TValue, TParam>
    {
        private readonly object _syncObj = new object();

        private TValue _value;
        private bool _hasValue;
        private readonly Func<TParam, TValue> _func;

        public Lazy(Func<TParam, TValue> func)
        {
            _func = func;
        }

        public TValue GetValue(TParam parameter)
        {
            if (_hasValue) return _value;

            lock (_syncObj)
            {
                if (_hasValue) return _value;

                _value = _func(parameter);
                _hasValue = true;
            }

            return _value;
        }

        public TValue SetValue(TValue value)
        {
            if (_hasValue) return _value;

            lock (_syncObj)
            {
                if (_hasValue) return _value;

                _value = value;
                _hasValue = true;
            }

            return _value;
        }
    }
}
