#!/usr/bin/env python3
import os
import sys
import json
import asyncio
import argparse
import io
import contextlib
from typing import Dict
from pydantic import BaseModel
from crawl4ai import AsyncWebCrawler, CrawlerRunConfig, BrowserConfig, CacheMode
from crawl4ai.extraction_strategy import LLMExtractionStrategy

async def extract_structured_data_using_llm(
    provider: str,
    base_url: str,
    system_prompt: str,
    schema: Dict,
    output_file: str,
    webpage_url: str,
    api_token: str = None,
    extra_headers: Dict[str, str] = None,
):
    print(f"\n--- Extracting Structured Data from {webpage_url} using {provider} ---")

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
            schema=schema,  # Use the schema from the provided file
            extraction_type="schema",
            instruction=system_prompt,  # Use the custom system prompt
            extra_args=extra_args,
            base_url=base_url,  # Use the custom base_url
        ),
    )

    async with AsyncWebCrawler(config=browser_config) as crawler:
        result = await crawler.arun(url=webpage_url, config=crawler_config)

    # Write the extracted content to the output file
    with open(output_file, "w", encoding="utf-8") as f:
        f.write(result.extracted_content)

def main():
    parser = argparse.ArgumentParser(description="Crawl a webpage and extract structured data using an LLM.")
    parser.add_argument("output_file", help="Path to save the extracted output.")
    parser.add_argument("webpage_url", help="The URL of the webpage to crawl.")
    parser.add_argument("--model", required=True, help="The model name to use for extraction.")
    parser.add_argument("--base_url", required=True, help="The base URL for the LLM service.")
    parser.add_argument("--system_prompt", required=True, help="The system prompt for extraction.")
    parser.add_argument("--schema_file", required=True, help="Path to a JSON file containing the schema.")

    args = parser.parse_args()

    # Load the schema from the provided JSON file
    with open(args.schema_file, "r", encoding="utf-8") as f:
        schema = json.load(f)

    # Run the async function
    asyncio.run(
        extract_structured_data_using_llm(
            provider=args.model,
            base_url=args.base_url,
            system_prompt=args.system_prompt,
            schema=schema,
            output_file=args.output_file,
            webpage_url=args.webpage_url,
            api_token="no-token",
        )
    )

if __name__ == "__main__":
    main()
