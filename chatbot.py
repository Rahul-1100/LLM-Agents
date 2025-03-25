from typing import Annotated, Sequence, TypedDict, Union, Dict, Optional, Literal
from langchain_core.messages import BaseMessage, ToolMessage, SystemMessage, HumanMessage
from langgraph.graph.message import add_messages
from langchain_community.tools import DuckDuckGoSearchResults
from langchain_community.utilities import DuckDuckGoSearchAPIWrapper
from langchain_ollama import ChatOllama
from langchain_core.runnables import RunnableConfig
from langgraph.graph import StateGraph, END, START
from pydantic import BaseModel, Field
import json, os, re

# Agent state definition
class AgentState(TypedDict):
    messages: Annotated[Sequence[BaseMessage], add_messages]
    generated_exps: dict
    exp_title: str
    reward: Union[int, float]
    comment: str
    Satisfied: bool
    final_formatted: dict


# Initialize DuckDuckGo search tool
wrapper = DuckDuckGoSearchAPIWrapper(max_results=3)
search = DuckDuckGoSearchResults(api_wrapper=wrapper, output_format='list')
tools = [search]

# Initialize the model
model = ChatOllama(model="qwen2.5:32b", base_url="192.168.23.138:11439")
model = model.bind_tools(tools)

# System message prompt
system_message = '''You are a Lab Assistant designed to assist with scientific experiments. Your task is to provide:

- Experiment Materials: A list of required materials and equipment.
- Experiment Steps: A detailed step-by-step guide for conducting the experiment.
- Safety Procedures: Essential precautions to ensure a safe experiment.

Before generating the final response, you will:
- Search the query using DuckDuckGo to gather up-to-date and relevant information.
- Analyze the results to determine if the information is complete.
- Enhance the response by integrating the best available knowledge before providing the final answer.'''

# Tool processing function
def tool_node(state: AgentState):
    last_message = state["messages"][-1]
    outputs = []
    
    for tool_call in last_message.tool_calls:
        if tool_call["name"] == 'duckduckgo_results_json':
            tool_result = search.invoke(tool_call["args"])
            return {"messages": str(tool_result)}
    
    return {"messages": outputs}

# Model invocation function
def call_model(state: AgentState, config: RunnableConfig):
    system_prompt = SystemMessage(system_message)
    response = model.invoke([system_prompt] + state["messages"], config)
    return {"messages": [response]}

# Conditional edge function
def should_continue(state: AgentState):
    return "continue" if state["messages"][-1].tool_calls else "end"

# Workflow setup
workflow = StateGraph(AgentState)
workflow.add_node("agent", call_model)
workflow.add_node("tools", tool_node)
workflow.set_entry_point("agent")
workflow.add_conditional_edges("agent", should_continue, {"continue": "tools", "end": END})
workflow.add_edge("tools", "agent")
graph = workflow.compile()

# Evaluation score model
class Score(BaseModel):
    score1: float = Field(le=1.0, ge=0.0,description="probability of getting the score of 1")
    score2: float = Field(le=1.0, ge=0.0,description="probability of getting the score of 1")
    score3: float = Field(le=1.0, ge=0.0,description="probability of getting the score of 1")
    score4: float = Field(le=1.0, ge=0.0,description="probability of getting the score of 1")
    score5: float = Field(le=1.0, ge=0.0,description="probability of getting the score of 1")

# Evaluation output model
class JsonnedOutput(BaseModel):
    Accuracy: Score = Field(...,description="ratings of the Accuracy in a probabilstic distribution manner")
    Completeness: Score = Field(...,description="ratings of the Completeness in a probabilstic distribution manner")
    Clarity: Score = Field(...,description="ratings of the Clarity in a probabilstic distribution manner")
    Safety:Score = Field(...,description="ratings of the Safety in a probabilstic distribution manner")
    Comment:str = Field(...,description="give a feedback on how to improve the scores")
    Satisfied:bool = Field(...,description="are you satisfied with the experiment return True or False")

class State(TypedDict):
    input: str
    output:Union[JsonnedOutput,None]

formatter = model.with_structured_output(JsonnedOutput, method='json_mode')

# Generate evaluation prompt
def generate_evaluation_prompt(ground_truth: Dict, test_procedure: str) -> str:
    return f"""
        You are an expert evaluator assessing the accuracy, completeness, clarity, and safety of a scientific experiment procedure.

        ### **Task**
        Compare the **Test Procedure** against the **Ground Truth** and evaluate it across four key dimensions. Provide a **probability distribution over scores [1,2,3,4,5]** for each category.

        ### **Evaluation Criteria**
        1. **Accuracy**: Are the components and steps correct?
        2. **Completeness**: Are all critical steps present?
        3. **Clarity**: Is the procedure easy to follow?
        4. **Safety**: Are safety precautions included?

        ### **Response Format (Strict JSON)**
        ```json
        {{
            "Accuracy": {{ "score1": p1, "score2": p2, "score3": p3, "score4": p4, "score5": p5 }},
            "Completeness": {{ "score1": p1, "score2": p2, "score3": p3, "score4": p4, "score5": p5 }},
            "Clarity": {{ "score1": p1, "score2": p2, "score3": p3, "score4": p4, "score5": p5 }},
            "Safety": {{ "score1": p1, "score2": p2, "score3": p3, "score4": p4, "score5": p5 }},
            "Comment: <<comment to imporve the score>>
        }}
        Each probability value must be between 0 and 1 and sum to 1.

        ### **Ground Truth**
        {json.dumps(ground_truth, indent=4)}

        ### **Test Procedure**
        {test_procedure}
        """

