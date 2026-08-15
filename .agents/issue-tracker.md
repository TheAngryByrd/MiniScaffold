# Issue tracker: GitHub

Issues and specifications for this repository are GitHub issues. Use the `gh` CLI for all operations.

## Conventions

- Create an issue: `gh issue create --title "..." --body "..."`.
- Use a heredoc for a multiline body.
- Read an issue: `gh issue view <number> --comments`.
- Fetch labels and filter comments with `jq` when necessary.
- List issues: `gh issue list --state open --json number,title,body,labels,comments`.
- Add suitable `--label`, `--state`, and `--jq` options.
- Comment on an issue: `gh issue comment <number> --body "..."`.
- Add a label: `gh issue edit <number> --add-label "..."`.
- Remove a label: `gh issue edit <number> --remove-label "..."`.
- Close an issue: `gh issue close <number> --comment "..."`.

Run commands inside this clone. The `gh` CLI infers the repository from `git remote -v`.

## Pull requests as a triage surface

**PRs as a request surface: no.**

When this value is `yes`, pull requests use the same labels and states as issues.

- Read a pull request: `gh pr view <number> --comments`.
- Read its changes: `gh pr diff <number>`.
- List pull requests: `gh pr list --state open --json number,title,body,labels,author,authorAssociation,comments`.
- Keep external requests from `CONTRIBUTOR`, `FIRST_TIME_CONTRIBUTOR`, or `NONE`.
- Exclude requests from `OWNER`, `MEMBER`, or `COLLABORATOR`.
- Use `gh pr comment`, `gh pr edit`, and `gh pr close` for updates.

GitHub shares one number sequence for issues and pull requests. Test `gh pr view <number>` before `gh issue view <number>` when necessary.

## Publish to the issue tracker

Create a GitHub issue.

## Fetch a ticket

Run `gh issue view <number> --comments`.

## Wayfinding operations

The map is one issue with child issues as tickets.

- Label the map issue `wayfinder:map`.
- Label each child `wayfinder:<type>`.
- Valid types are `research`, `prototype`, `grilling`, and `task`.
- Use GitHub sub-issues when they are available.
- Otherwise, add children to a task list in the map.
- Add `Part of #<map>` to each fallback child.
- Use native issue dependencies for blocking relationships.
- Otherwise, add `Blocked by: #<number>` to the child body.
- Exclude assigned or blocked issues from the frontier.
- Claim work with `gh issue edit <number> --add-assignee @me`.
- Resolve work with a comment, issue closure, and a context pointer in the map.
