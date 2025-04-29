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
using System.Threading.Tasks;
using System.Windows.Forms;

// Ensure this namespace matches your project name
namespace GraphGameGUI
{
    public partial class Form1 : Form
    {
        // --- State Variables ---
        private GraphData? currentGameGraph; // Use the definition from GraphModels.cs
        private GraphNode? currentNode;      // Use the definition from GraphModels.cs

        // --- Constructor ---
        public Form1()
        {
            InitializeComponent();
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
                openFileDialog.InitialDirectory = Environment.CurrentDirectory;
                openFileDialog.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*";
                openFileDialog.FilterIndex = 1;
                openFileDialog.RestoreDirectory = true;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string filePath = openFileDialog.FileName;
                    LoadGraphFromFile(filePath);
                }
            }
        }

        /// <summary>
        /// Handles the Click event for *any* dynamically created action button.
        /// Retrieves the associated transition and moves the game to the next state
        /// ONLY IF the transition is valid for the CURRENT node.
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
                    // The button corresponding to a transition NOT starting from the current node was clicked.
                    // Do not change the state. Provide feedback.
                    lblStatus.Text = $"Action '{chosenTransition.Action?.GetActionDescription() ?? "Selected Action"}' is not valid from the current state '{currentNode.NodeId}'. Try a different action.";

                    // Optional: More intrusive MessageBox feedback
                    // MessageBox.Show($"The action '{chosenTransition.Action?.GetActionDescription() ?? "Selected Action"}' cannot be performed from state '{currentNode.NodeId}'.",
                    //                 "Invalid Action for Current State", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                string jsonString = File.ReadAllText(filePath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var graphContainers = JsonSerializer.Deserialize<List<GraphContainer>>(jsonString, options);

                if (graphContainers == null || graphContainers.Count == 0 || graphContainers[0].Output == null)
                {
                    throw new JsonException("No valid graph 'output' data found. Structure `[{\"output\": {...}}]` expected.");
                }

                currentGameGraph = graphContainers[0].Output;

                if (currentGameGraph.Nodes == null || !currentGameGraph.Nodes.Any()) {
                    throw new InvalidDataException("Graph data is missing 'nodes' or the list is empty.");
                }
                // Ensure Transitions list exists, even if empty, for iteration later
                currentGameGraph.Transitions ??= new List<Transition>();
                // Other lists from GraphData could be initialized here if needed by other logic

                currentNode = currentGameGraph.Nodes.FirstOrDefault(n => n.IsInitial);
                if (currentNode == null) {
                    throw new InvalidDataException("No node marked as 'is_initial: true' found.");
                }

                lblStatus.Text = $"Graph '{Path.GetFileName(filePath)}' loaded. Initial State: {currentNode.NodeId}";
                UpdateUI();
            }
            // --- Keep existing robust error handling ---
            catch (FileNotFoundException) { MessageBox.Show($"Error: File not found.\nPath: {filePath}", "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error); ResetGame(); }
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
            lblStatus.Text = "Please load a graph file to begin.";
            UpdateUI();
        }

        // --- UI Update Method ---

        /// <summary>
        /// Refreshes UI based on current state. Displays buttons for ALL transitions
        /// defined in the graph.
        /// </summary>
        private void UpdateUI()
        {
            // --- Clear Previous State ---
            txtDescription.Text = string.Empty;
            lblStateId.Text = "(No graph loaded)";
            lstMaterials.Items.Clear();
            flpActions.Controls.Clear(); // Remove old buttons

            // --- Handle No Graph/Node State ---
            if (currentGameGraph == null || currentNode == null)
            {
                txtDescription.Enabled = false;
                lstMaterials.Enabled = false;
                flpActions.Enabled = false;
                if (string.IsNullOrEmpty(lblStatus.Text) || lblStatus.Text.StartsWith("Graph"))
                {
                    lblStatus.Text = "Please load a graph file to begin.";
                }
                return;
            }

            // --- Enable Controls ---
            txtDescription.Enabled = true;
            lstMaterials.Enabled = true;
            flpActions.Enabled = true; // Panel is enabled, click handler decides if action is valid

            // --- Display Current Node Info ---
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
                return;
            }

            // --- Create Buttons for ALL Transitions ---
            // Iterate through ALL transitions defined in the graph data.
            // The AvailableVrActions list is ignored for button creation in this version.
            var allTransitions = currentGameGraph.Transitions ?? Enumerable.Empty<Transition>(); // Handle null

            if (allTransitions.Any())
            {
                // Create a button for every single transition
                foreach (var transition in allTransitions)
                {
                    Button actionButton = new Button
                    {
                        Text = transition.Action?.GetActionDescription() ?? "Perform Action", // Use helper
                        Tag = transition, // CRITICAL: Store the specific transition object
                        AutoSize = true,
                        MinimumSize = new Size(100, 0),
                        Margin = new Padding(3)
                        // Optional: Add visual cue if this transition is valid *right now*
                        // Use with caution as it gives away the answer.
                        // BackColor = (transition.FromNodeId == currentNode.NodeId) ? SystemColors.Info : SystemColors.Control,
                        // Font = new Font(this.Font, (transition.FromNodeId == currentNode.NodeId) ? FontStyle.Bold : FontStyle.Regular)
                    };
                    actionButton.Click += ActionButton_Click; // Attach the same handler to all
                    flpActions.Controls.Add(actionButton);
                }
                 // Update status to prompt the user to find the correct action among all displayed
                 lblStatus.Text = $"Current State: {currentNode.NodeId}. Select the correct action to proceed.";
            }
            else
            {
                // This case means the 'transitions' list in the JSON was empty or missing.
                lblStatus.Text = $"State: {currentNode.NodeId}. No transitions defined in the graph file.";
                flpActions.Enabled = false; // Disable panel if truly no actions possible

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

    } // End Class Form1
} // End Namespace