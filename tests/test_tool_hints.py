from __future__ import annotations

from cmdai.tool_hints import infer_tool_hints


def test_azure_subscription_routes_to_az_account() -> None:
    hints = infer_tool_hints("with Azure CLI, list my subscriptions", "powershell")

    assert hints[0].tool == "az account"


def test_azure_resource_groups_routes_to_az_group() -> None:
    hints = infer_tool_hints("show Azure resource groups", "powershell")

    assert hints[0].tool == "az group"


def test_git_submitters_routes_to_git_log() -> None:
    hints = infer_tool_hints("show me the list of submitters for the last 5 commits", "powershell")

    assert hints[0].tool == "git log"


def test_git_repo_status_routes_to_git_status() -> None:
    hints = infer_tool_hints(
        "I want to see the status of the current local git repo like which branch I am on, and files not yet committed",
        "powershell",
    )

    assert hints[0].tool == "git status"


def test_sorted_directories_query_hints_get_childitem_and_sort_object() -> None:
    hints = infer_tool_hints("show directories sorted by datetime descending order", "powershell")

    tools = {hint.tool for hint in hints}
    assert "get-childitem" in tools
    assert "sort-object" in tools


def test_list_files_and_directories_routes_to_get_childitem() -> None:
    hints = infer_tool_hints("list files and directories", "powershell")

    assert hints[0].tool == "get-childitem"

