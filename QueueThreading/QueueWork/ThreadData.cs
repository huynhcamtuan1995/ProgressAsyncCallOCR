using System;
using System.Threading;

namespace AzuseOcrAsyncService
{
    /// <summary>
    /// Thread data in progress
    /// </summary>
    public class ThreadModel
    {
        public string Name { get; set; }
        public int Number { get; set; }
        public DateTime CreateAt { get; set; } = DateTime.Now;
        public string OperationId { get; set; }
        public ThreadResponse Response { get; set; }

        public bool IsExpire()
        {
            return CreateAt.AddSeconds(30) <= DateTime.Now;
        }
        public AutoResetEvent Event = new AutoResetEvent(false);
    }

    /// <summary>
    /// Thread result Response
    /// </summary>
    public class ThreadResponse
    {
        public int Status { get; set; }
        public string Message { get; set; }
        public object Data { get; set; }
    }
}
