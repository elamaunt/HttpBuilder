using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace HttpBuilder
{
    /// <summary>
    /// Class - Http request builder
    /// </summary>
    public class RequestBuilder
    {
        private readonly HttpClient _client;

        private readonly Uri _uri;
        private readonly NameValueCollection _keyValue;
        private string _additionalQuery;

        /// <summary>
        /// Built Http message
        /// </summary>
        public HttpRequestMessage Message { get; }

        /// <summary>
        /// The identifier of this request (received in increments)
        /// </summary>
        public int RequestId { get; private set; }
        private static volatile int _requestsCounter;
        private static volatile int _currentRequestsCounter;

        /// <summary>
        ///Number of requests that have been made
        /// </summary>
        public int RequestsCounter => _requestsCounter;

        /// <summary>
        /// The number of active requests at the moment
        /// </summary>
        public int CurrentRequestsCounter => _currentRequestsCounter;

        /// <summary>
        /// Final request Uri
        /// </summary>
        public Uri Uri
        {
            get
            {
                UriBuilder builder;
                builder = new UriBuilder(_uri);

                if (_additionalQuery == null)
                    builder.Query = _keyValue.ToString();
                else
                {
                    var q = _keyValue.ToString();

                    if (q.Length == 0)
                        builder.Query = _additionalQuery;
                    else
                        builder.Query = q + "&" + _additionalQuery;
                }

                if (_additionalQuery == null)
                    return builder.Uri;
                else
                    return builder.Uri;
            }
        }

        /// <summary>
        /// Determines if a parameter is defined with the specified key
        /// </summary>
        /// <param name="name">Key name</param>
        /// <returns>Parameter presence flag</returns>
        public bool HasParamater(string name) => !_keyValue.Get(name).IsNullOrWhiteSpace();

        /// <summary>
        /// Determines if the request header is defined with the specified key
        /// </summary>
        /// <param name="name">Key name</param>
        /// <returns>Header presence flag</returns>
        public bool HasHeader(string name) => Message.Headers.Contains(name);

        /// <summary>
        /// Returns the presence of an extra query string for the Uri
        /// </summary>
        public bool HasAdditionalQuery => !_additionalQuery.IsNullOrWhiteSpace();

        /// <summary>
        /// Final request Uri converted to string
        /// </summary>
        public string UriString => Uri.ToString();

        internal readonly IRequestObserver Observer;

        private List<Action<RequestBuilder>> _beforeSend;
        private List<Action<RequestBuilder>> _afterSend;

        public RequestBuilder(IRequestObserver observer, HttpClient client, string path, UriKind kind = UriKind.Relative)
        {
            Observer = observer;
            _client = client;

            var uri = new Uri(path, kind);

            if (!uri.IsAbsoluteUri)
                uri = new Uri(client.BaseAddress, uri);

            _keyValue = HttpUtility.ParseQueryString(uri.Query);
            _uri = new Uri(uri.GetLeftPart(UriPartial.Path));

            Message = new HttpRequestMessage();
        }

        /// <summary>
        /// Adds a delegate that will be called immediately after the request is sent
        /// </summary>
        /// <param name="handler">Delegate</param>
        /// <returns>Returns the query builder</returns>
        public RequestBuilder AfterSend(Action<RequestBuilder> handler)
        {
            if (_afterSend == null)
                _afterSend = new List<Action<RequestBuilder>>();

            _afterSend.Add(handler);
            return this;
        }

        /// <summary>
        /// Adds a delegate that will be called immediately before the request is sent
        /// </summary>
        /// <param name="handler">Delegate</param>
        /// <returns>Returns the query builder</returns>
        public RequestBuilder BeforeSend(Action<RequestBuilder> handler)
        {
            if (_beforeSend == null)
                _beforeSend = new List<Action<RequestBuilder>>();

            _beforeSend.Add(handler);
            return this;
        }

        /// <summary>
        /// Adds a custom string to the request Uri
        /// </summary>
        /// <param name="query">String reference</param>
        /// <returns>Returns the query builder</returns>
        public RequestBuilder WithAdditionalQuery(string query)
        {
            _additionalQuery = query;
            return this;
        }

        /// <summary>
        /// Adds the specified parameters to the query string
        /// </summary>
        /// <typeparam name="T">The type of objects to be converted to string</typeparam>
        /// <param name="dict">Parameter dictionary</param>
        /// <returns>Returns the query builder</returns>
        public RequestBuilder WithParameters<T>(Dictionary<string, T> dict)
          where T : struct
        {
            if (ReferenceEquals(null, dict))
                return this;

            foreach (var item in dict)
                _keyValue.Set(item.Key, item.Value.ToString());

            return this;
        }

        /// <summary>
        /// Adds the specified parameters to the query string
        /// </summary>
        /// <param name="dict">Parameter dictionary</param>
        /// <returns>Returns the query builder</returns>
        public RequestBuilder WithParameters(Dictionary<string, string> dict)
        {
            if (ReferenceEquals(null, dict))
                return this;

            foreach (var item in dict)
                _keyValue.Set(item.Key, item.Value);

            return this;
        }

        /// <summary>
        /// Adds a new parameter to the query string or replaces the current one with the specified key
        /// </summary>
        /// <typeparam name="T">The type of the parameter that will be converted to a string</typeparam>
        /// <param name="key">Key</param>
        /// <param name="value">Value</param>
        /// <returns>Returns the query builder</returns>
        public RequestBuilder WithParameter<T>(string key, T value)
            where T : struct
        {
            _keyValue.Set(key, value.ToString());
            return this;
        }

        /// <summary>
        /// Adds a new parameter to the query string or replaces the current one with the specified key
        /// </summary>
        /// <param name="key">Key</param>
        /// <param name="value">Value</param>
        /// <returns>Returns the query builder</returns>
        public RequestBuilder WithParameter(string key, string value)
        {
            if (ReferenceEquals(null, value))
                return this;
            _keyValue.Set(key, value);
            return this;
        }

        /// <summary>
        /// Adds a new request property or replaces the current one with the specified key
        /// </summary>
        /// <param name="key">Key</param>
        /// <param name="value">Value</param>
        /// <returns>Returns the query builder</returns>
        public RequestBuilder WithProperty(string key, object value)
        {
            Message.Properties.Add(key, value);
            return this;
        }

        /// <summary>
        /// Adds content to the request
        /// </summary>
        /// <param name="content">Content</param>
        /// <returns>Returns the query builder</returns>
        public RequestBuilder WithContent(HttpContent content)
        {
            Message.Content = content;
            return this;
        }

        /// <summary>
        /// Adds an object, converting it to Json, according to the specified serialization and encoding settings
        /// </summary>
        /// <param name="content">Content</param>
        /// <param name="jsonSerializerSettings">Settings</param>
        /// <param name="encoding">Encoding, default UTF-8</param>
        /// <returns>Returns the query builder</returns>
        public RequestBuilder WithJsonContent(object content, JsonSerializerSettings jsonSerializerSettings, Encoding encoding = null)
        {
            var stringContent = JsonConvert.SerializeObject(content, jsonSerializerSettings);
            Message.Content = new StringContent(stringContent, encoding ?? Encoding.UTF8, "application/json");
            return this;
        }

        /// <summary>
        /// Adds Form-Data content to the request using the specified byte array and content and file names
        /// </summary>
        /// <returns>Returns the query builder</returns>
        public RequestBuilder WithFormDataContent(byte[] content, string name, string filename)
        {
            var requestContent = new MultipartFormDataContent();
            var byteContent = new ByteArrayContent(content);
            var splited = filename.Split('.');
            var contentType = MimeTypeMap.GetMimeType(splited.LastOrDefault());
            byteContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
            byteContent.Headers.ContentLength = content.LongLength;
            requestContent.Add(byteContent, $"\"{name}\"", filename);
            Message.Content = requestContent;
            return this;
        }

        /// <summary>
        /// Adds Form-Data content to the request using the specified files
        /// </summary>
        /// <param name="files">Files</param>
        /// <returns>Returns the query builder</returns>
        public RequestBuilder WithFormDataContent(params FileContent[] files)
        {
            var requestContent = new MultipartFormDataContent();

            for (int i = 0; i < files.Length; i++)
            {
                var file = files[i];

                var byteContent = new ByteArrayContent(file.Content);
                var splited = file.Filename.Split('.');
                var contentType = MimeTypeMap.GetMimeType(splited.LastOrDefault());
                byteContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
                byteContent.Headers.ContentLength = file.Content.LongLength;

                requestContent.Add(byteContent, $"\"{file.Name}\"", file.Filename);
            }

            Message.Content = requestContent;
            return this;
        }

        /// <summary>
        /// Adds Form-Data content to the request using the specified files
        /// </summary>
        /// <param name="files">Files</param>
        /// <returns>Returns the query builder</returns>
        public RequestBuilder WithFormDataContent(params HttpContent[] files)
        {
            var requestContent = new MultipartFormDataContent();

            for (int i = 0; i < files.Length; i++)
                requestContent.Add(files[i]);

            Message.Content = requestContent;
            return this;
        }

        /// <summary>
        /// Adds Form-Data content to the request using the specified Contents
        /// </summary>
        /// <param name="contents">Contents</param>
        /// <returns>Returns the query builder</returns>
        public RequestBuilder WithFormDataContent(params (HttpContent Content, string Name)[] contents)
        {
            var requestContent = new MultipartFormDataContent();

            for (int i = 0; i < contents.Length; i++)
                requestContent.Add(contents[i].Content, contents[i].Name);

            Message.Content = requestContent;
            return this;
        }

        /// <summary>
        /// Adds a new request header or replaces the current one with the specified key
        /// </summary>
        /// <param name="key">Key</param>
        /// <param name="value">Value</param>
        /// <returns>Returns the query builder</returns>
        public RequestBuilder WithHeader(string key, string value)
        {
            if (Message.Headers.Contains(key))
                Message.Headers.Remove(key);

            Message.Headers.Add(key, value);
            return this;
        }

        /// <summary>
        /// Adds a new request header if it hasn't been added yet
        /// </summary>
        /// <param name="key">Key</param>
        /// <param name="value">Value</param>
        /// <returns>Returns the query builder</returns>
        public RequestBuilder AddHeaderIfNotAdded(string key, string value)
        {
            if (Message.Headers.Contains(key))
                return this;

            Message.Headers.Add(key, value);
            return this;
        }

        /// <summary>
        /// Adds a new request header if it hasn't been added yet
        /// </summary>
        /// <param name="key">Key</param>
        /// <param name="values">Values</param>
        /// <returns>Returns the query builder</returns>
        public RequestBuilder WithHeader(string key, IEnumerable<string> values)
        {
            Message.Headers.Add(key, values);
            return this;
        }

        /// <summary>
        /// Adds a new request header without performing any checks
        /// </summary>
        /// <param name="key">Key</param>
        /// <param name="value">Value</param>
        /// <returns>Returns the query builder</returns>
        public RequestBuilder WithHeaderWithoutValidation(string key, string value)
        {
            Message.Headers.TryAddWithoutValidation(key, value);
            return this;
        }

        /// <summary>
        /// Adds a new request header without performing any checks
        /// </summary>
        /// <param name="key">Key</param>
        /// <param name="values">Values</param>
        /// <returns>Returns the query builder</returns>
        public RequestBuilder WithHeaderWithoutValidation(string key, IEnumerable<string> values)
        {
            Message.Headers.TryAddWithoutValidation(key, values);
            return this;
        }

        /// <summary>
        /// Sets the request type to POST
        /// </summary>
        /// <returns>Returns the query builder</returns>
        public RequestBuilder Post()
        {
            Message.Method = HttpMethod.Post;
            return this;
        }

        /// <summary>
        /// Sets the request type to GET
        /// </summary>
        /// <returns>Возвращает строитель запроса</returns>
        public RequestBuilder Get()
        {
            Message.Method = HttpMethod.Get;
            return this;
        }

        /// <summary>
        /// Sets the request type to PUT
        /// </summary>
        /// <returns>Returns the query builder</returns>
        public RequestBuilder Put()
        {
            Message.Method = HttpMethod.Put;
            return this;
        }

        /// <summary>
        /// Sets the request type to TRACE
        /// </summary>
        /// <returns>Returns the query builder</returns>
        public RequestBuilder Trace()
        {
            Message.Method = HttpMethod.Trace;
            return this;
        }

        /// <summary>
        /// Sets the request type to DELETE
        /// </summary>
        /// <returns>Returns the query builder</returns>
        public RequestBuilder Delete()
        {
            Message.Method = HttpMethod.Delete;
            return this;
        }

        /// <summary>
        /// Sets the request type to HEAD
        /// </summary>
        /// <returns>Returns the query builder</returns>
        public RequestBuilder Head()
        {
            Message.Method = HttpMethod.Head;
            return this;
        }

        /// <summary>
        /// Sets the request type to OPTIONS
        /// </summary>
        /// <returns>Returns the query builder</returns>
        public RequestBuilder Options()
        {
            Message.Method = HttpMethod.Options;
            return this;
        }

        /// <summary>
        /// Sends the request built by the current builder
        /// </summary>
        /// <param name="option">Request processing options</param>
        /// <returns>Returns the first result handler</returns>
        public RequestHandler<HttpResponseMessage> Send(HttpCompletionOption option = HttpCompletionOption.ResponseContentRead)
        {
            return Send(CancellationToken.None, option);
        }

        /// <summary>
        /// Sends the request built by the current builder
        /// </summary>
        /// <param name="token">Request cancellation token</param>
        /// <param name="option">Request processing options</param>
        /// <returns>Returns the first result handler</returns>
        public RequestHandler<HttpResponseMessage> Send(CancellationToken token, HttpCompletionOption option = HttpCompletionOption.ResponseContentRead)
        {
            Message.RequestUri = Uri;

            Observer.OnRequestCreated(this);

            if (!_beforeSend.IsNullOrEmpty())
                for (int i = 0; i < _beforeSend.Count; i++)
                    _beforeSend[i](this);

            RequestId = _requestsCounter++;
            _currentRequestsCounter++;

            var handler = new RequestHandler<HttpResponseMessage>(this, Observer, Observer.TransformSendTask(this, Message, () => SendRequest(token, option).WithBlockingCancellation(token)));

            Observer.OnRequestStarted(handler);

            if (!_afterSend.IsNullOrEmpty())
                for (int i = 0; i < _afterSend.Count; i++)
                    _afterSend[i](this);

            handler.Task.ContinueWith(t =>
            {
                _currentRequestsCounter--;
                Observer.OnRequestFinished(handler);
            }).NoWarning();

            return handler;
        }

        private async Task<HttpResponseMessage> SendRequest(CancellationToken token, HttpCompletionOption option)
        {
            try
            {
                if (Message.Method == HttpMethod.Post)
                {
                    var content = Message.Content ?? new StringContent("");

                    foreach (var item in Message.Headers)
                    {
                        content.Headers.Remove(item.Key);
                        content.Headers.Add(item.Key, item.Value);
                    }

                    return await _client.PostAsync(Message.RequestUri, content, token).ConfigureAwait(false);
                }
                return await _client.SendAsync(Message, option, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new NoConnectionException(Observer.BuildNoConnectionExceptionMessage(this, ex), ex);
            }
        }
    }
}

