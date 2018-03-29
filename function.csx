#r "Microsoft.ServiceBus"
#r "Newtonsoft.Json"

using System.Text;
using Microsoft.ServiceBus.Messaging;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Newtonsoft.Json;

static string customerId = "8516a918-4e0f-47dd-a3e2-01a9a95102b1";
static string sharedKey = "HaAZ7I4PZgo6uN7fiAUjkwJpBdK61hj8Zi7LI/s5uIzU6UI3J74SeycB45gvXvr8dWlL1Q8xDgpbzUnw0OYNrg==";
static string LogName = "PerformanceCounter";
static string TimeStampField = "";
static System.Net.Http.HttpClient client = new System.Net.Http.HttpClient();

public static async Task Run(EventData myEventHubMessage, TraceWriter log)
{
    //log.Info($"{Encoding.UTF8.GetString(myEventHubMessage.GetBytes())}");

    //Output format: 
    //{"records":[{"time":"2018-03-28T18:35:30.4400803Z","dimensions":{"DeploymentId":"3dc30ede-b110-4b63-8cbe-d203f60ecf75","Role":"IaaS","RoleInstance":"_Type1_3"},
    //  "metricName":"\\Service Fabric Service(b942e303-0cc5-492e-859f-9cdffec1b68e_131666626300567800_9273126912)\\Average milliseconds per request","last":0.0,"timeGrain":"PT60S"}]}
  
    var input = Encoding.UTF8.GetString(myEventHubMessage.GetBytes());
    
    try

    {
        RootObject rootObject = JsonConvert.DeserializeObject<RootObject>(input, new JsonSerializerSettings
        {
            Error = (object sender, Newtonsoft.Json.Serialization.ErrorEventArgs args) =>
            {
                // handle any parsing errors here
                args.ErrorContext.Handled = false;
                return;
            }
        });

        foreach (Record record in rootObject.records)
        {
            string[] parts = record.metricName.Split(new[] { "\\" }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 2)
            {
                continue;
            }

            string instanceName = "";
            string objectName = parts[0];
            string counterName = parts[1];

            int first = objectName.IndexOf('(');
            int last = objectName.LastIndexOf(')');

            if (first >= 0 && last > 0 && last > first)
            {
                instanceName = objectName.Substring(first + 1, last - first - 1);
                objectName = objectName.Substring(0, first);
            }

            laEvent newEvent = new laEvent()
            {
                TimeCurrent = record.time,
                SourceSystem = "Function",
                CounterPath = record.metricName,
                ObjectName = objectName,
                CounterName = counterName,
                InstanceName = instanceName,
                CounterValue = record.last,
                SampleCount = record.timeGrain,
                Computer = record.dimensions.RoleInstance
            };

            // here's your string
            string result = JsonConvert.SerializeObject(newEvent);

            // Create a hash for the API signature
            string datestring = DateTime.UtcNow.ToString("r");
            string stringToHash = "POST\n" + result.Length + "\napplication/json\n" + "x-ms-date:" + datestring + "\n/api/logs";
            string hashedString = BuildSignature(stringToHash, sharedKey);
            string signature = "SharedKey " + customerId + ":" + hashedString;

            await PostDataAsync(signature, datestring, result, log);
        }
    }
    catch (JsonException)
    {
        // this typically means we got an event type that we don't support in this function.
        // you can log this out, but it may be very verbose if there are a lot of unsupported event types.
        return;
    }

}

// Build the API signature
public static string BuildSignature(string message, string secret)
{
    var encoding = new System.Text.ASCIIEncoding();
    byte[] keyByte = Convert.FromBase64String(secret);
    byte[] messageBytes = encoding.GetBytes(message);
    using (var hmacsha256 = new HMACSHA256(keyByte))
    {
        byte[] hash = hmacsha256.ComputeHash(messageBytes);
        return Convert.ToBase64String(hash);
    }
}

// PostData
public static async Task PostDataAsync(string signature, string date, string json, TraceWriter log)
{
    try
    {
        string url = "https://" + customerId + ".ods.opinsights.azure.com/api/logs?api-version=2016-04-01";

        using(HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, new Uri(url)))
        {
            HttpContent httpContent = new StringContent(json, Encoding.UTF8);
            httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            request.Content = httpContent; 
            request.Headers.Add("Accept", "application/json");
            request.Headers.Add("Log-Type", LogName);
            request.Headers.Add("Authorization", signature);
            request.Headers.Add("x-ms-date", date);
            request.Headers.Add("time-generated-field", TimeStampField);
            
            using (HttpResponseMessage response = await client.SendAsync(request))
            {
                if (!response.IsSuccessStatusCode)
                {
                    string errorResponse = await response.Content.ReadAsStringAsync();

                    log.Info("Status code: " + response.StatusCode);
                    log.Warning("Error response: " + errorResponse);
                }
            }
        }
    }
    catch (Exception excep)
    {
        log.Warning("API Post Exception: " + excep.Message);
    }
}

public class Dimensions { public string DeploymentId { get; set; } public string Role { get; set; } public string RoleInstance { get; set; } }
public class Record { public string time { get; set; } public Dimensions dimensions { get; set; } public string metricName { get; set; } public double last { get; set; } public string timeGrain { get; set; } }
public class RootObject { public List<Record> records { get; set; } }

public class laEvent 
{
    public string TimeCurrent { get; set;} 
    public string SourceSystem { get; set;}
    public string CounterPath { get; set;}
    public string ObjectName { get; set;}
    public string CounterName { get; set;}
    public string InstanceName { get; set;}
    public double CounterValue { get; set;}
    public string SampleCount { get; set;}
    public string Computer {get; set;}
}
