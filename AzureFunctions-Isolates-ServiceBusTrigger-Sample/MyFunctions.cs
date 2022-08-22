using System;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace AzureFunctions_Isolates_ServiceBusTrigger_Sample
{
    /// <summary>
    /// Azure Functions 測試用專案
    /// </summary>
    public class MyFunctions
    {
        private readonly ILogger _logger;

        public MyFunctions(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<MyFunctions>();
        }

        /// <summary>
        /// ServiceBus Trigger 測試用 Function
        /// </summary>
        /// <param name="myQueueItem">My queue item.</param>
        [Function("ServiceBusTriggerSample")]
        public async Task Run(
            [ServiceBusTrigger(
                queueName: "%QueueName%", // local.settings.json > Values > QueueName
                Connection = "ServiceBus")] // local.settings.json > ConnectionStrings > ServiceBus
            string myQueueItem)
        {
            _logger.LogInformation("開始處理訊息: {myQueueItem}", myQueueItem);

            await Task.Delay(new TimeSpan(0, 8, 0));
            
            _logger.LogInformation("結束處理訊息: {myQueueItem}", myQueueItem);
        }
    }
}
