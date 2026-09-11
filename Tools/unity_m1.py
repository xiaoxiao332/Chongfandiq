"""Use the existing local Coplay bridge. Arguments: tool JSON, or read URI."""
import asyncio, json, sys, httpx
from mcp import ClientSession
from mcp.client.streamable_http import streamable_http_client

async def main():
    async with httpx.AsyncClient(trust_env=False, timeout=90) as client:
        async with streamable_http_client('http://127.0.0.1:8080/mcp', http_client=client) as (r,w,_):
            async with ClientSession(r,w) as session:
                await session.initialize()
                if sys.argv[1]=='read':
                    result=await session.read_resource(sys.argv[2])
                elif sys.argv[1]=='schema':
                    result=await session.list_tools()
                    print(json.dumps([t.model_dump() for t in result.tools if t.name==sys.argv[2]],ensure_ascii=False)); return
                else:
                    args=json.loads(sys.stdin.read()) if len(sys.argv)<3 else json.loads(sys.argv[2])
                    result=await session.call_tool(sys.argv[1],args)
                print(result.model_dump_json())
asyncio.run(main())
