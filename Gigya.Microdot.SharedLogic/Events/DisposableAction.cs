using System;

namespace Gigya.Microdot.SharedLogic.Events
{
    /// <summary>    
    /// A disposable action allows the developer to use it as part of a using statement to call a function on creation and another on disposal.
    /// </summary>    
    class DisposableAction<T> : IDisposable
    {  
        private readonly T _state;
        private Action<T> _dispose;

        /// <summary>    
        /// Initializes a new instance of the <see cref="DisposableAction"/> class.    
        /// </summary>    
        /// <param name="dispose">    
        /// The dispose.    
        /// </param>    
        public DisposableAction(T state, Action<T> dispose)
        {
            if (dispose == null) throw new ArgumentNullException("dispose");
            _state = state;
            _dispose = dispose;
        }


        /// <summary>    
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.    
        /// </summary>    
        /// <filterpriority>2</filterpriority>    
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>    
        /// The dispose.    
        /// </summary>    
        /// <param name="disposing">    
        /// The disposing.    
        /// </param>    
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            { 
                _dispose?.Invoke(_state);
                _dispose = null;
            }
        }
    }
}