# Using Functions and EventHubs to get perf counter data into OMS

There's a gap today with getting Service Fabric's custom performance counters into Log Analytics, because of limitations in the OMS/SCOM agent. This repo contains an Azure Functions app that can be used to get performance counters into OMS Log Analytics via the [Log Collector API](https://docs.microsoft.com/en-us/azure/log-analytics/log-analytics-data-collector-api), for Windows clusters running SF.

The basic path here is:
* WAD (Windows Azure Diagnostics extension) collects the performance counters and sends them to an EventHub
* A function app is running with an EventHub trigger, on each new request reaching an EventHub
* The function extracts the incoming EventData, restructures it to have the PerfCounter properties other performance counters in Log Analytics will have (if collected by the OMS Agent), and sends it to your workspace

## Basic instructions

### Prereqs

To use this, I assume you have the following set up: 
* A Service Fabric Windows cluster up and running already, and can get access to the ARM template (check out [](resources.azure.com) if you need help with this)
* A Log Analytics Workspace up and running that you have access to (you will need the private key and workspace id) 

### Steps

#### Collect performance counters
The first step is to actually collect some performance counters! This is most helpful in getting recommended perf counters for Reliable Services and Actors into Log Analytics. Go get your ARM template and add some performance counters to the WadCfg section (where WAD is configured). Here is a random selection of perf counters that I added to test this:

    ```json
    "PerformanceCounters": {
        "scheduledTransferPeriod": "PT1M",
        "PerformanceCounterConfiguration": [
            {
                "counterSpecifier": "\\Processor(_Total)\\% Processor Time",
                "sampleRate": "PT5M",
                "unit": "Percent"
            },
            {
                "counterSpecifier": "\\Service Fabric Actor(*)\\Average milliseconds per request",
                "sampleRate": "PT1M"
            },
            {
                "counterSpecifier": "\\Service Fabric Service(*)\\Average milliseconds per request",
                "sampleRate": "PT1M"
            },
            {
                "counterSpecifier": "\\Service Fabric Actor Method(*)\\Invocations/Sec",
                "sampleRate": "PT1M"
            },
            {
                "counterSpecifier": "\\.NET CLR Memory(*)\\% Time in GC",
                "sampleRate": "PT1M",
                "unit": "Percent"
            }
        ]
    }
    ```

#### Create and configure an EventHub

Create an EventHub in Azure. This involves first creating a new EventHubNamespace, under which you create the EventHub. Once your EventHub is up, add a policy for sending/receiving requests.

#### Add an EventHub sink to WAD

This requires 3 changes to your WadCfg:

1. Configure the sink

    ```json
    "SinksConfig": {
        "Sink": [
            {
                "name": "eventHub",
                "EventHub": {
                    "Url": "https://dekapurventhub.servicebus.windows.net/perfcounterhub",
                    "SharedAccessKeyName": "oneRuleToRingThemAll"
                }    
            }
        ]
    }
    ```

2. Add the EventHub access key to the `protectedSettings` in the IaaSDiagnostics extension:
    
    ```json
    "protectedSettings": {
        "storageAccountName": "[parameters('applicationDiagnosticsStorageAccountName')]",
        "storageAccountKey": "[listKeys(resourceId('Microsoft.Storage/storageAccounts', parameters('applicationDiagnosticsStorageAccountName')),'2015-05-01-preview').key1]",
        "storageAccountEndPoint": "https://core.windows.net/",
        "EventHub": {
            "Url": "https://dekapureventhub.servicebus.windows.net/perfcounterhub",
            "SharedAccessKeyName": "oneRuleToRingThemAll",
            "SharedAccessKey": "******************************" 
            "
        }                            
    }, 
    ```

3. Add the sink to your `DiagnosticMonitorConfiguration` either at the top level for all your events or just where the perf counters are configured:

    ```json
    "sinks": "eventHub",
    ```

#### Update your cluster

Once you've added the required perf counters and the EventHub sink to your WadCfg, update your cluster using an ARM upgrade. The easiest way to do this is to try redeploying a new resource group with the same template and parameter files. ARM will realize that these resources already exist, and just update the required delta between the old template and the new. 

    ```powershell
    New-AzureRmResourceGroupDeployment `
        -ResourceGroupName dekapurwindowscluster `
        -TemplateFile 'template.json' `
        -TemplateParameterFile 'parameters.json' `
    ```

This should go through in under 15min. Check to see if you are collecting the right perf counters in your `WADPerformanceCounterTable` in your Storage account and make sure there is incoming request traffic to your EventHub.

#### Set up the Function

1. Create a new *Function App* resource in Azure Portal
2. Add a new function, using the custom function template. Use the EventHubTrigger, and specify that you will use C#
3. Hook up the Function to your EventHub
4. Copy the Function script from this repo - https://github.com/dkkapur/function-eventhub-to-loganalytics/blob/master/function.csx
5. Modify the values for the Log Analytics shared key and the customer id
6. Save and run!

## Next steps for this repo
Please feel free to share / improve this as you see fit! Here is a short list for things that need to be updated for this solution:
* the function currently process each incoming request independently, and deserializes/re-seralizes individually for each one. It would be a lot more efficient to open up a stream to do this. 
* the function is currently written as a .csx file that has to be deployed as an App Service Function App in Azure Portal. There's a dir in this repo called "vs-solution" where I'm trying to rebuild this as a .NET app that can be deployed indepedently. The ideal state for me would be to have an app that contains the required Funtions runtime, and can be deployed as a container on an SF cluster or elsewhere.
* I'm working on improving this readme to have better instructions - albeit slowly!

Anyway, hope this was useful! Looking forward to hearing your feedback. 
