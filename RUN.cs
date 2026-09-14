using System.Diagnostics;
using System.Net.Http;

class Program
{
    static int Run(string fileName, string arguments, string workingDirectory, bool waitForExit = true)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = false
        };

        process.OutputDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) Console.WriteLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) Console.Error.WriteLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        if (!waitForExit)
            return 0;

        process.WaitForExit();
        return process.ExitCode;
    }
    static void StopExistingApi()
    {
        foreach (var process in Process.GetProcessesByName("Smart_X_API"))
        {
            try
            {
                Console.WriteLine($"Stopping existing Smart_X_API process ({process.Id})...");
                process.Kill();
                process.WaitForExit();
            }
            catch
            {
                Console.WriteLine($"Could not stop Smart_X_API process ({process.Id}).");
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    static async Task<bool> WaitForApiAsync()
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        const string url = "http://localhost:8080";

        Console.WriteLine("Waiting for API to become ready...");

        for (int attempt = 1; attempt <= 30; attempt++)
        {
            try
            {
                using var response = await client.GetAsync(url);
                Console.WriteLine("API is ready.");
                return true;
            }
            catch
            {
                Console.WriteLine($"Waiting for API... ({attempt}/30)");
                await Task.Delay(1000);
            }
        }

        Console.WriteLine();
        Console.WriteLine("ERROR: API did not become ready.");
        return false;
    }

    static async Task Main()
    {
        string root = Directory.GetCurrentDirectory();
        string api = Path.Combine(root, "Smart_X_API");
        string ui = Path.Combine(root, "Smart_X_UI");
        string simulator = Path.Combine(root, "Smart_X_Simulator.py");

        Console.WriteLine("================================");
        Console.WriteLine("Smart_X Launcher");
        Console.WriteLine("================================");
        Console.WriteLine();

        if (!Directory.Exists(api))
        {
            Console.WriteLine("ERROR: API directory not found:");
            Console.WriteLine(api);
            return;
        }

        if (!Directory.Exists(ui))
        {
            Console.WriteLine("ERROR: UI directory not found:");
            Console.WriteLine(ui);
            return;
        }

        StopExistingApi();

        Console.WriteLine();
        Console.WriteLine("================================");
        Console.WriteLine("Building API");
        Console.WriteLine("================================");
        Console.WriteLine();

        if (Run("dotnet", "build Smart_X_API.csproj", api) != 0)
        {
            Console.WriteLine();
            Console.WriteLine("API build failed.");
            return;
        }

        Console.WriteLine();
        Console.WriteLine("API build completed successfully.");
        Console.WriteLine();
        Console.WriteLine("================================");
        Console.WriteLine("Building UI");
        Console.WriteLine("================================");
        Console.WriteLine();

        if (Run("dotnet", "build Smart_X_UI.csproj", ui) != 0)
        {
            Console.WriteLine();
            Console.WriteLine("UI build failed.");
            return;
        }

        Console.WriteLine();
        Console.WriteLine("UI build completed successfully.");
        Console.WriteLine();
        Console.WriteLine("================================");
        Console.WriteLine("Python Simulator");
        Console.WriteLine("================================");
        Console.WriteLine();

        // We don't auto-start this one - it's easier to launch it manually once you can
        // actually see the dashboard and watch data come in.
        if (File.Exists(simulator))
        {
            Console.WriteLine("Smart_X_Simulator.py found.");
            Console.WriteLine("The simulator will not start automatically.");
        }
        else
        {
            Console.WriteLine("Warning: Smart_X_Simulator.py not found.");
        }

        Console.WriteLine();
        Console.WriteLine("================================");
        Console.WriteLine("Building Docker Image");
        Console.WriteLine("================================");
        Console.WriteLine();
        Console.WriteLine("Docker build started.");
        Console.WriteLine("Waiting for Docker build to fully complete...");
        Console.WriteLine();

        if (Run("docker", "build -t smart-x-api:latest .", api) != 0)
        {
            Console.WriteLine();
            Console.WriteLine("Docker image build failed.");
            return;
        }

        Console.WriteLine();
        Console.WriteLine("Docker image build completed successfully.");
        Console.WriteLine();
        Console.WriteLine("================================");
        Console.WriteLine("Removing Existing Container");
        Console.WriteLine("================================");
        Console.WriteLine();

        Run("docker", "rm -f smart-x-api", api);

        Console.WriteLine();
        Console.WriteLine("================================");
        Console.WriteLine("Starting Docker Container");
        Console.WriteLine("================================");
        Console.WriteLine();
        Console.WriteLine("Starting smart-x-api...");
        Console.WriteLine();

        if (Run("docker", "run -d --name smart-x-api -p 8080:8080 smart-x-api:latest", api) != 0)
        {
            Console.WriteLine();
            Console.WriteLine("Docker container failed to start.");
            return;
        }

        Console.WriteLine();
        Console.WriteLine("Docker container started.");
        Console.WriteLine("API: http://localhost:8080");
        Console.WriteLine();

        if (!await WaitForApiAsync())
        {
            Console.WriteLine();
            Console.WriteLine("The UI will not be started because the API is unavailable.");
            return;
        }

        Console.WriteLine();
        Console.WriteLine("================================");
        Console.WriteLine("Starting UI");
        Console.WriteLine("================================");
        Console.WriteLine();
        Console.WriteLine("Starting Smart_X UI...");
        Console.WriteLine();

        Run("dotnet", "run --no-build", ui, false);

        if (File.Exists(simulator))
        {
            Console.WriteLine();
            Console.WriteLine("================================");
            Console.WriteLine("Python Simulator");
            Console.WriteLine("================================");
            Console.WriteLine();
            Console.WriteLine("Press ENTER to start the Python simulator.");
            Console.WriteLine("Press any other key to skip.");
            Console.WriteLine();

            var key = Console.ReadKey(true);

            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                Console.WriteLine("Starting Python simulator...");
                Console.WriteLine();

                int simulatorResult = Run("python", "\"Smart_X_Simulator.py\"", root);
                Console.WriteLine();

                Console.WriteLine(simulatorResult != 0
                    ? $"Python simulator exited with code {simulatorResult}."
                    : "Python simulator finished.");
            }
            else
            {
                Console.WriteLine();
                Console.WriteLine("Python simulator skipped.");
            }
        }

        Console.WriteLine();
        Console.WriteLine("================================");
        Console.WriteLine("Smart_X Startup Complete");
        Console.WriteLine("================================");
        Console.WriteLine();
        Console.WriteLine("API: http://localhost:8080");
        Console.WriteLine("UI: Started");
        Console.WriteLine();
    }
}
