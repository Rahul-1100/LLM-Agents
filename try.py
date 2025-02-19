import os
import json
import asyncio
from pydantic import BaseModel, Field
from crawl4ai import AsyncWebCrawler, CrawlerRunConfig
from crawl4ai.extraction_strategy import LLMExtractionStrategy
from typing import Dict
from crawl4ai import BrowserConfig,CacheMode

class OpenAIModelFee(BaseModel):
    model_name: str = Field(..., description="Name of the OpenAI model.")
    input_fee: str = Field(..., description="Fee for input token for the OpenAI model.")
    output_fee: str = Field(
        ..., description="Fee for output token for the OpenAI model."
    )

class ScrappedData(BaseModel):
    title: str = Field(...,description="The title of the webpage or the blog")
    content: str = Field(...,description="Entire Main content of the webpage which is relevant to the title")

async def extract_structured_data_using_llm(
    provider: str, api_token: str = None, extra_headers: Dict[str, str] = None
):
    print(f"\n--- Extracting Structured Data with {provider} ---")

    if api_token is None and provider != "ollama":
        print(f"API token is required for {provider}. Skipping this example.")
        return

    browser_config = BrowserConfig(headless=True)

    extra_args = {"temperature": 0, "top_p": 0.9, "max_tokens": 2000}
    if extra_headers:
        extra_args["extra_headers"] = extra_headers

    crawler_config = CrawlerRunConfig(
        cache_mode=CacheMode.BYPASS,
        word_count_threshold=1,
        page_timeout=80000,
        extraction_strategy=LLMExtractionStrategy(
            provider=provider,
            api_token=api_token,
            schema=ScrappedData.model_json_schema(),
            extraction_type="schema",
            instruction="""From the crawled content, extract all the Main content or the body content which any user would read and leave none""",
            extra_args=extra_args,
            base_url="http://192.168.23.138:11439"
        ),
    )

    async with AsyncWebCrawler(config=browser_config) as crawler:
        result = await crawler.arun(
            url="https://www.millerwelds.com/resources/article-library/understanding-the-basics-of-mig-welding-for-mild-steel", config=crawler_config
        )
        print(result.extracted_content)

if __name__ == "__main__":
    # Use ollama with llama3.3
    asyncio.run(
        extract_structured_data_using_llm(
            provider="ollama/jacob-ebey/phi4-tools:latest", api_token="no-token"
        )
    )

    # asyncio.run(
    #     extract_structured_data_using_llm(
    #         provider="openai/gpt-4o", api_token=os.getenv("OPENAI_API_KEY")
    #     )
    # )