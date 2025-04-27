// File: Form1.cs
using System;
using System.Collections.Generic;
using System.ComponentModel; // If needed for designer attributes, often implicitly used
using System.Data;         // If needed, often implicitly used
using System.Drawing;        // If needed, often implicitly used
using System.IO;
using System.Linq;
using System.Text;         // If needed, often implicitly used
using System.Text.Json;
using System.Threading.Tasks; // If needed, often implicitly used
using System.Windows.Forms;

// Ensure this namespace matches your project name (check your .csproj file or project properties)
namespace GraphGameGUI
{
    public partial class Form1 : Form
    {
        // --- State Variables ---
        private GraphData? currentGameGraph; // Nullable GraphData
        private GraphNode? currentNode;      // Nullable GraphNode

        // --- Constructor ---
        public Form1()
        {
            // This call is required by the Windows Form Designer.
            // It initializes the controls declared in Form1.Designer.cs
            InitializeComponent();

            // Set the initial state of the UI when the form loads
            UpdateUI();
        }

        // --- Event Handlers ---

        /// <summary>
        /// Handles the Click event for the "Load Graph File..." button.
        /// Opens a file dialog to select the graph.json file.
        /// </summary>
        private void btnLoadGraph_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                // Set dialog properties
                openFileDialog.InitialDirectory = Environment.CurrentDirectory; // Start in the application's directory
                openFileDialog.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*"; // Filter for JSON files
                openFileDialog.FilterIndex = 1; // Default to JSON files
                openFileDialog.RestoreDirectory = true; // Remember the last directory opened

