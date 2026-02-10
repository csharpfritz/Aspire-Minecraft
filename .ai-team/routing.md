# Work Routing

How to decide who handles what.

## Routing Table

| Work Type | Route To | Examples |
|-----------|----------|----------|
| Architecture & scope | Rhodey | Project structure, NuGet strategy, API design, trade-offs |
| .NET/Aspire hosting | Shuri | Hosting extensions, resource builders, Docker config, NuGet packaging |
| RCON protocol | Shuri | RconClient, RconConnection, protocol parsing |
| Minecraft interactions | Rocket | Worker service, holograms, scoreboards, in-world display, Minecraft commands |
| OpenTelemetry | Shuri + Rocket | Metrics, tracing, structured logging |
| Code review | Rhodey | Review PRs, check quality, suggest improvements |
| Testing | Nebula | Write tests, find edge cases, verify fixes |
| Scope & priorities | Rhodey | What to build next, trade-offs, decisions |
| Documentation | Rhodey | README, API docs, usage guides |
| Blog posts & comms | Mantis | Release posts, changelogs, developer announcements |
| CI/CD & GitHub | Wong | Actions workflows, branch policies, NuGet publishing pipeline |
| Repo config | Wong | Issue labels, PR templates, dependabot, CODEOWNERS |
| Release automation | Wong | Version tagging, NuGet push, release notes |
| Session logging | Scribe | Automatic — never needs routing |

## Rules

1. **Eager by default** — spawn all agents who could usefully start work, including anticipatory downstream work.
2. **Scribe always runs** after substantial work, always as `mode: "background"`. Never blocks.
3. **Quick facts → coordinator answers directly.** Don't spawn an agent for "what port does the server run on?"
4. **When two agents could handle it**, pick the one whose domain is the primary concern.
5. **"Team, ..." → fan-out.** Spawn all relevant agents in parallel as `mode: "background"`.
6. **Anticipate downstream work.** If a feature is being built, spawn the tester to write test cases from requirements simultaneously.
