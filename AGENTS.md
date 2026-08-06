# Repository collaboration instructions

## Development workflow

- Do not use subagents, parallel-agent workflows, or delegated reviews unless the
  user explicitly requests them for the current task.
- Do not stage, commit, push, create pull requests, or otherwise publish changes.
  Leave changes unstaged so the user can review and perform git operations manually.
- During implementation, run the smallest focused test command needed for the
  changed behavior. Run the full relevant test suite only once before handoff.
- Do not repeat full builds, test suites, or review cycles unless the previous run
  failed or the user explicitly requests another run.
- Give the user a concise manual test checklist for UI or device behavior that
  cannot be verified reliably through automated tests.
- Ask before dependency installation or restoration, migrations, destructive
  operations, lengthy automation, or work that is likely to require repeated
  approval prompts.
- At handoff, summarize the unstaged files, automated checks run, known limitations,
  and the exact manual or git steps left for the user.
