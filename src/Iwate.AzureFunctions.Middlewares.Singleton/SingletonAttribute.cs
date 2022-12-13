namespace Iwate.AzureFunctions.Middlewares.Singleton;

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class SingletonAttribute : Attribute
{
    public SingletonAttribute(string? partialKey = null)
    {
        PartialKey = partialKey;
    }
    
    public string? PartialKey { get; init; }
}
