using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Timers;
using Timer = System.Timers.Timer;

namespace AzuseOcrAsyncService
{
    public class QueueThread
    {
        private static ConcurrentDictionary<string, ThreadModel> ConcurrentDictionary = new ConcurrentDictionary<string, ThreadModel>();
        private static ConcurrentQueue<string> ConcurrentQueue = new ConcurrentQueue<string>();
        private static Semaphore Semaphore = new Semaphore(10, 10, "MySemaphore");
        private static Semaphore GetOcrResultSemaphore = new Semaphore(10, 10, "GetOcrResult");
        private static AutoResetEvent QueueEvent = new AutoResetEvent(false);

        internal static Timer TimerExpired;
        internal static Timer TimerOcrResult;

        static QueueThread()
        {
            TimerRun();
            TimerGetOcrResultRun();
            new Thread(() => StartThreads()).Start();
        }

        /// <summary>
        /// Timer to run every 5s, to progress expired request
        /// </summary>
        public static void TimerRun()
        {
            TimerExpired = new Timer(1000 * 5);
            TimerExpired.Elapsed += ProcessOnRemoveExpired;
            TimerExpired.AutoReset = true;
            TimerExpired.Start();
        }

        public static void TimerGetOcrResultRun()
        {
            TimerOcrResult = new Timer(1000 * 5);
            TimerOcrResult.Elapsed += ProcessOnGetOcrResult;
            TimerOcrResult.AutoReset = true;
            TimerOcrResult.Start();
        }

        private static void ProcessOnGetOcrResult(object source, ElapsedEventArgs e)
        {
            //get all expired request in Dictionary store
            TimerOcrResult.Stop();

            try
            {
                Next:
                //select top in list (request or db) to process
                //where some condition

                List<ThreadModel> listRequests = ConcurrentDictionary.Values
                    .Where(x => !string.IsNullOrEmpty(x.OperationId))
                    .OrderBy(x => x.CreateAt)
                    .ToList();

                if (listRequests.Count == 0)
                {
                    goto End;
                }

                foreach (ThreadModel lead in listRequests)
                {
                    // Set Stage is Inprogress
                    //UpdateState(lead, LeadStateEnum.Inprogress);

                    // Split Thread
                    Thread thread = CreateThread(
                        GetOcrResultSemaphore,
                        () =>
                        {
                            ProgressOcrResultAsync(lead);
                        });
                    thread.Start();
                }
                //repeat 
                goto Next;

                End:
                return;
            }
            catch (Exception exception)
            {
                //log ex
            }
            finally
            {
                TimerOcrResult.Start();
            }
        }

        /// <summary>
        /// Remove expired request
        /// </summary>
        /// <param name="source"></param>
        /// <param name="e"></param>
        private static void ProcessOnRemoveExpired(object source, ElapsedEventArgs e)
        {
            //get all expired request in Dictionary store
            List<string> expiredModels = ConcurrentDictionary.Values
                .Where(x => x.IsExpire()
                    && string.IsNullOrEmpty(x.OperationId))
                .OrderBy(x => x.CreateAt)
                .Select(x => x.Name)
                .ToList();

            foreach (string modelKey in expiredModels)
            {
                if (ConcurrentDictionary.TryRemove(modelKey, out ThreadModel model))
                {
                    //set value for expire item
                    if (model.Response == null)
                    {
                        //reponse stats reponse to timeout or somethign...
                        //then set request event to continutes reponse 
                        ThreadResponse response = new ThreadResponse();
                        response.Status = 408;
                        response.Message = "Timeout";
                        model.Response = response;
                    }
                    model.Event.Set();
                }
            }
        }

        /// <summary>
        /// isolate thread to running thread
        /// </summary>
        public static void StartThreads()
        {
            while (true)
            {
                if (ConcurrentQueue.IsEmpty)
                {
                    //make this thread sleep while request in queue empty
                    QueueEvent.WaitOne();
                }

                //dequeue from queue and progress 
                if (ConcurrentQueue.TryDequeue(out string modelName)
                    && ConcurrentDictionary.TryGetValue(modelName, out ThreadModel model))
                {
                    Thread t = CreateThread(Semaphore,
                        () => ProgressAsync(model));

                    t.Start();
                }
            }

        }

        /// <summary>
        /// adding new request to queue
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public static bool AddThreadRequest(ThreadModel model)
        {
            if (!ConcurrentDictionary.TryAdd(model.Name, model))
            {
                return false;
            }

            ConcurrentQueue.Enqueue(model.Name);
            QueueEvent.Set();

            return true;
        }

        /// <summary>
        ///  managed max thread to progress by semaphore
        /// </summary>
        /// <param name="semaphore"></param>
        /// <param name="threadStart"></param>
        /// <returns></returns>
        public static Thread CreateThread(Semaphore semaphore, ThreadStart threadStart)
        {
            semaphore.WaitOne();
            return new Thread(threadStart);
        }

        /// <summary>
        /// function process request
        /// </summary>
        /// <param name="model"></param>
        private static void ProgressAsync(ThreadModel model)
        {
            try
            {
                string operationId = string.Empty;

                ////Call Read Ocr result
                //var client = new HttpClient();
                //var queryString = HttpUtility.ParseQueryString(string.Empty);

                //// Request headers
                //client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", "{subscription key}");

                //// Request parameters
                //queryString["language"] = "{string}";
                //var uri = "https://westcentralus.api.cognitive.microsoft.com/vision/v3.1/read/analyze?" + queryString;

                //HttpResponseMessage response;

                //// Request body
                //byte[] byteData = Encoding.UTF8.GetBytes("{body}");

                //using (var content = new ByteArrayContent(byteData))
                //{
                //    content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                //    response = await client.PostAsync(uri, content);
                //}

                //if (response.StatusCode == HttpStatusCode.Accepted
                //    && response.Headers.TryGetValues("Operation-Location", out var headerValues))
                //{
                //    operationId = headerValues.ToArray()[0];
                //}

                //Then update state response
                model.OperationId = operationId;
                ConcurrentDictionary[model.Name] = model;
                //Common.WriteLog(model);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error {model.Number}-------------------- {ex.Message}");
                //Log ex
            }
            finally
            {
                int availableThreads = Semaphore.Release();
                Console.WriteLine($"                        ---> AvailableThreads:{availableThreads} || Queue:{ConcurrentQueue.Count()} || Dictiondary:{ConcurrentDictionary.Count()}");
            }
        }

        private static void ProgressOcrResultAsync(ThreadModel model)
        {
            try
            {
                object ocrResult = null;
                ////Call Get OCR result
                //var client = new HttpClient();
                //var queryString = HttpUtility.ParseQueryString(string.Empty);

                //// Request headers
                //client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", "{subscription key}");

                //var uri = "https://westcentralus.api.cognitive.microsoft.com/vision/v3.1/read/analyzeResults/{operationId}?" + queryString;

                //ocrResult = await client.GetAsync(uri);

                model.Response.Status = (int)HttpStatusCode.OK;
                model.Response.Message = "Successed";
                model.Response.Data = ocrResult;
                model.Event.Set();
                //Common.WriteLog(model);
            }
            catch (Exception ex)
            {
                //log Ex
            }
            finally
            {
                int availableThreads = Semaphore.Release();
            }
        }
    }
}
