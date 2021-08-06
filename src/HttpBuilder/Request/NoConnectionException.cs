using System;

namespace HttpBuilder
{
    /// <summary>
    /// Helper exception for catching send and / or connection errors.
    /// Contains an inner exception, but not in the InnerException field, but in the NativeException property
    /// </summary>
    public class NoConnectionException : Exception
    {
        public Exception NativeException { get; }

        public NoConnectionException(string message, Exception nativeException)
            : base(message)
        {
            NativeException = nativeException;
        }
    }
}
