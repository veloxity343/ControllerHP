namespace WattsLeft.Battery;

/// <summary>
/// Sources that see the same controllers, ordered best-first. Only the first one that finds any
/// controller is used, so the same controller isn't shown twice (e.g. once by Windows.Gaming.Input and
/// again by XInput).
/// </summary>
public sealed class FirstAvailableSource(string name, params IBatterySource[] sources) : IBatterySource, IDisposable
{
    public string Name => name;

    public IReadOnlyList<IBatterySource> Sources => sources;

    public event EventHandler? ControllersChanged
    {
        add
        {
            foreach (var source in sources)
                source.ControllersChanged += value;
        }
        remove
        {
            foreach (var source in sources)
                source.ControllersChanged -= value;
        }
    }

    /// <summary>The first source's readings that has any. Throws only if every source failed, so that isn't taken for "none".</summary>
    public IReadOnlyList<ControllerBattery> Read()
    {
        Exception? failure = null;
        bool anyAnswered = false;
        foreach (var source in sources)
        {
            try
            {
                var readings = source.Read();
                if (readings.Count > 0)
                    return readings;
                anyAnswered = true;
            }
            catch (Exception ex)
            {
                Log.Once($"{source.Name} failed: {ex.GetType().Name}: {ex.Message}");
                failure ??= ex;
            }
        }
        if (!anyAnswered && failure is not null)
            throw new InvalidOperationException($"Every {name} source failed", failure);
        return [];
    }

    public void Dispose()
    {
        foreach (var source in sources)
            (source as IDisposable)?.Dispose();
    }
}
