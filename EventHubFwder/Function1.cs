using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using LogAnalyticsAPIHandler;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.ServiceBus;
using Microsoft.ServiceBus.Messaging;

namespace EventHubFwder
{
    public static class Function1
    {
        //[FunctionName("Function1")]
        //public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)]HttpRequestMessage req, TraceWriter log)
        //{
        //    log.Info("C# HTTP trigger function processed a request.");

        //    // parse query parameter
        //    string name = req.GetQueryNameValuePairs()
        //        .FirstOrDefault(q => string.Compare(q.Key, "name", true) == 0)
        //        .Value;

        //    // Get request body
        //    dynamic data = await req.Content.ReadAsAsync<object>();

        //    // Set name to query string or body data
        //    name = name ?? data?.name;

        //    return name == null
        //        ? req.CreateResponse(HttpStatusCode.BadRequest, "Please pass a name on the query string or in the request body")
        //        : req.CreateResponse(HttpStatusCode.OK, "Hello " + name);
        //}

        [FunctionName("EventHubTrigger")]
        //public static void Run([ServiceBusTrigger("myqueue", AccessRights.Manage, Connection = "ServiceBusConnection")]string myQueueItem, TraceWriter log)
        public static void Run([EventHubTrigger("samples-workitems", Connection = "EventHubConnectionAppSetting")] EventData myEventHubMessage, TraceWriter log)

        {
            log.Info($"{Encoding.UTF8.GetString(myEventHubMessage.GetBytes())}");

            string customerId = "xxxxxxxx-xxx-xxx-xxx-xxxxxxxxxxxx";
            string sharedKey = "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxx";
            string logType = "DemoExample";
            LogAnalyticsAPIConfig config1 = new LogAnalyticsAPIConfig(customerId, sharedKey, logType);

            string sampleJson = @"[{""DemoField1"":""DemoValue1"",""DemoField2"":""DemoValue2""},{""DemoField3"":""DemoValue3"",""DemoField4"":""DemoValue4""}]";

            config1.SendData(sampleJson);
        }


        //public static class EventHubExporter
        //{
        //    static HttpClient Client { get; } = new HttpClient();

        //    [FunctionName("EventHubExporter")]
        //    public static async Task RunAsync(
        //        [EventHubTrigger("wadeventhub", Connection = "EventHubConnection")]string[] eventHubMessages, 
        //        TraceWriter log)
        //    {
        //        log.Info($"C# Event Hub trigger function processed a message: {eventHubMessages}");

        //        var result = await Client.PostAsync(Util.GetSettings("LogzioUri"), new StringContent(string.Join("\n", eventHubMessages)));

        //        log.Info(result.StatusCode.ToString());
        //    }

        //} 
    }
}
