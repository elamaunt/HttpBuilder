using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace HttpBuilder
{
    /// <summary>
    /// Delegate type for handling file upload progress
    /// </summary>
    public delegate void ProgressHandler(long totalBytes, long readedBytes, float percentage);
    public static class RequestHandlerExtentionMethods
    {
        /// <summary>
        /// Adds validation of the Http request result for code 200. If the code is different, an exception will be thrown
        /// </summary>
        /// <param name="self">Reference to the current handler</param>
        /// <returns>Returns the transformed query result handler</returns>
        public static RequestHandler<HttpResponseMessage> ValidateSuccessStatusCode(this RequestHandler<HttpResponseMessage> self)
        {
            return self.Validate(res =>
            {
                try
                {
                    res.EnsureSuccessStatusCode();
                }
                catch (HttpRequestException ex)
                {
                    throw new HttpException(self.Observer.BuildHttpExceptionMessage(self.Builder, res, ex), self.Builder, res, ex);
                }
            });
        }

        /// <summary>
        /// Creates a result handler with intermediate conversion of the request to Json
        /// </summary>
        /// <typeparam name="B">Required type</typeparam>
        /// <param name="self">Reference to the current request handler</param>
        /// <returns>Returns the transformed requests result handler</returns>
        public static RequestHandler<B> Json<B>(this RequestHandler<string> self)
        {
            return self.Continue(JsonConvert.DeserializeObject<B>);
        }

        /// <summary>
        /// Creates a result handler with intermediate conversion of the request result to Json
        /// </summary>
        /// <typeparam name="B">Required type</typeparam>
        /// <param name="self">Reference to the current request handler</param>
        /// <param name="serializer">Setting up serialization</param>
        /// <returns>Returns the transformed requests result handler</returns>
        public static RequestHandler<B> Json<B>(this RequestHandler<HttpResponseMessage> self, JsonSerializer serializer)
        {
            return self.Continue(mes => mes.Content.ReadAsStreamAsync())
                .Continue(stream =>
                {
                    using (var sr = new StreamReader(stream))
                    {
                        using (var jsonTextReader = new JsonTextReader(sr))
                        {
                            return serializer.Deserialize<B>(jsonTextReader);
                        }
                    }
                });
        }

        /// <summary>
        /// Creates a result handler with intermediate content extraction from the response message
        /// </summary>
        /// <param name="self">Reference to the current request handler</param>
        /// <returns>Returns the transformed requests result handler</returns>
        public static RequestHandler<HttpContent> Content(this RequestHandler<HttpResponseMessage> self)
        {
            return self.Continue(mes => mes.Content);
        }

        /// <summary>
        /// Creates a result handler with intermediate content extraction as a string from the response message
        /// </summary>
        /// <param name="self">Reference to the current request handler</param>
        /// <returns>Returns the transformed requests result handler</returns>
        public static RequestHandler<string> String(this RequestHandler<HttpContent> self)
        {
            return self.Continue(content => content.ReadAsStringAsync());
        }

        /// <summary>
        /// Creates a result handler with intermediate content extraction as a data stream from the response message
        /// </summary>
        /// <param name="self">Reference to the current request handler</param>
        /// <returns>Returns the transformed requests result handler</returns>
        public static RequestHandler<Stream> Stream(this RequestHandler<HttpContent> self)
        {
            return self.Continue(content => content.ReadAsStreamAsync());
        }

        /// <summary>
        /// Creates a result handler with intermediate content extraction as an array of bytes from the response message
        /// </summary>
        /// <param name="self">Reference to the current request handler</param>
        /// <returns>Returns the transformed requests result handler</returns>
        public static RequestHandler<byte[]> ByteArray(this RequestHandler<HttpContent> self)
        {
            return self.Continue(content => content.ReadAsByteArrayAsync());
        }

        /// <summary>
        /// Creates a result handler that will intermediate load the request content into the specified data stream.
        /// Allows you to track the download of a file.
        /// </summary>
        /// <param name="self">Reference to the current request handler</param>
        /// <param name="token">Cancellation token</param>
        /// <param name="destinationStream">Target stream to save the content file</param>
        /// <param name="bufferSize">Buffer size when loading</param>
        /// <param name="handler">Delegate - Load Handler</param>
        /// <param name="readsOfOneUpdate">Content stream reads before invoking the update delegate</param>
        /// <returns>Returns the transformed requests result handler</returns>
        public static RequestHandler<Stream> ProcessLoading(this RequestHandler<HttpContent> self, CancellationToken token, Stream destinationStream, long bufferSize, ProgressHandler handler, int readsOfOneUpdate = 100)
        {
            return self.Continue(async content =>
            {
                token.ThrowIfCancellationRequested();

                var stream = await content.ReadAsStreamAsync();

                var totalDownloadSize = content.Headers.ContentLength;
                var totalBytesRead = 0L;
                var readCount = 0L;
                var buffer = new byte[bufferSize];
                var isMoreToRead = true;

                do
                {
                    token.ThrowIfCancellationRequested();

                    var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

                    if (bytesRead == 0)
                    {
                        isMoreToRead = false;
                        handler(totalDownloadSize.GetValueOrDefault(), totalBytesRead, 1f);
                        continue;
                    }

                    await destinationStream.WriteAsync(buffer, 0, bytesRead);

                    totalBytesRead += bytesRead;
                    readCount += 1;

                    if (readCount % readsOfOneUpdate == 0)
                    {
                        var sizeValue = totalDownloadSize.GetValueOrDefault();

                        if (sizeValue == 0)
                            handler(sizeValue, totalBytesRead, 0f);
                        else
                            handler(sizeValue, totalBytesRead, (float)Math.Round((double)totalBytesRead / sizeValue, 2));
                    }
                }
                while (isMoreToRead);

                token.ThrowIfCancellationRequested();

                return destinationStream;
            });
        }

        /// <summary>
		/// Return true, if enumerable is null or empty
		/// </summary>
		/// <param name="self">Self reference</param>
		/// <returns>Bool</returns>
		internal static bool IsNullOrEmpty(this IEnumerable self)
        {
            return self == null || !self.Cast<object>().Any();
        }

        /// <summary>
        /// Return true, if collection is null or empty
        /// </summary>
        /// <param name="self">Self reference</param>
        /// <returns>Bool</returns>
        internal static bool IsNullOrEmpty(this ICollection self)
        {
            return self == null || self.Count == 0;
        }

        /// <summary>
        /// Return true, if string is null or empty
        /// </summary>
        /// <param name="self">Self reference</param>
        /// <returns>Bool</returns>
        internal static bool IsNullOrEmpty(this string self)
        {
            return string.IsNullOrEmpty(self);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void NoWarning(this Task task) { /* nothing */ }

        /// <summary>
		/// Return true, if string is null or contains only whitespaces
		/// </summary>
		/// <param name="self">Self reference</param>
		/// <returns>Bool</returns>
		internal static bool IsNullOrWhiteSpace(this string self)
        {
            return string.IsNullOrWhiteSpace(self);
        }


        /// <summary>
        /// Converts task to "Task without Continue" if operation has been cancelled
        /// </summary>
        /// <param name="originalTask">Original unsafe task</param>
        /// <param name="ct">Token</param>
        /// <returns>Task</returns>
        public static async Task<TResult> WithBlockingCancellation<TResult>(this Task<TResult> originalTask, CancellationToken ct)
        {
            var blockingTask = new TaskCompletionSource<TResult>();
            var cancelTask = new TaskCompletionSource<Void>();

            using (ct.Register(t => ((TaskCompletionSource<Void>)t).TrySetResult(new Void()), cancelTask))
            {
                Task any = await Task.WhenAny(originalTask, cancelTask.Task);
                if (any != cancelTask.Task && !originalTask.IsCanceled)
                {
                    if (originalTask.IsFaulted)
                        blockingTask.SetException(originalTask.Exception);
                    else
                        blockingTask.SetResult(originalTask.Result);
                }
            }

            return await blockingTask.Task;
        }

        /// <summary>
        /// Converts task to "Task without Continue" if operation has been cancelled
        /// </summary>
        /// <param name="originalTask">Original unsafe task</param>
        /// <param name="ct">Token</param>
        /// <returns>Task</returns>
        public static async Task WithBlockingCancellation(this Task originalTask, CancellationToken ct)
        {
            var blockingTask = new TaskCompletionSource<Void>();
            var cancelTask = new TaskCompletionSource<Void>();

            using (ct.Register(t => ((TaskCompletionSource<Void>)t).TrySetResult(new Void()), cancelTask))
            {
                Task any = await Task.WhenAny(originalTask, cancelTask.Task);
                if (any != cancelTask.Task && !originalTask.IsCanceled)
                {
                    if (originalTask.IsFaulted)
                        blockingTask.SetException(originalTask.Exception);
                    else
                        blockingTask.SetResult(new Void());
                }
            }

            await blockingTask.Task;
        }

        public struct Void
        {

        }
    }
}
