using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace Iwate.AzureFunctions.Middlewares.Singleton;

// https://github.com/Azure/azure-functions-dotnet-worker/issues/938#issuecomment-1167329390
public class SingletonMiddleware : IFunctionsWorkerMiddleware
{
    private readonly LockService _lockService;
    private readonly ILogger<SingletonMiddleware> _logger;
    public SingletonMiddleware(LockService lockService, ILogger<SingletonMiddleware> logger) {
        _lockService = lockService;
        _logger = logger;
    }
    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var workerAssembly = Assembly.GetExecutingAssembly();

        var entryPointParts = context.FunctionDefinition.EntryPoint.Split(".");

        var workerTypeName = string.Join(".", entryPointParts[..^1]);
        var workerFunctionName = entryPointParts.Last();

        var workerType = workerAssembly.GetType(workerTypeName)!;
        var workerFunction = workerType.GetMethod(workerFunctionName)!;

        if (workerFunction.GetCustomAttribute<SingletonAttribute>() is SingletonAttribute attr)
        {
            var logger = context.GetLogger<SingletonMiddleware>();

            logger.LogTrace($"Singleton invocation '{context.InvocationId}' waiting for lock...");
            
            var partialKey = string.Empty;
            
            if (!string.IsNullOrEmpty(attr.PartialKey) && context.BindingContext.BindingData.TryGetValue(attr.PartialKey, out var value)) {
                partialKey = $"/{value}";
            }
            
            var lockName = $"{context.FunctionDefinition.EntryPoint}{partialKey}.lock";

            await using var @lock = await _lockService.Lock(lockName, CancellationToken.None);

            logger.LogTrace($"Singleton invocation '{context.InvocationId}' entered lock");

            try
            {
                await next(context);
                logger.LogTrace($"Singleton invocation '{context.InvocationId}' released lock");
            }
            catch (Exception)
            {
                logger.LogTrace($"Singleton invocation '{context.InvocationId}' released lock");
                throw;
            }
        }
        else
        {
            await next(context);
        }
    }
}
