using System;
using System.Threading;
using System.Threading.Tasks;

namespace HttpBuilder
{
    /// <summary>
    /// A class that encapsulates some intermediate result of processing an HTTP request
    /// </summary>
    /// <typeparam name="T">Result type</typeparam>
    public sealed class RequestHandler<T>
    {
        internal readonly IRequestObserver Observer;
        private readonly CancellationToken _token;

        /// <summary>
        /// Builder of this request
        /// </summary>
        public readonly RequestBuilder Builder;

        /// <summary>
        /// The task of this handler
        /// </summary>
        public readonly Task<T> Task;

        /// <summary>
        /// Creates a request handler
        /// </summary>
        /// <param name="builder">Requests Builder</param>
        /// <param name="observer">Requests Observer</param>
        /// <param name="task">Requests task</param>
        public RequestHandler(RequestBuilder builder, IRequestObserver observer, Task<T> task)
        {
            Builder = builder;
            Observer = observer;
            Task = task;
        }

        /// <summary>
        /// Converts the result to another result type using the specified delegate
        /// </summary>
        /// <typeparam name="B">Desired result type</typeparam>
        /// <param name="converter">Delegate converting the result</param>
        /// <returns>Returns the transformed request handler</returns>
        public RequestHandler<B> Continue<B>(Converter<T, B> converter)
        {
            return new RequestHandler<B>(Builder, Observer, Task.ContinueWith(task => Convert(task, converter)));
        }

        /// <summary>
        /// Converts the result to another result type using the specified delegate as an asynchronous task
        /// </summary>
        /// <typeparam name="B">Desired result type</typeparam>
        /// <param name="converter">Delegate converting the result</param>
        /// <returns>Returns the transformed request handler</returns>
        public RequestHandler<B> Continue<B>(Converter<T, Task<B>> converter)
        {
            return new RequestHandler<B>(Builder, Observer, Task.ContinueWith(task => Convert(task, converter)).Unwrap());
        }

        /// <summary>
        /// Validates the result at the current transformation step using the specified delegate.
        /// </summary>
        /// <param name="validator">Delegate doing validation</param>
        /// <returns>Returns a link to the request result handler after validation</returns>
        public RequestHandler<T> Validate(Action<T> validator)
        {
            var validation = Task.ContinueWith(task =>
            {
                _token.ThrowIfCancellationRequested();
                var result = task.Result;

                try
                {
                    validator(result);
                }
                catch (Exception ex)
                {
                    Observer.OnValidationFailed(this, result, ex, () => validator(result));
                    throw;
                }
                return result;
            });

            return new RequestHandler<T>(Builder, Observer, validation);
        }

        private B Convert<B>(Task<T> task, Converter<T, B> converter)
        {
            _token.ThrowIfCancellationRequested();
            var result = task.Result;
            Observer.OnBeforeContinue(this, result);
            var convertedResult = converter(result);
            Observer.OnAfterContinue(this, convertedResult);
            return convertedResult;
        }

        /// <summary>
        /// Retrieves the task from the given request handler
        /// </summary>
        /// <param name="self">Link to the request handler</param>
        public static implicit operator Task<T>(RequestHandler<T> self)
        {
            return self.Task;
        }
    }
}
