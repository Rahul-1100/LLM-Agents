// File: GetGraph.cs
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks; // Added for async Task

namespace GraphGameGUI
{
    // Define the delegate to return a Task<string> (the path of the generated file)
    public delegate Task<string> GraphDelegate(
        string arg1 = "voltage follower using lm741 op amp",
        string arg2 = "[\"LM741 Operational Amplifier\", \"Breadboard\", \"Jumper wires\", \"Power supply\", \"Resistors (1kΩ)\", \"Resistors (10kΩ)\", \"Signal generator\", \"Multimeter\"]",
        string arg3 = "[\"connect\", \"place\", \"adjust\", \"observe\", \"disconnect\", \"turn_on\", \"turn_off\", \"measure\"]");

    public class GraphService
    {
        // The name of the file where the graph will be saved
        public const string DefaultOutputFilename = "Graph.json";

        // Update the implicit operator to match the new delegate signature
        public static implicit operator GraphDelegate(GraphService graphService)
        {
            return graphService.GenerateGraphAsync;
        }

        /// <summary>
        /// Asynchronously fetches graph data from the webhook and saves it to a file.
        /// </summary>
        /// <param name="arg1">Experiment name or description.</param>
        /// <param name="arg2">JSON string array of materials.</param>
        /// <param name="arg3">JSON string array of actions.</param>
        /// <returns>The path to the generated file if successful.</returns>
        /// <exception cref="HttpRequestException">Thrown if the network request fails.</exception>
        /// <exception cref="IOException">Thrown if writing the file fails.</exception>
        /// <exception cref="Exception">Thrown for other unexpected errors.</exception>
        public async Task<string> GenerateGraphAsync(
            string arg1 = "voltage follower using lm741 op amp",
            string arg2 = "[\"LM741 Operational Amplifier\", \"Breadboard\", \"Jumper wires\", \"Power supply\", \"Resistors (1kΩ)\", \"Resistors (10kΩ)\", \"Signal generator\", \"Multimeter\"]",
            string arg3 = "[\"connect\", \"place\", \"adjust\", \"observe\", \"disconnect\", \"turn_on\", \"turn_off\", \"measure\"]")
        {
            // Use 'using' for HttpClient is generally recommended for simple cases,
            // but for high-load apps, a static/singleton instance is better.
            // For this desktop app, 'using' is fine.
            using (HttpClient client = new HttpClient())
            {
                // Consider adding a timeout
                client.Timeout = TimeSpan.FromSeconds(300); // Example: 2 minutes

                // Construct the URL carefully - Consider URL encoding arguments if they contain special characters
                string url = $"http://192.168.23.138:5678/webhook/96665228-6276-44ad-924f-d665b971146b/generate_exp/{Uri.EscapeDataString(arg1)}/{Uri.EscapeDataString(arg2)}/{Uri.EscapeDataString(arg3)}";

                // Make the GET request asynchronous
                HttpResponseMessage response = await client.GetAsync(url);
                // Configure the HttpClient to wait indefinitely for a response
                
                // Check if the request was successful
                response.EnsureSuccessStatusCode(); // Throws HttpRequestException if not successful

                // Read the response content asynchronously
                string responseBody = await response.Content.ReadAsStringAsync();

                // Write the JSON content to the file asynchronously
                string outputPath = Path.Combine(Environment.CurrentDirectory, DefaultOutputFilename);
                await File.WriteAllTextAsync(outputPath, responseBody); // Async file write

                // Return the path of the successfully created file
                return outputPath;
            }
            // Catch blocks can be added here, but it's often better to let
            // exceptions bubble up to the caller (the Form) to handle UI feedback.
        }
    }
}