using Serilog.Core;
using Serilog.Events;

namespace PackTracker.Logging.Destructuring;

public sealed class ExceptionDestructuringPolicy : IDestructuringPolicy
{
    public bool TryDestructure(
        object value,
        ILogEventPropertyValueFactory propertyValueFactory,
        out LogEventPropertyValue? result)
    {
        result = null;

        if (value is not Exception ex)
            return false;

        var properties = new List<LogEventProperty>
        {
            new("Type", new ScalarValue(ex.GetType().FullName)),
            new("Message", new ScalarValue(ex.Message)),
            new("Source", new ScalarValue(ex.Source)),
            new("StackTrace", new ScalarValue(ex.StackTrace)),
            new("InnerExceptionMessage", new ScalarValue(ex.InnerException?.Message))
        };

        result = new StructureValue(properties, "Exception");
        return true;
    }
}