def generate_graph_evaluation(state:State,config:RunnableConfig):
    # print(state["input"])
    response = model.invoke(state["input"],config)
    # print("----------------------------->",response)
    formatted_response = formatter.invoke(f"based on the {response.content} only output probabilities of each criteria and Leave everything. OUTPUT in the following json manner {{'Accuracy': {{ 'score1': p1, 'score2': p2, 'score3': p3, 'score4': p4, 'score5': p5 }}, 'Completeness': {{ 'score1': p1, 'score2': p2, 'score3': p3, 'score4': p4, 'score5': p5 }}, 'Clarity': {{ 'score1': p1, 'score2': p2, 'score3': p3, 'score4': p4, 'score5': p5 }},'Safety': {{ 'score1': p1, 'score2': p2, 'score3': p3, 'score4': p4, 'score5': p5 }},'Comment':<<comment to improve the score>>,'Satisfied:<<are you satisfied with the response or not return True or False }} but ensure that the json format is correct")
    # print(formatted_response)
    return {"output":{"Accuracy": formatted_response.Accuracy, "Completeness": formatted_response.Completeness,"Clarity":formatted_response.Clarity,"Safety": formatted_response.Safety,"Comment":formatted_response.Comment,"Satisfied":formatted_response.Satisfied}}


workflow2 = StateGraph(state_schema=State)
workflow2.add_node("evaluator",generate_graph_evaluation)
workflow2.add_edge(START,"evaluator")
workflow2.add_edge("evaluator",END)
eval_graph = workflow2.compile()


# Evaluation function
def evaluate_experiment(ground_truth: Dict, test_procedure: str):
    prompt = generate_evaluation_prompt(ground_truth, test_procedure)


    event = eval_graph.invoke({"input": prompt, "output": None })
        # print(event)
    # Debug: Print the raw response to see what the model is returning
    try:
        return event['output']
    except KeyError:
        return event

# File operations
def load_json(filepath: str) -> Dict:
    with open(filepath, "r", encoding="utf-8") as file:
        return json.load(file)

def run_evaluation(ground_truth_path: str, results_path: Union[str, dict], plots: dict, mode: Literal["feedback", "judge"] = "judge"):
    ground_truth_data = load_json(ground_truth_path)
    ground_truth_dict = {exp["title"]: exp for exp in ground_truth_data["experiments"]}
    llm_generated_results = load_json(results_path)["experiments"] if mode == "judge" else results_path

    for chosen_exps in llm_generated_results:
        for title, test_procedure in chosen_exps.items():
            ground_truth = ground_truth_dict.get(title)
            if ground_truth:
                result = evaluate_experiment(ground_truth, test_procedure)
                print(result)
                comment = result.pop('Comment')
                satisfied = result.pop('Satisfied')
                for key, values in result.items():
                    score = sum(i * getattr(values, f'score{i}') for i in range(1, 6))
                    plots[key].append(score)
    return plots if mode == "judge" else (4.0, comment, satisfied)

# Experiment generator
def run_generator(state: AgentState):
    response = graph.invoke({"messages": state["messages"][-1]})
    return {"generated_exps": {state["exp_title"]: response["messages"][-1].content}}

def run_evaluator(state: AgentState):
    gt = "non_code_files/experiments_2.json"
    plots = {'Accuracy': [], 'Clarity': [], 'Completeness': [], 'Safety': []}
    response, comment, satisfied = run_evaluation(gt, [state["generated_exps"]], plots, mode="feedback")
    return {"reward": response, "messages": [str(state['generated_exps']) + '\n\ncomment:'+ comment], "Satisfied": satisfied}
finish_on_next = False
def continue_condition(state: AgentState):
    global finish_on_next
    if not state["Satisfied"] and not finish_on_next:
        return "continue"
    elif state["Satisfied"] and not finish_on_next:
        finish_on_next = True
        return 'continue'
    else: 
        finish_on_next = False
        return 'finish'
    # return "continue" if not state["Satisfied"] else "finish"

# Feedback workflow setup
feedback_workflow = StateGraph(AgentState)
feedback_workflow.add_node("generator", run_generator)
feedback_workflow.add_node("evaluator", run_evaluator)
feedback_workflow.set_entry_point("generator")
feedback_workflow.add_conditional_edges("evaluator", continue_condition, {"continue": "generator", "finish": END})
feedback_workflow.add_edge("generator", "evaluator")
feedback_graph = feedback_workflow.compile()

def call_custom_model(exp: str, history):
    query = f"What should be the required materials and experiment procedure for the following lab experiment ({exp})?"
    inputs = {"messages": [("user", query)], "exp_title": exp}
    event = feedback_graph.invoke(inputs)
    return event['generated_exps'][exp]

# call_custom_model("To verify the relationship between voltage (V), current (I), and resistance (R) in an electrical circuit, as expressed by Ohm's Law: V=IRV = IRV=IR", [])

import gradio as gr

gr.ChatInterface(fn=call_custom_model,type="messages").launch()