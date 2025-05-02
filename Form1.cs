// File: Form1.cs
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks; // Added for async Task
using System.Windows.Forms;

// Ensure this namespace matches your project name
namespace GraphGameGUI
{
    public partial class Form1 : Form
    {
        // --- State Variables ---
        private GraphData? currentGameGraph;
        private GraphNode? currentNode;
        private GraphDelegate graphDelegate; // Now expects a Task<string> delegate

        // Added state for generation process
        private bool isGeneratingGraph = false;

        // --- Constructor ---
        public Form1()
        {
            InitializeComponent(); // Initialize controls first

            GraphService graphService = new GraphService();
            graphDelegate = graphService; // Implicit conversion assigns GenerateGraphAsync

            UpdateUI(); // Set initial UI state
        }

        // --- Event Handlers ---

        /// <summary>
        /// Handles the Click event for the "Load Graph File..." button.
        /// </summary>
        private void btnLoadGraph_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                // Set initial directory to current directory or where Graph.json might be
                string initialDir = Path.Combine(Environment.CurrentDirectory);
                if (Directory.Exists(initialDir))
                {
                    openFileDialog.InitialDirectory = initialDir;
                }
                else
                {
                    openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                }

                openFileDialog.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*";
                openFileDialog.FilterIndex = 1;
                openFileDialog.RestoreDirectory = true;
                openFileDialog.FileName = GraphService.DefaultOutputFilename; // Default to generated file name

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string filePath = openFileDialog.FileName;
                    LoadGraphFromFile(filePath);
                }
            }
        }

        /// <summary>
        /// Handles the Click event for *any* dynamically created action button.
        /// (No changes needed here for the generation logic)
        /// </summary>
        private void ActionButton_Click(object? sender, EventArgs e)
        {
            // Ensure sender is a Button, Tag contains a Transition, and game state is valid
            if (sender is Button clickedButton && clickedButton.Tag is Transition chosenTransition && currentNode != null && currentGameGraph != null)
            {
                // *** CORE CHECK: Verify if the clicked action's transition is valid for the current state ***
                if (chosenTransition.FromNodeId == currentNode.NodeId)
                {
                    // --- Proceed with valid transition --- Correct button was clicked

                    // Display safety notes before proceeding, if any exist
                    if (!string.IsNullOrWhiteSpace(chosenTransition.SafetyNotes))
                    {
                        MessageBox.Show($"Safety Note:\n\n{chosenTransition.SafetyNotes}",
                                        "Safety Information",
                                        MessageBoxButtons.OK,
                                        MessageBoxIcon.Warning);
                    }

                    // Find the next node based on the transition's target ID
                    GraphNode? nextNode = currentGameGraph.Nodes?.FirstOrDefault(n => n.NodeId == chosenTransition.ToNodeId);

                    if (nextNode != null)
                    {
                        // Move to the next state
                        currentNode = nextNode;
                        // Refresh the entire UI to reflect the new state
                        UpdateUI();
                    }
                    else
                    {
                        // Error if the target node defined in the transition doesn't exist
                        MessageBox.Show($"Error: Target node '{chosenTransition.ToNodeId ?? "UNKNOWN"}' not found for the chosen action.\nThe graph definition might be invalid.",
                                        "Graph Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        lblStatus.Text = $"Error: Could not find next state '{chosenTransition.ToNodeId ?? "UNKNOWN"}'.";
                    }
                    // --- End of valid transition logic ---
                }
                else
                {
                    // --- Handle incorrect action for the current state ---
                    lblStatus.Text = $"Action '{chosenTransition.Action?.GetActionDescription() ?? "Selected Action"}' is not valid from the current state '{currentNode.NodeId}'. Try a different action.";
                }
            }
            else if (currentNode == null || currentGameGraph == null)
            {
                MessageBox.Show("Cannot perform action: Game is not loaded or in a valid state.",
                               "State Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                MessageBox.Show("Error processing action button click. Button data missing or invalid.",
                                "Internal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        // --- Core Logic Methods ---

        /// <summary>
        /// Loads and parses graph data using models from GraphModels.cs.
        /// </summary>
        private void LoadGraphFromFile(string filePath)
        {
            try
            {
                // Check if file exists before reading
                if (!File.Exists(filePath))
                {
                     throw new FileNotFoundException($"The specified graph file was not found.", filePath);
                }

                string jsonString = File.ReadAllText(filePath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                // Assumes structure [{"output": {...}}]
                var graphContainers = JsonSerializer.Deserialize<List<GraphContainer>>(jsonString, options);

                if (graphContainers == null || graphContainers.Count == 0 || graphContainers[0].Output == null)
                {
                    // Try deserializing directly as GraphData if the outer list structure is missing
                    var directGraph = JsonSerializer.Deserialize<GraphData>(jsonString, options);
                    if (directGraph != null && directGraph.Nodes != null) {
                        currentGameGraph = directGraph;
                        // Fallthrough to node validation
                    } else {
                        throw new JsonException("No valid graph 'output' data found in `[{\"output\": {...}}]` structure, and direct parsing as GraphData also failed.");
                    }
                }
                 else
                {
                    currentGameGraph = graphContainers[0].Output;
                }


                if (currentGameGraph.Nodes == null || !currentGameGraph.Nodes.Any())
                {
                    throw new InvalidDataException("Graph data is missing 'nodes' or the node list is empty.");
                }
                // Ensure Transitions list exists, even if empty, for iteration later
                currentGameGraph.Transitions ??= new List<Transition>();

                currentNode = currentGameGraph.Nodes.FirstOrDefault(n => n.IsInitial);
                if (currentNode == null)
                {
                     // Attempt to use the first node if no initial node is marked
                    currentNode = currentGameGraph.Nodes.FirstOrDefault();
                     if (currentNode == null)
                     {
                        // This should not happen if Nodes.Any() passed, but check defensively
                         throw new InvalidDataException("No nodes found in the graph data.");
                     }
                     // Optionally warn the user that the first node is being used as the initial state
                     MessageBox.Show("Warning: No node was marked as 'is_initial: true'. Starting from the first node found.", "Graph Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    //throw new InvalidDataException("No node marked as 'is_initial: true' found.");
                }

                lblStatus.Text = $"Graph '{Path.GetFileName(filePath)}' loaded. Initial State: {currentNode.NodeId}";
                UpdateUI(); // Refresh UI with loaded graph
            }
            // --- Keep existing robust error handling ---
            catch (FileNotFoundException fnfEx) { MessageBox.Show($"Error: File not found.\nPath: {fnfEx.FileName}\nMessage: {fnfEx.Message}", "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error); ResetGame(); }
            catch (JsonException jsonEx) { MessageBox.Show($"Error parsing JSON:\n{jsonEx.Message}\nPath: {jsonEx.Path}\nLine: {jsonEx.LineNumber}, Pos: {jsonEx.BytePositionInLine}", "JSON Error", MessageBoxButtons.OK, MessageBoxIcon.Error); ResetGame(); }
            catch (InvalidDataException dataEx) { MessageBox.Show($"Error in graph data structure:\n{dataEx.Message}", "Graph Data Error", MessageBoxButtons.OK, MessageBoxIcon.Error); ResetGame(); }
            catch (IOException ioEx) { MessageBox.Show($"Error reading file:\n{ioEx.Message}", "File Access Error", MessageBoxButtons.OK, MessageBoxIcon.Error); ResetGame(); }
            catch (Exception ex) { MessageBox.Show($"An unexpected error occurred loading graph:\n{ex.ToString()}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); ResetGame(); }
        }

        /// <summary>
        /// Resets the game state and clears UI.
        /// </summary>
        private void ResetGame()
        {
            currentGameGraph = null;
            currentNode = null;
            // Don't reset status if it's showing an error message from loading
            if (!lblStatus.Text.StartsWith("Error:"))
            {
                 lblStatus.Text = "Please load or generate a graph file to begin.";
            }
            UpdateUI();
        }

        // --- UI Update Method ---

        /// <summary>
        /// Refreshes UI based on current state. Displays buttons for ALL transitions
        /// defined in the graph. Also handles the "Generating" state.
        /// </summary>
        private void UpdateUI()
        {
            // Handle Generating State
            if (isGeneratingGraph)
            {
                lblStatus.Text = "Generating graph, please wait...";
                btnLoadGraph.Enabled = false;
                genGraphBtn.Enabled = false; // Assuming the button is named btnGenerateGraph
                txtDescription.Enabled = false;
                lstMaterials.Enabled = false;
                flpActions.Enabled = false;
                flpActions.Controls.Clear(); // Clear actions while generating
                lblStateId.Text = "(Generating...)";
                txtDescription.Text = string.Empty;
                lstMaterials.Items.Clear();
                return; // Don't process further if generating
            }

            // --- Enable/Disable Core Controls based on whether a graph is loaded ---
            bool graphLoaded = currentGameGraph != null && currentNode != null;
            txtDescription.Enabled = graphLoaded;
            lstMaterials.Enabled = graphLoaded;
            flpActions.Enabled = graphLoaded;
            btnLoadGraph.Enabled = !isGeneratingGraph; // Can always load unless generating
            genGraphBtn.Enabled = !isGeneratingGraph; // Can always generate unless generating

            // --- Clear Previous State ---
            txtDescription.Text = string.Empty;
            lblStateId.Text = "(No graph loaded)";
            lstMaterials.Items.Clear();
            flpActions.Controls.Clear(); // Remove old buttons

            // --- Handle No Graph/Node State (After generation check) ---
            if (!graphLoaded)
            {
                if (string.IsNullOrEmpty(lblStatus.Text) || lblStatus.Text.StartsWith("Graph") || lblStatus.Text.Contains("successfully")) // Avoid overwriting specific messages like errors or success
                {
                     lblStatus.Text = "Please load or generate a graph file to begin.";
                }
                return;
            }

             // --- Display Current Node Info (Graph is loaded) ---
            lblStateId.Text = currentNode.NodeId ?? "N/A";
            txtDescription.Text = currentNode.Description ?? "No description.";

            // --- Populate Materials ---
            if (currentNode.MaterialsPresent != null)
            {
                lstMaterials.Items.AddRange(currentNode.MaterialsPresent.ToArray());
            }

            // --- Handle Final State ---
            if (currentNode.IsFinal)
            {
                lblStatus.Text = "Experiment Complete! (Final State Reached)";
                flpActions.Enabled = false; // Disable actions panel in final state
                Label finalLabel = new Label { Text = "--- FINAL STATE ---", AutoSize = true, ForeColor = Color.Green, Padding = new Padding(5) };
                flpActions.Controls.Add(finalLabel);
                return; // Don't add action buttons
            }

            // --- Create Buttons for ALL Transitions ---
            var allTransitions = currentGameGraph.Transitions ?? Enumerable.Empty<Transition>();

            if (allTransitions.Any())
            {
                foreach (var transition in allTransitions)
                {
                    Button actionButton = new Button
                    {
                        Text = transition.Action?.GetActionDescription() ?? "Perform Action",
                        Tag = transition,
                        AutoSize = true,
                        MinimumSize = new Size(100, 0),
                        Margin = new Padding(3)
                    };
                    actionButton.Click += ActionButton_Click;
                    flpActions.Controls.Add(actionButton);
                }
                // Update status only if it wasn't just set by LoadGraphFromFile
                 if (!lblStatus.Text.StartsWith("Graph '")) {
                    lblStatus.Text = $"Current State: {currentNode.NodeId}. Select the correct action to proceed.";
                 }
            }
            else
            {
                // This state might be final or just have no outgoing transitions defined
                 if (!currentNode.IsFinal) // Only show this if not already marked final
                 {
                     lblStatus.Text = $"State: {currentNode.NodeId}. No transitions defined from this state.";
                     flpActions.Enabled = false;
                     Label noActionsLabel = new Label
                     {
                         Text = "--- NO TRANSITIONS DEFINED ---",
                         AutoSize = true,
                         Padding = new Padding(5),
                         ForeColor = Color.Red
                     };
                     flpActions.Controls.Add(noActionsLabel);
                 }
            }
        }


        /// <summary>
        /// Handles the click event for the graph generation button.
        /// Assumes the button is named 'btnGenerateGraph' in the designer.
        /// </summary>
        private async void btnGenerateGraph_Click(object sender, EventArgs e) // Changed name and made async
        {
            isGeneratingGraph = true;
            UpdateUI(); // Update UI to show "Generating..." state and disable controls

            string generatedFilePath = null; // To store the path if generation succeeds

            try
            {
                // *** Execute the delegate asynchronously ***
                // Add arguments here if needed, otherwise defaults from GetGraph.cs are used.
                // Example: string filePath = await graphDelegate("My Experiment", "[...]", "[...]");
                generatedFilePath = await graphDelegate(); // Uses defaults

                // --- Success ---
                isGeneratingGraph = false; // Reset flag *before* loading
                lblStatus.Text = $"Graph generated successfully: {Path.GetFileName(generatedFilePath)}. Loading...";
                // UpdateUI(); // Optional: Briefly show status before loading starts

                // Automatically load the generated graph
                LoadGraphFromFile(generatedFilePath);
                // LoadGraphFromFile calls UpdateUI itself upon success/failure
            }
            catch (HttpRequestException httpEx)
            {
                // Handle network/server errors
                MessageBox.Show($"Failed to generate graph. Network or Server error:\n{httpEx.Message}\nURL: {httpEx.Source}", // Source might contain URL depending on .NET version/HttpClient usage
                                "Generation Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "Error generating graph (Network/Server).";
            }
            catch (IOException ioEx)
            {
                // Handle file writing errors
                MessageBox.Show($"Failed to save generated graph file:\n{ioEx.Message}",
                                "Generation Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "Error generating graph (File write failed).";
            }
            catch (Exception ex)
            {
                // Handle other unexpected errors during generation
                MessageBox.Show($"An unexpected error occurred during graph generation:\n{ex.ToString()}",
                                "Generation Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "Error generating graph (Unexpected).";
            }
            finally
            {
                // --- Always run regardless of success or failure ---
                if (isGeneratingGraph) // Check if it's still true (meaning an error occurred before setting it false)
                {
                     isGeneratingGraph = false;
                }

                // Ensure UI is updated to reflect the end of generation,
                // especially if loading failed or an error occurred.
                // If LoadGraphFromFile was successful, it already called UpdateUI.
                // If generation or loading failed, call UpdateUI here to re-enable controls
                // and clear the "Generating" state.
                 if (generatedFilePath == null || currentGameGraph == null) // Call if generation failed OR loading failed
                 {
                     UpdateUI();
                 }
            }
        }

        // You need to rename 'button1' in your Form Designer to 'btnGenerateGraph'
        // or change the event handler name above ('btnGenerateGraph_Click') to match 'button1_Click'.
        // Make sure the 'Generate Graph' button in the UI calls this specific handler.

    } // End Class Form1
} // End Namespace