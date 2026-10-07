using System.Globalization;

namespace Cabinet.FakeBgg;

/// <summary>Command-line entry point that runs the fake until it is stopped.</summary>
public static class FakeBggProgram
{
    private const int DefaultPort = 6190;
    private const int DefaultSize = 65;

    /// <summary>Starts the fake; arguments are <c>--port</c>, <c>--scenario</c> and <c>--size</c>.</summary>
    /// <param name="args">The command-line arguments.</param>
    public static async Task<int> Main(string[] args)
    {
        if (!TryReadArguments(args, out var port, out var scenario, out var size, out var problem))
        {
            await Console.Error.WriteLineAsync(problem);
            await Console.Error.WriteLineAsync("Usage: Cabinet.FakeBgg [--port 6190] [--scenario normal] [--size 65]");
            return 2;
        }

        var app = FakeBggServer.Create(scenario, size, port);
        Console.WriteLine($"fake BGG listening on http://127.0.0.1:{port.ToString(CultureInfo.InvariantCulture)}/xmlapi2/ (scenario {scenario}, size {SyntheticBggCollection.Clamp(size).ToString(CultureInfo.InvariantCulture)})");
        Console.WriteLine($"start the cabinet in Development with: Bgg__BaseUri=http://127.0.0.1:{port.ToString(CultureInfo.InvariantCulture)}/xmlapi2/");
        Console.WriteLine($"and: Images__DevelopmentOrigin=http://127.0.0.1:{port.ToString(CultureInfo.InvariantCulture)}");
        await app.RunAsync();
        return 0;
    }

    private static bool TryReadArguments(string[] args, out int port, out FakeBggScenario scenario, out int size, out string problem)
    {
        port = DefaultPort;
        scenario = FakeBggScenario.Default;
        size = DefaultSize;
        problem = string.Empty;

        for (var index = 0; index < args.Length; index += 2)
        {
            if (index + 1 >= args.Length)
            {
                problem = $"The argument {args[index]} needs a value.";
                return false;
            }

            var value = args[index + 1];
            switch (args[index])
            {
                case "--port" when int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out port):
                case "--size" when int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out size):
                    break;
                case "--scenario" when FakeBggScenario.TryParse(value, out scenario):
                    break;
                default:
                    problem = $"Cannot use {args[index]} {value}.";
                    return false;
            }
        }

        return true;
    }
}