                // Show the dialog and check if the user clicked OK
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    // Get the path of specified file
                    string filePath = openFileDialog.FileName;
                    // Attempt to load the graph from the selected file
                    LoadGraphFromFile(filePath);
                }
            }
        }

        /// <summary>
        /// Handles the Click event for *any* dynamically created action button.
        /// Retrieves the associated transition and moves the game to the next state.
        /// </summary>
        private void ActionButton_Click(object? sender, EventArgs e) // Make sender nullable
        {
            // Ensure the sender is a Button and it has a Transition object in its Tag
            if (sender is Button clickedButton && clickedButton.Tag is Transition chosenTransition)
            {
                // Display safety notes before proceeding, if any exist
                if (!string.IsNullOrWhiteSpace(chosenTransition.SafetyNotes))
                {
                    MessageBox.Show($"Safety Note:\n\n{chosenTransition.SafetyNotes}",
                                    "Safety Information",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Warning);
                }

                // Find the next node in the graph based on the transition's target ID
                GraphNode? nextNode = currentGameGraph?.Nodes?.FirstOrDefault(n => n.NodeId == chosenTransition.ToNodeId);

                if (nextNode != null)
                {
                    // Move to the next state
                    currentNode = nextNode;
                    // Refresh the entire UI to reflect the new state
                    UpdateUI();
                }
                else
                {
                    // Error if the target node defined in the transition doesn't exist in the node list
                    MessageBox.Show($"Error: Target node '{chosenTransition.ToNodeId ?? "UNKNOWN"}' not found for the chosen action.\nThe graph definition might be invalid.",
                                    "Graph Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    lblStatus.Text = $"Error: Could not find next state '{chosenTransition.ToNodeId ?? "UNKNOWN"}'.";
                }
            }
            else
            {
                // Should not happen if buttons are set up correctly, but good to handle
                MessageBox.Show("Error processing action button click. Button data missing.",
                                "Internal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        // --- Core Logic Methods ---

        /// <summary>
        /// Loads and parses the graph data from the specified JSON file.
        /// Initializes the game state if successful.
        /// </summary>
        /// <param name="filePath">The full path to the graph.json file.</param>
        private void LoadGraphFromFile(string filePath)
        {
            try
            {
                // Read the entire content of the file
                string jsonString = File.ReadAllText(filePath);

                // Configure JSON deserialization options (optional but good practice)
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true // Match JSON properties regardless of case
                };

                // Deserialize the JSON array into a list of GraphContainer objects
                var graphContainers = JsonSerializer.Deserialize<List<GraphContainer>>(jsonString, options);

                // Validate the loaded data
                if (graphContainers == null || graphContainers.Count == 0 || graphContainers[0].Output == null)
                {
                    // Throw if the basic structure is missing
                    throw new JsonException("No valid graph 'output' data found in the JSON file. Ensure the file has the correct array structure `[{\"output\": {...}}]`.");
                }

                // Assume we always use the first graph defined in the file's array
                currentGameGraph = graphContainers[0].Output;

                // Further validation of the loaded graph data
                if (currentGameGraph.Nodes == null || !currentGameGraph.Nodes.Any())
                {
                    throw new InvalidDataException("Graph data is missing 'nodes' or the nodes list is empty.");
                }
                // Ensure transitions list exists, even if empty
                currentGameGraph.Transitions ??= new List<Transition>();


                // Find the starting node marked as initial
                currentNode = currentGameGraph.Nodes.FirstOrDefault(n => n.IsInitial);

                if (currentNode == null)
                {
                    // Critical error if no starting point is defined
                    throw new InvalidDataException("No node marked as 'is_initial: true' found in the graph.");
                }

                // Success! Update status and display the initial state
                lblStatus.Text = $"Graph '{Path.GetFileName(filePath)}' loaded successfully.";
                UpdateUI();
            }
            catch (FileNotFoundException)
            {
                MessageBox.Show($"Error: The file was not found.\nPath: {filePath}", "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                ResetGame(); // Reset state if file not found
            }
            catch (JsonException jsonEx)
            {
                // Handle errors during JSON parsing
                string errorDetails = jsonEx.Message;
                if (jsonEx.LineNumber.HasValue) // Provide line number if available
                {
                    errorDetails += $"\nError near Line: {jsonEx.LineNumber + 1}, Position: {jsonEx.BytePositionInLine + 1}";
                }
                MessageBox.Show($"Error reading or parsing the JSON file:\n{errorDetails}", "JSON Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                ResetGame();
            }
            catch (InvalidDataException dataEx)
            {
                // Handle logical errors in the graph structure
                MessageBox.Show($"Error in graph structure:\n{dataEx.Message}", "Graph Data Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                ResetGame();
            }
            catch (IOException ioEx)
            {
                // Handle general file reading errors
                MessageBox.Show($"Error reading file:\n{ioEx.Message}", "File Access Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                ResetGame();
            }
            catch (Exception ex)
            {
                // Catch-all for any other unexpected errors during loading
                MessageBox.Show($"An unexpected error occurred while loading the graph:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                ResetGame();
            }
        }

        /// <summary>
        /// Resets the game state and clears the UI elements.
        /// Called when loading fails or potentially to start over.
        /// </summary>
        private void ResetGame()
        {
            currentGameGraph = null;
            currentNode = null;
            lblStatus.Text = "Please load a graph file to begin.";
            UpdateUI(); // Update UI to reflect the reset state
        }

        // --- UI Update Method ---

        /// <summary>
        /// Refreshes all UI elements based on the current game state (currentGameGraph and currentNode).
        /// Displays node information, materials, and available action buttons.
        /// </summary>
        private void UpdateUI()
        {
            // --- Clear Previous State ---
            txtDescription.Text = string.Empty;
            lblStateId.Text = "(No graph loaded)";
            lstMaterials.Items.Clear();
            flpActions.Controls.Clear(); // Remove all dynamically created buttons

            // --- Handle No Graph Loaded State ---
            if (currentGameGraph == null || currentNode == null)
            {
                // Disable interactive elements if no game is active
                txtDescription.Enabled = false;
                lstMaterials.Enabled = false;
                flpActions.Enabled = false; // Disable the panel containing action buttons
                // Ensure status reflects the need to load a graph
                if (string.IsNullOrEmpty(lblStatus.Text) || lblStatus.Text.StartsWith("Graph"))
                {
                    lblStatus.Text = "Please load a graph file to begin.";
                }
                return; // Stop further UI updates
            }

            // --- Enable Controls for Active Game ---
            txtDescription.Enabled = true;
            lstMaterials.Enabled = true;
            flpActions.Enabled = true;

            // --- Display Current Node Information ---
            lblStateId.Text = currentNode.NodeId ?? "N/A"; // Show Node ID or "N/A" if null
            txtDescription.Text = currentNode.Description ?? "No description provided."; // Show description or default text

            // --- Populate Materials List ---
            if (currentNode.MaterialsPresent != null)
            {
                // Use AddRange for slightly better performance with many items
                lstMaterials.Items.AddRange(currentNode.MaterialsPresent.ToArray());
            }

            // --- Handle Final State ---
            if (currentNode.IsFinal)
            {
                lblStatus.Text = "Experiment Complete! You have reached the final state.";
                flpActions.Enabled = false; // No more actions from the final state

                // Optional: Add a label to the actions panel indicating completion
                Label finalLabel = new Label
                {
                    Text = "--- FINAL STATE REACHED ---",
                    AutoSize = true,
                    Padding = new Padding(5),
                    ForeColor = System.Drawing.Color.Green // Make it stand out
                };
                flpActions.Controls.Add(finalLabel);
                return; // Stop processing, no action buttons needed
            }

            // --- Find and Display Available Actions (Transitions) ---
            var possibleTransitions = currentGameGraph.Transitions? // Null check on Transitions
                                        .Where(t => t.FromNodeId == currentNode.NodeId)
                                        .ToList() ?? new List<Transition>(); // Default to empty list if null

            if (possibleTransitions.Any())
            {
                // Create a button for each valid transition from the current state
                foreach (var transition in possibleTransitions)
                {
                    Button actionButton = new Button
                    {
                        // Use the helper method from ActionDetail for user-friendly text
                        Text = transition.Action?.GetActionDescription() ?? "Perform Action", // Default text if action is missing
                        Tag = transition, // CRITICAL: Store the transition data with the button
                        AutoSize = true,       // Adjust button size automatically
                        MinimumSize = new System.Drawing.Size(100, 0), // Ensure buttons aren't too small
                        Margin = new Padding(5) // Add spacing between buttons
                    };
                    actionButton.Click += ActionButton_Click; // Attach the common event handler
                    flpActions.Controls.Add(actionButton);    // Add the button to the panel
                }
                lblStatus.Text = "Select an action to proceed."; // Update status for normal operation
            }
            else
            {
                // No transitions defined from this state (and it's not the final one)
                lblStatus.Text = "No further actions defined from this state (possible dead end in graph).";
                flpActions.Enabled = false; // Disable actions if none are possible

                // Optional: Add a label indicating the dead end
                Label deadEndLabel = new Label
                {
                    Text = "--- NO ACTIONS AVAILABLE ---",
                    AutoSize = true,
                    Padding = new Padding(5),
                    ForeColor = System.Drawing.Color.Red // Make it stand out
                };
                flpActions.Controls.Add(deadEndLabel);
            }
        }
    }
}