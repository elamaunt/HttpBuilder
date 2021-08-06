
using System;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace HttpBuilder
{
    /// <summary>
    /// Base class for defining a service for handling HTTP requests. Contains the ability to intercept the execution of requests, build them and handle errors
    /// </summary>
    public abstract class HttpService : IHttpService, IRequestObserver, IDisposable
    {
        /// <summary>
        /// Current request handler
        /// </summary>
        public HttpClientHandler Handler { get; }

        /// <summary>
        /// Current client working with HTTP requests
        /// </summary>
        public HttpClient Client { get; }

        /// <summary>
        /// Creates a service instance, invokes the initialization of the HttpClientHandler and the HttpClient.
        /// Overrides the SSL functionality in the ServicePointManager.ServerCertificateValidationCallback property
        /// </summary>
        public HttpService()
        {
            ConfigureHandler(Handler = CreateHandler());
            ConfigureClient(Client = new HttpClient(Handler, true));
            ServicePointManager.ServerCertificateValidationCallback = ValidateSertificate;
        }

        /// <summary>
        /// The method that defines the policy for interacting with X509 SSL certificates. Returns true by default for any certificate
        /// </summary>
        /// <param name="sender">Sender of the request</param>
        /// <param name="certificate">Certificate</param>
        /// <param name="chain">Key chain</param>
        /// <param name="sslPolicyErrors">SSL Policy errors</param>
        /// <returns>Returns whether the given certificate is trusted</returns>
        protected virtual bool ValidateSertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        /// <summary>
        /// Creates an HttpClientHandler that will handle requests.
        /// </summary>
        /// <returns></returns>
        protected virtual HttpClientHandler CreateHandler()
        {
            return new HttpClientHandler();
        }

        /// <summary>
        /// Performs the configuration of the generated HttpClientHandler
        /// </summary>
        /// <param name="handler">Reference to HttpClientHandler</param>
        protected abstract void ConfigureHandler(HttpClientHandler handler);

        /// <summary>
        /// Performs the configuration of the HttpClient
        /// </summary>
        /// <param name="client">Reference to HttpClient</param>
        protected abstract void ConfigureClient(HttpClient client);

        public RequestBuilder Build(string path = null, UriKind kind = UriKind.Relative)
        {
            var builder = new RequestBuilder(this, Client, path, kind);

            OnBuildingStarted(builder);

            return builder;
        }

        /// <summary>
        /// Called immediately when the Build method is called on a new query build
        /// </summary>
        /// <param name="builder">Link to new query builder</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected virtual void OnBuildingStarted(RequestBuilder builder)
        {
        }

        void IRequestObserver.OnBeforeContinue<T>(RequestHandler<T> handler, T result) => OnBeforeContinue(handler, result);

        /// <summary>
        /// Calls before each further conversion of the query result
        /// </summary>
        /// <typeparam name="T">Object type before conversion</typeparam>
        /// <param name="handler">Request result handler</param>
        /// <param name="result">The result at this stage of transformation</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected virtual void OnBeforeContinue<T>(RequestHandler<T> handler, T result)
        {
            // Nothing
        }

        void IRequestObserver.OnAfterContinue<T, B>(RequestHandler<T> handler, B result) => OnAfterContinue(handler, result);

        /// <summary>
        /// Calls after each conversion of the query result
        /// </summary>
        /// <typeparam name="T">Object type before conversion</typeparam>
        /// <typeparam name="B">Object type after conversion</typeparam>
        /// <param name="handler">Request result handler</param>
        /// <param name="result">The result at this stage of transformation</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected virtual void OnAfterContinue<T, B>(RequestHandler<T> handler, B result)
        {
            // Nothing
        }
        
        void IRequestObserver.OnRequestCreated(RequestBuilder builder) => OnRequestCreated(builder);

        /// <summary>
        /// Called every time a query builder is created
        /// </summary>
        /// <param name="builder">Link to builder</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected virtual void OnRequestCreated(RequestBuilder builder)
        {
            // Nothing
        }

        void IRequestObserver.OnRequestStarted(RequestHandler<HttpResponseMessage> handler) => OnRequestStarted(handler);

        /// <summary>
        /// Called every time a new request has started
        /// </summary>
        /// <param name="handler">First request handler</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected virtual void OnRequestStarted(RequestHandler<HttpResponseMessage> handler)
        {
            // Nothing
        }

        void IRequestObserver.OnRequestFinished(RequestHandler<HttpResponseMessage> handler) => OnRequestFinished(handler);

        /// <summary>
        /// Called every time a request has been completed
        /// </summary>
        /// <param name="handler">First request handler</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected virtual void OnRequestFinished(RequestHandler<HttpResponseMessage> handler)
        {
            // Nothing
        }

        void IRequestObserver.OnValidationFailed<T>(RequestHandler<T> handler, T result, Exception ex, Action repeateValidate) => OnValidationFailed(handler, result, ex, repeateValidate);

        /// <summary>
        /// Called every time the result validation returned an error
        /// </summary>
        /// <typeparam name="T">Result type</typeparam>
        /// <param name="handler">Checked request handler</param>
        /// <param name="result">Object that failed validation</param>
        /// <param name="ex">Exception</param>
        /// <param name="repeateValidate">Delegate to repeat validation</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected virtual void OnValidationFailed<T>(RequestHandler<T> handler, T result, Exception ex, Action repeateValidate)
        {
            // Nothing
        }

        string IRequestObserver.BuildHttpExceptionMessage(RequestBuilder builder, HttpResponseMessage res, HttpRequestException ex) => BuildHttpExceptionMessage(builder, res, ex);

        /// <summary>
        /// Builds the message text based on the query result and the thrown exception
        /// </summary>
        /// <param name="builder">Query Builder</param>
        /// <param name="res">Message - query result</param>
        /// <param name="ex">Thrown exception</param>
        /// <returns>Returns the error text corresponding to the given request and exception</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected virtual string BuildHttpExceptionMessage(RequestBuilder builder, HttpResponseMessage res, HttpRequestException ex)
        {
            return ex.Message;
        }

        string IRequestObserver.BuildNoConnectionExceptionMessage(RequestBuilder builder, Exception ex) => BuildNoConnectionExceptionMessage(builder, ex);

        /// <summary>
        /// Builds the message text based on the native exception during connection establishment and / or request sending
        /// </summary>
        /// <param name="builder">Query Builder</param>
        /// <param name="ex">Native exception</param>
        /// <returns>Returns the error text</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected virtual string BuildNoConnectionExceptionMessage(RequestBuilder builder, Exception ex)
        {
            return "Отсутствует подключение к сети интернет";
        }

        Task<HttpResponseMessage> IRequestObserver.TransformSendTask(RequestBuilder builder, HttpRequestMessage message, Func<Task<HttpResponseMessage>> send) => TransformSendTask(builder, message, send);

        /// <summary>
        /// Transforms the task of sending an HTTP message before processing the result as desired by the observer.
        /// Must call send function inside method body
        /// </summary>
        /// <param name="builder">Query Builder</param>
        /// <param name="message">Sent message</param>
        /// <param name="send">The function to send the message to be called in the method</param>
        /// <returns>Returns the started task of sending a message</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected virtual Task<HttpResponseMessage> TransformSendTask(RequestBuilder builder, HttpRequestMessage message, Func<Task<HttpResponseMessage>> send)
        {
            return send();
        }

        #region IDisposable Support
        private bool _disposed = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    Client?.Dispose();
                }
                _disposed = true;
            }
        }
        
         ~HttpService()
         {
            Dispose(false);
         }
        
        void IDisposable.Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
