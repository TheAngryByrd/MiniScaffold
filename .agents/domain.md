# Domain documents

Engineering skills use these documents when they explore the repository.

## Before exploration

Read these paths when they exist:

- `.agents/CONTEXT.md`
- Relevant ADRs under `.agents/adr/`

Proceed silently when a path does not exist. Do not request domain documents before work can start.

The `domain-modeling` skill creates these documents when it resolves domain terms or decisions.

## File structure

This repository uses a single-context layout:

```text
/
├── .agents/
│   ├── CONTEXT.md
│   ├── domain.md
│   ├── issue-tracker.md
│   ├── triage-labels.md
│   └── adr/
│       ├── 0001-example-decision.md
│       └── 0002-example-decision.md
└── source files
```

Only the three configuration files must exist after setup. Create `CONTEXT.md` and ADRs when domain work requires them.

## Use glossary terms

Use the terms from `.agents/CONTEXT.md` in issues, proposals, hypotheses, and test names.

Do not replace a defined term with a synonym. Record a missing domain term for later domain-modeling work.

## Report ADR conflicts

Report any output that conflicts with an existing ADR. Name the ADR and describe the conflict.
