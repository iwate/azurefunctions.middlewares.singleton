A singleton lib for dotnet-isolated of Azure Functions

# How to Install

```
$ dotnet add package Iwate.AzureFunctions.Middlewares.Singleton
```

# How to Use

1. Register this middleware

```
var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults(app => {
        app.UseMiddleware<Iwate.AzureFunctions.Middlewares.Singleton.SingletonMiddleware>();
    })
    .Build();
host.Run();
```

2. Declare `Singleton` attribute on your function.

```
public class SingletonFunction
{
    [Function("SingletonFunction")]
    [Singleton()]
    public async Task RunAsync([QueueTrigger(QUEUE_NAME)]string message)
    {
        ...
    }
}
```

You can separate a singleton stream to a few lines by partial key if you put the arg on the `Singleton` attribute.

```
+-----------------+
|PARTIAL KEY: 100 |
+-------+---------+
        |
+-------v---------+                       +-----------------+
|PARTIAL KEY: 100 |                       |PARTIAL KEY: 101 |
+-------+---------+                       +--------+--------+
        |                                          |
+-------v------------------------------------------v--------+
|                                                           |
|                      Azure Functions                      |
|                                                           |
+-----------------------------------------------------------+
```

```
public class SingletonFunction
{
    [Function("SingletonFunction")]
    [Singleton(nameof(Trigger.PartialKey))]
    public async Task RunAsync([QueueTrigger(QUEUE_NAME)]Trigger trigger)
    {
        ...
    }
}
public class Trigger
{
    public string PartialKey { get; set; }
    public string Message { get; set; }
}
```

