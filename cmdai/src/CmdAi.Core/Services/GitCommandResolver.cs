using CmdAi.Core.Interfaces;
using CmdAi.Core.Models;
using System.Text.RegularExpressions;

namespace CmdAi.Core.Services;

public class GitCommandResolver : ICommandResolver
{
    private readonly List<CommandPattern> _patterns;

    public GitCommandResolver()
    {
        _patterns = new List<CommandPattern>
        {
            new(@"\b(status|check\s+status|what.*status|show.*status)\b", "git status", "Show the working tree status"),
            new(@"\b(undo.*last.*commit|revert.*commit)\b", "git reset --soft HEAD~1", "Undo the last commit while keeping changes staged"),
            new(@"\b(add|stage)\s+all\b", "git add .", "Add all changes to the index"),
            new(@"\b(add|stage)\s+(.*)", "git add $2", "Add file contents to the index"),
            new(@"\b(commit)(?:\s+.*)?", "git commit", "Record changes to the repository"),
            new(@"\b(push)(?:\s+.*)?", "git push", "Update remote refs along with associated objects"),
            new(@"\b(pull)(?:\s+.*)?", "git pull", "Fetch from and integrate with another repository or local branch"),
            new(@"\b(log|history)\b", "git log --oneline", "Show commit logs"),
            new(@"\b(diff|changes|what.*changed)\b", "git diff", "Show changes between commits, commit and working tree, etc"),
            new(@"\b(branch|branches)\b", "git branch", "List, create, or delete branches"),
            new(@"\b(checkout|switch)\s+(.*)", "git checkout $2", "Switch branches or restore working tree files"),
            new(@"\b(merge)\s+(.*)", "git merge $2", "Join two or more development histories together"),
            new(@"\b(stash)(?:\s+.*)?", "git stash", "Stash the changes in a dirty working directory away"),
            new(@"\b(remote|remotes)\b", "git remote -v", "Show remote repositories"),
            new(@"\b(init|initialize)\b", "git init", "Create an empty Git repository or reinitialize an existing one")
        };
    }

    public bool CanResolve(string tool) => tool.Equals("git", StringComparison.OrdinalIgnoreCase);

    public Task<CommandResult?> ResolveCommandAsync(CommandRequest request, CommandContext context)
    {
        if (!CanResolve(request.Tool))
            return Task.FromResult<CommandResult?>(null);

        var query = request.Query.ToLowerInvariant();

        foreach (var pattern in _patterns)
        {
            var match = pattern.Regex.Match(query);
            if (match.Success)
            {
                var command = pattern.Command;
                
                for (int i = 1; i < match.Groups.Count; i++)
                {
                    command = command.Replace($"${i}", match.Groups[i].Value.Trim());
                }

                var contextInfo = context.IsGitRepository ? null : "Warning: Not in a Git repository";
                
                return Task.FromResult<CommandResult?>(
                    new CommandResult(command, pattern.Description, true, contextInfo));
            }
        }

        return Task.FromResult<CommandResult?>(null);
    }

    private record CommandPattern(string Pattern, string Command, string Description)
    {
        public Regex Regex { get; } = new(Pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }
}