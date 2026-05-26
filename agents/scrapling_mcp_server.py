import argparse


def main() -> None:
    parser = argparse.ArgumentParser(description="Run the Scrapling MCP server as a standalone sidecar")
    parser.add_argument("--host", default="127.0.0.1", help="Host to bind the Scrapling MCP server")
    parser.add_argument("--port", type=int, default=8011, help="Port to bind the Scrapling MCP server")
    parser.add_argument(
        "--http",
        action="store_true",
        help="Serve the MCP transport over HTTP instead of stdio",
    )
    args = parser.parse_args()

    from scrapling.core.ai import ScraplingMCPServer

    server = ScraplingMCPServer()
    server.serve(http=args.http, host=args.host, port=args.port)


if __name__ == "__main__":
    main()
