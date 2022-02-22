using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Unity.Reflect;
using Unity.Reflect.Actors;
using UnityEngine.Networking;

namespace UnityEngine.Reflect
{
    public class ReflectRequestHandler: IRestHttpClient
    {
        readonly MainThreadIODispatcherActor m_Dispatcher;
        readonly TaskScheduler m_Scheduler;
        
        public ReflectRequestHandler(MainThreadIODispatcherActor dispatcher = null)
        {
            // UnityWebRequest must run on the main unity thread
            m_Scheduler = dispatcher != null ? TaskScheduler.Default : TaskScheduler.FromCurrentSynchronizationContext();
            m_Dispatcher = dispatcher;
        }
        
        public async Task<HttpResponseMessage> RequestAsync(HttpRequestMessage httpRequestMessage, CancellationToken cancellationToken)
        {
            var factoryTask = await Task.Factory.StartNew(
                async () => await RequestAsyncInternal(httpRequestMessage, cancellationToken: cancellationToken),
                cancellationToken,
                TaskCreationOptions.DenyChildAttach,
                m_Scheduler);

            var response = await factoryTask;
            
            return response;
        }

        public async Task<HttpResponseMessage> DownloadFileToDisk(HttpRequestMessage httpRequestMessage, string path, CancellationToken cancellationToken)
        {
            var factoryTask = await Task.Factory.StartNew(
                async () => await RequestAsyncInternal(httpRequestMessage, path, cancellationToken),
                cancellationToken,
                TaskCreationOptions.DenyChildAttach,
                m_Scheduler);

            var response = await factoryTask;
            
            return response;
        }
        
        async Task<HttpResponseMessage> RequestAsyncInternal(HttpRequestMessage httpRequestMessage, string downloadFilePath = null, CancellationToken cancellationToken = default)
        {
            var stringContent = "{}";
            if (httpRequestMessage.Content is StringContent)
                stringContent = await httpRequestMessage.Content.ReadAsStringAsync();
            
            var state = new RequestState(httpRequestMessage, stringContent, downloadFilePath, null, null);

            if (m_Dispatcher != null)
            {
                await m_Dispatcher.Run<object>(onCompleted => PrepareAndStartRequest(state, onCompleted, cancellationToken)).ConfigureAwait(false);
                await m_Dispatcher.Run<object>(onCompleted => CompleteRequest(state, onCompleted)).ConfigureAwait(false);
            }
            else
            {
                var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
                PrepareAndStartRequest(state, res => tcs.TrySetResult(res), cancellationToken);
                await tcs.Task;
                CompleteRequest(state, res => { });
            }

            return state.Response;
        }

        static void PrepareAndStartRequest(RequestState state, Action<object> onCompleted, CancellationToken cancellationToken)
        {
            var httpRequestMessage = state.HttpRequestMessage;
            var stringContent = state.StringContent;
            var downloadFilePath = state.DownloadFilePath;

            var request = httpRequestMessage.Method.ToString() switch
            {
                UnityWebRequest.kHttpVerbGET => UnityWebRequest.Get(httpRequestMessage.RequestUri),
                // NOTE: For POST, create a PUT request, then override the verb, see https://manuelotheo.com/uploading-raw-json-data-through-unitywebrequest/
                UnityWebRequest.kHttpVerbPOST => UnityWebRequest.Put(httpRequestMessage.RequestUri, stringContent),
                UnityWebRequest.kHttpVerbPUT => UnityWebRequest.Put(httpRequestMessage.RequestUri, stringContent),
                UnityWebRequest.kHttpVerbDELETE => UnityWebRequest.Delete(httpRequestMessage.RequestUri),
                _ => throw new NotImplementedException()
            };

            state.Request = request;

            if (httpRequestMessage.Method == HttpMethod.Post)
            {
                // Override the put
                request.method = UnityWebRequest.kHttpVerbPOST;
            }
            
            foreach (var header in httpRequestMessage.Headers)
            {
                var value = string.Join(",", header.Value);
                request.SetRequestHeader(header.Key, value);
            }
            
            if (httpRequestMessage.Content != null)
            {
                foreach (var header in httpRequestMessage.Content.Headers)
                {
                    var value = string.Join(",", header.Value);
                    request.SetRequestHeader(header.Key, value);
                }
            }

            var isDownload = !string.IsNullOrEmpty(downloadFilePath);
            
            if (isDownload)
                request.downloadHandler = new DownloadHandlerFile(downloadFilePath);

            state.CancellationTokenRegistration = cancellationToken.Register(() =>
            {
               request.Abort();
            });            
            
            var asyncOp = request.SendWebRequest();
            asyncOp.completed += obj => { onCompleted(null); };
        }

        static void CompleteRequest(RequestState state, Action<object> onCompleted)
        {
            state.CancellationTokenRegistration?.Dispose();
            
            var request = state.Request;
            var httpRequestMessage = state.HttpRequestMessage;
            var isDownload = !string.IsNullOrEmpty(state.DownloadFilePath);

            if (request.result == UnityWebRequest.Result.ConnectionError)
            {
                var requestException = new HttpRequestException();
                throw requestException;
            }
            
            var response = new HttpResponseMessage();
            response.RequestMessage = httpRequestMessage;

            if (!isDownload)
            {
                // Parse response message
                if (request.GetResponseHeader("Content-Type") == "application/octet-stream")
                    response.Content = new ByteArrayContent(request.downloadHandler?.data ?? new byte[0]);
                else
                    response.Content = new StringContent(GetResponseTextContent(request));
            }
            
            response.StatusCode = (HttpStatusCode) request.responseCode;

            state.Response = response;
            onCompleted(null);
        }
        
        static string GetResponseTextContent(UnityWebRequest request)
        {
            try
            {
                return request.downloadHandler?.text ?? string.Empty;
            }
            catch (NotSupportedException)
            {
                // Some download handlers don't have string accessors
                return string.Empty;
            }
        }

        public string Serialize(object content)
        {
            return JsonUtility.ToJson(content);
        }

        public T Deserialize<T>(string content)
        {
            return JsonUtility.FromJson<T>(content);
        }

        class RequestState
        {
            public HttpRequestMessage HttpRequestMessage;
            public string StringContent;
            public string DownloadFilePath;
            public UnityWebRequest Request;
            public HttpResponseMessage Response;
            public IDisposable CancellationTokenRegistration;

            public RequestState(HttpRequestMessage httpRequestMessage, string stringContent, string downloadFilePath, UnityWebRequest request, HttpResponseMessage response)
            {
                HttpRequestMessage = httpRequestMessage;
                StringContent = stringContent;
                DownloadFilePath = downloadFilePath;
                Request = request;
                Response = response;
            }
        }
    }
}
