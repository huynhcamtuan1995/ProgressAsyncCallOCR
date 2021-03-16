using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace AzuseOcrAsyncService.Controllers
{
    [ApiController]
    [Route("[controller]/[action]")]
    public class AzuseOcrAsyncController : ControllerBase
    {


        private readonly ILogger<AzuseOcrAsyncController> _logger;

        public AzuseOcrAsyncController(ILogger<AzuseOcrAsyncController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        public async Task<ThreadResponse> SendSingleRequestAsync()
        {
            ThreadModel model = new ThreadModel();
            string name = $"{Guid.NewGuid().ToString("N")}_{DateTime.Now.ToString("HHmmss")}";
            model.Name = name;

            //if cannot add request -> response bad request
            if (!QueueThread.AddThreadRequest(model))
            {
                ThreadResponse response = new ThreadResponse();
                response.Status = 400;
                response.Message = "Bad Request";
                return response;
            }

            model.Event.WaitOne();

            return model.Response;
        }
    }
}
