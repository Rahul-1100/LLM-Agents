// File: GraphModels.cs
using System.Text.Json.Serialization;
using System.Collections.Generic;
using System.Text.Json; // Needed for JsonElement

public class GraphContainer
{
    [JsonPropertyName("output")]
    public GraphData Output { get; set; }
}

public class GraphData
{
    [JsonPropertyName("nodes")]
    public List<GraphNode> Nodes { get; set; } = new List<GraphNode>();

    [JsonPropertyName("transitions")]
    public List<Transition> Transitions { get; set; } = new List<Transition>();

    [JsonPropertyName("available_vr_actions")]
    public List<string> AvailableVrActions { get; set; } = new List<string>();

    [JsonPropertyName("available_vr_materials")]
    public List<string> AvailableVrMaterials { get; set; } = new List<string>();

    [JsonPropertyName("missing_materials")]
    public List<string> MissingMaterials { get; set; } = new List<string>();

    [JsonPropertyName("missing_actions")]
    public List<string> MissingActions { get; set; } = new List<string>();
}

public class GraphNode
{
    [JsonPropertyName("node_id")]
    public string NodeId { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; }

    [JsonPropertyName("materials_present")]
    public List<string> MaterialsPresent { get; set; } = new List<string>();

    [JsonPropertyName("properties")]
    public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();

    [JsonPropertyName("is_initial")]
    public bool IsInitial { get; set; }

    [JsonPropertyName("is_final")]
    public bool IsFinal { get; set; }

    [JsonPropertyName("corresponds_to_step")]
    public int? CorrespondsToStep { get; set; }
}

public class Transition
{
    [JsonPropertyName("from_node_id")]
    public string FromNodeId { get; set; }

    [JsonPropertyName("to_node_id")]
    public string ToNodeId { get; set; }

    [JsonPropertyName("action")]
    public ActionDetail Action { get; set; }

    [JsonPropertyName("materials_used")]
    public List<string> MaterialsUsed { get; set; } = new List<string>();

    [JsonPropertyName("corresponds_to_step")]
    public int? CorrespondsToStep { get; set; }

    [JsonPropertyName("duration_estimate")]
    public int? DurationEstimate { get; set; }

    [JsonPropertyName("safety_notes")]
    public string SafetyNotes { get; set; }
}

public class ActionDetail
{
    [JsonPropertyName("action_name")]
    public string ActionName { get; set; }

    [JsonPropertyName("parameters")]
    public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();

    // Helper method to generate a user-friendly description of the action
    public string GetActionDescription()
    {
        string description = ActionName ?? "Unnamed Action";
        if (Parameters != null && Parameters.Count > 0)
        {
            if (Parameters.TryGetValue("component", out object component) && component != null)
            {
                description += $" {FormatParameterValue(component)}";
            }
            else if (Parameters.TryGetValue("components", out object components) && components is JsonElement componentsElement && componentsElement.ValueKind == JsonValueKind.Array)
            {
                description += $" multiple components ({componentsElement.GetArrayLength()})";
            }
             else if (Parameters.TryGetValue("components", out object singleComponent) && singleComponent != null) // Handle case where "components" might be single item sometimes
            {
                description += $" {FormatParameterValue(singleComponent)}";
            }
            else if (Parameters.TryGetValue("object", out object obj) && obj != null)
            {
                description += $" {FormatParameterValue(obj)}";
            }

            if (Parameters.TryGetValue("to", out object to) && to != null)
            {
                description += $" to {FormatParameterValue(to)}";
            }
             if (Parameters.TryGetValue("on", out object on) && on != null)
            {
                description += $" on {FormatParameterValue(on)}";
            }
            if (Parameters.TryGetValue("between", out object between) && between != null)
            {
                 description += $" between points"; // Simplified
            }
             if (Parameters.TryGetValue("location", out object location) && location != null)
            {
                description += $" at {FormatParameterValue(location)}";
            }
             if (Parameters.TryGetValue("using", out object usingParam) && usingParam != null)
            {
                description += $" using {FormatParameterValue(usingParam)}";
            }
             if (Parameters.TryGetValue("at", out object atParam) && atParam != null)
            {
                description += $" at {FormatParameterValue(atParam)}";
            }
             if (Parameters.TryGetValue("device", out object deviceParam) && deviceParam != null)
            {
                description += $" with {FormatParameterValue(deviceParam)}";
            }
             if (Parameters.TryGetValue("from", out object fromParam) && fromParam != null) // Added for completeness
            {
                description += $" from {FormatParameterValue(fromParam)}";
            }
        }
        // Basic Capitalization
        return string.IsNullOrWhiteSpace(description) ? "Action" : char.ToUpper(description[0]) + description.Substring(1);
    }

    // Helper to handle potential JsonElement values in parameters
    private string FormatParameterValue(object value)
    {
        if (value is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.String) return element.GetString() ?? "value";
            if (element.ValueKind == JsonValueKind.Array) return $"[{element.GetArrayLength()} items]";
             return element.ToString(); // Fallback for other types
        }
        return value?.ToString() ?? "value";
    }
}