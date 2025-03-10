import requests
import json
import time
import sys
import base64
import os
from typing import Dict, Any
from pydantic import BaseModel,Field
from typing import Union

class crawlerSchema(BaseModel):
    title: str = Field(..., description="The title of the article")
    article: str = Field(..., description="rephrase the article so that it covers every part of the topic in a concise manner in about 500 words")
    # main_topics: str = Field(..., description="Main topics or themes discussed in the article")
    materials: str = Field( description="extract all the materials used in the experiment")
    steps: str = Field( description="exctract Step by step guide for conducting the experiment")
    safety_procedures: str = Field( description="Safety precautions for the experiment")

class Crawl4AiTester:
    def __init__(self, base_url: str = "http://192.168.23.138:11235", api_token: str = None):
        self.base_url = base_url
        self.api_token = (
            api_token or os.getenv("CRAWL4AI_API_TOKEN") or "test_api_code"
        )  # Check environment variable as fallback
        self.headers = (
            {"Authorization": f"Bearer {self.api_token}"} if self.api_token else {}
        )

    def submit_and_wait(
        self, request_data: Dict[str, Any], timeout: int = 300
    ) -> Dict[str, Any]:
        # Submit crawl job
        response = requests.post(
            f"{self.base_url}/crawl", json=request_data, headers=self.headers
        )
        if response.status_code == 403:
            raise Exception("API token is invalid or missing")
        task_id = response.json()["task_id"]
        print(f"Task ID: {task_id}")

        # Poll for result
        start_time = time.time()
        while True:
            if time.time() - start_time > timeout:
                raise TimeoutError(
                    f"Task {task_id} did not complete within {timeout} seconds"
                )

            result = requests.get(
                f"{self.base_url}/task/{task_id}", headers=self.headers
            )
            status = result.json()

            if status["status"] == "failed":
                print("Task failed:", status.get("error"))
                raise Exception(f"Task failed: {status.get('error')}")

            if status["status"] == "completed":
                return status

            time.sleep(2)

    def submit_sync(self, request_data: Dict[str, Any]) -> Dict[str, Any]:
        response = requests.post(
            f"{self.base_url}/crawl_sync",
            json=request_data,
            headers=self.headers,
            timeout=60,
        )
        if response.status_code == 408:
            raise TimeoutError("Task did not complete within server timeout")
        response.raise_for_status()
        return response.json()

    def crawl_direct(self, request_data: Dict[str, Any]) -> Dict[str, Any]:
        """Directly crawl without using task queue"""
        response = requests.post(
            f"{self.base_url}/crawl_direct", json=request_data, headers=self.headers
        )
        response.raise_for_status()
        return response.json()


def test_docker_deployment(version="basic"):
    tester = Crawl4AiTester(
        base_url="http://192.168.23.138:11235",
        # base_url="https://api.crawl4ai.com" # just for example
        # api_token="test" # just for example
    )
    print(f"Testing Crawl4AI Docker {version} version")

    # Health check with timeout and retry
    max_retries = 5
    for i in range(max_retries):
        try:
            health = requests.get(f"{tester.base_url}/health", timeout=10)
            print("Health check:", health.json())
            break
        except requests.exceptions.RequestException:
            if i == max_retries - 1:
                print(f"Failed to connect after {max_retries} attempts")
                sys.exit(1)
            print(f"Waiting for service to start (attempt {i+1}/{max_retries})...")
            time.sleep(5)

    # Test cases based on version
    # test_basic_crawl_direct(tester)
    # test_basic_crawl(tester)
    # test_basic_crawl(tester)
    # test_basic_crawl_sync(tester)

    # if version in ["full", "transformer"]:
    #     test_cosine_extraction(tester)

    # test_js_execution(tester)
    # test_css_selector(tester)
    # test_structured_extraction(tester)
    # test_llm_extraction(tester)
    print(test_llm_with_ollama(tester,url="https://techcommunity.microsoft.com/blog/machinelearningblog/phi-4-quantization-and-inference-speedup/4360047"))
    




def test_llm_with_ollama(tester: Crawl4AiTester,url:str,schema:Union[dict,None]=None):
    # print("\n=== Testing LLM with Ollama ===")
    if schema is None:
        schema = {
            "type": "object",
            "properties": {
                "article_title": {
                    "type": "string",
                    "description": "The main title of the news article",
                },
                "summary": {
                    "type": "string",
                    "description": "A brief summary of the article content",
                },
                "main_topics": {
                    "type": "array",
                    "items": {"type": "string"},
                    "description": "Main topics or themes discussed in the article",
                },
            },
        }

    request = {
        "urls": url,
        "priority": 8,
        "extraction_config": {
            "type": "llm",
            "params": {
                "provider": "jacob-ebey/phi4-tools:latest",
                "base_url":"http://192.168.23.138:11439",
                "schema": schema,
                "extraction_type": "schema",
                "instruction": "Extract the main article information including title, summary and if the article is about an experiment then extract materials, steps and safety procedures.",
            },
        },
        "extra": {"word_count_threshold": 1},
        "crawler_params": {"verbose": True},
    }

    try:
        result = tester.submit_and_wait(request)
        extracted = json.loads(result["result"]["extracted_content"])
        return extracted
        assert result["result"]["success"]
    except Exception as e:
        print(f"Ollama extraction test failed: {str(e)}")

crawler_result = test_llm_with_ollama(tester=Crawl4AiTester(), url="https://techcommunity.microsoft.com/blog/machinelearningblog/phi-4-quantization-and-inference-speedup/4360047", schema=crawlerSchema.model_json_schema()['properties'])

print(crawler_result)