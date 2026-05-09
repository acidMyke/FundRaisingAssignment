# Task: Add user-story attribution comments to the codebase for report generation

You are a senior C# developer working on a .NET 10 ASP.NET Core project (`FundRaisingAssignment`) for a university software methodologies assignment (CSIT314). The team has six members, and the final report must demonstrate clear traceability between user stories, group members, and the actual code that implements each story.

Your job is NOT to change behaviour. Your job is to **annotate the existing codebase** with structured XML-doc comments and inline markers so that:

1. Each section of code is clearly attributed to the user story it implements.
2. Each user story is clearly attributed to the team member who owns it.
3. The comments are extractable later for a report appendix (BCE traceability matrix, individual contributions, etc.).
4. The comments are useful for code review and onboarding teammates — not just paperwork.

---

## Team members and their user stories

Use these exact names and IDs in every comment. Do not abbreviate, do not reorder, do not invent new IDs.

| Member | Story ID | Story Name |
|---|---|---|
| **Ho Dan Ze** | DN05 | View Donation Receipt |
| **Ho Dan Ze** | PM06 | View Top Donors Leaderboard |
| **Yong Jun Jie (JJ)** | FR04 | Campaign Access Delegation |
| **Zhu Jianshan (Josh)** | FR01 | Set Funding Goal and Deadline |
| **Zhu Jianshan (Josh)** | PM01 | Review Flagged Campaign |
| **Unnikrishna Pillai Karthik** | DN02 | Set Donation Budget and Target |
| **Unnikrishna Pillai Karthik** | UA02 | Export Platform Performance and Financial Report |
| **Khoo Shi Hao Nicholas** | UA01 | User Admin – Add and Manage Users |
| **Khoo Shi Hao Nicholas** | FR03 | Send Thank-You Message to Donors |
| **Khoo Si Kai** | DN01 | Search Fundraising Campaigns |
| **Khoo Si Kai** | PM05 | View Platform Analytics Dashboard |
| **Shared (consolidated)** | DN03 | Make a Donation to a Campaign |

> Note: DN03 is the consolidated donation flow that resulted from refactoring four duplicate implementations (originally split between Josh, Karthik, and others). Mark it as **shared** and list contributors in the comment.

---

## Comment format — REQUIRED structure

Use ONE of two block formats, depending on context:

### Format A — File-level header (top of every relevant `.cs` / `.cshtml.cs` file)

Place this immediately after the `using` directives, before the namespace:

```csharp
// ─────────────────────────────────────────────────────────────────────────────
// User Story:    – 
// Owner:        
// BCE Role:     
// Description:  <1–2 sentences explaining what this file contributes to the story.>
// Notes:        
// ─────────────────────────────────────────────────────────────────────────────
```

If a file contributes to MORE THAN ONE story (this is common for shared services and pages), list each story as a separate `User Story:` line under one block, with its own Owner. Example:

```csharp
// ─────────────────────────────────────────────────────────────────────────────
// User Story:   DN03 – Make a Donation to a Campaign        Owner: Shared
// User Story:   FR01 – Set Funding Goal and Deadline        Owner: Zhu Jianshan (Josh)
// User Story:   PM01 – Review Flagged Campaign              Owner: Zhu Jianshan (Josh)
// BCE Role:     Control
// Description:  Application service exposing campaign lifecycle and donation operations.
// Notes:        DN03 was consolidated from four duplicate implementations; see
//               git history and the Final Report § "Donation flow consolidation".
// ─────────────────────────────────────────────────────────────────────────────
```

### Format B — Inline region marker (around a specific method or block within a multi-story file)

When a single file contains code from multiple stories, wrap each contribution in `#region` markers AND add a brief XML doc comment:

```csharp
#region DN02 – Set Donation Budget and Target (Karthik)
/// 
/// Persists the donee's budget and target, recomputes status from donation
/// history, and returns the snapshot for display.
/// 
/// 
/// User Story: DN02 — Set Donation Budget and Target.
/// Owner: Unnikrishna Pillai Karthik.
/// Sub-flows handled: 4a (budget-only update), 7a (target-only update),
/// 9a (no donation records), 12a (budget exceeded), 13a (target progress),
/// 15a (system processing failure).
/// 
public async Task OnPostSaveAsync()
{
    // ...
}
#endregion
```

Use Format B inside `Program.cs`, `ApplicationDbContext.cs`, `ICampaignService.cs`, `CampaignService.cs`, and any other shared file where multiple owners contributed.

---

## What to comment — file inventory

Apply Format A or Format B (as appropriate) to every file in this list. If a file is not in the list, leave it alone.

### Dan Ze (DN05, PM06)
- `Areas/Campaigns/Pages/DonationConfirmation.cshtml.cs` — DN05, Boundary
- `Areas/Dashboard/Pages/DonationHistoryPage.cshtml` and `.cshtml.cs` — DN05, Boundary
- Any `TopDonors` / `Leaderboard` page if it exists — PM06, Boundary. If not present, add a comment in `Program.cs` under a `#region PM06 (Pending — owner: Ho Dan Ze)` marker noting that the story is unimplemented.

### JJ (FR04)
- Any `Delegate*` or `CampaignAccess*` files — FR04, all roles. If none exist, add a `#region FR04 (Pending — owner: Yong Jun Jie)` placeholder in `Program.cs` flagging the gap.

### Josh (FR01, PM01)
- `Areas/Dashboard/Pages/CreateCampaign.cshtml.cs` and `UpdateCampaign.cshtml.cs` — FR01, Boundary
- `Areas/Dashboard/Pages/FlaggedCampaigns.cshtml.cs` — PM01, Boundary
- `Areas/Dashboard/Pages/ManageCampaigns.cshtml.cs` — PM01, Boundary
- `Models/Campaign.cs` — FR01 (status transitions, funding goal fields), Entity. Use Format B to mark the `SubmitForReview`, `PublishCampaign`, `FlagCampaignByAdmin`, `PauseCampaign`, `TerminateCampaign`, `ReleaseCampaign` methods individually under PM01 / FR01 regions.

### Karthik (DN02, UA02)
- `Pages/MyBudget.cshtml.cs` — DN02, Boundary + Control
- `Models/DonationGoal.cs` (and its enums `BudgetStatus`, `TargetStatus`) — DN02, Entity
- `Areas/Internal/Pages/Reports.cshtml.cs` — UA02, Boundary + Control
- `Models/ExportFile.cs` — UA02, Entity
- `Areas/Campaigns/Pages/Index.cshtml.cs` — search-mode block belongs to DN01 (Si Kai); use Format B to attribute the search filter to Karthik's PR if he authored that branch, otherwise to Si Kai per the table above.

### Nicholas (UA01, FR03)
- `Areas/Internal/Pages/Users/Index.cshtml.cs`, `Create.cshtml.cs`, `Edit.cshtml.cs`, `Delete.cshtml.cs` — UA01, Boundary
- `Areas/Internal/Pages/FundraiserApprovals.cshtml.cs` — UA01 extension, Boundary. Note in the comment that this was added beyond the original story to support the pending-fundraiser registration pipeline.
- Any `ThankYou*` page if it exists — FR03. If not present, add a `#region FR03 (Pending — owner: Khoo Shi Hao Nicholas)` marker in `Program.cs`. Mention that `MailjetEmailService` is already wired and ready to be reused.

### Si Kai (DN01, PM05)
- `Areas/Campaigns/Pages/Index.cshtml.cs` — DN01 (the search filter logic), Boundary + Control
- Any analytics-dashboard page — PM05. If not present, add `#region PM05 (Pending — owner: Khoo Si Kai)` in `Program.cs`.

### Shared / consolidated (DN03)
- `Services/ICampaignService.cs` — DN03 + FR01 + PM01, Control. Use Format B to attribute the `DonateAsync` line to DN03 (Shared) and the campaign-lifecycle methods to Josh.
- `Services/CampaignService.cs` — same as above; mark each method group with a `#region`.
- `Services/DonationContracts.cs` (the file holding `MakeDonationInput` / `DonationResult`) — DN03, Control.
- `Areas/Dashboard/Pages/CampaignPage.cshtml.cs` — DN03 (donation form) + Reviews (Josh's extension). Use Format B for each.
- `Areas/Campaigns/Pages/Details.cshtml.cs` — DN03, Boundary.
- `Areas/Donations/Pages/Create.cshtml.cs` — DN03, Boundary. Add a `Notes:` line stating "Refactored from a vulnerable direct-bind implementation; now uses canonical ICampaignService.DonateAsync."
- `Controllers/DonationsController.cs` — DN03, Boundary. Add a `Notes:` line stating "Web API entry point; mirrors the Razor Page boundaries that all funnel through ICampaignService.DonateAsync."
- `Models/Donation.cs` — DN03, Entity.
- `FundRaisingAssignment.Test/CampaignServiceDonateTests.cs` (or whatever the file is now called) — DN03, supporting tests.

### Cross-cutting (no single owner)
- `Program.cs` — Use Format B with a top-of-file Format A header attributing the file to "Team (cross-cutting)". Then mark each `AddScoped` / `AddTransient` registration with a one-line trailing comment naming the story it supports, e.g.:
```csharp
  builder.Services.AddScoped();   // DN03, FR01, PM01 — backbone service
```
- `Data/ApplicationDbContext.cs` — Use Format A as "Team (cross-cutting)". Inside `OnModelCreating`, use Format B `#region` blocks per entity (e.g., `#region Donation (DN03)`, `#region Campaign (FR01, PM01)`, `#region DonationGoal (DN02)`).

---

## Rules

1. **Do not change behaviour.** Comments only. No method bodies modified, no signatures changed, no usings added beyond what comments require (none should).
2. **Do not duplicate effort.** If a file already has a partial attribution comment (e.g., the existing `// (Karthik)` and `// (Josh)` markers in `Areas/Campaigns/Pages/Index.cshtml.cs`), upgrade it to the canonical Format A or Format B — do not stack a second comment on top.
3. **Be honest about gaps.** If a story has no implementation in the codebase, do not invent attributions. Add a `#region <ID> (Pending — owner: <Name>)` marker in `Program.cs` listing the unimplemented stories. The Final Report needs this gap analysis.
4. **Keep `Notes:` lines factual.** Acceptable: "Refactored to remove over-posting vulnerability." Not acceptable: "This is the cleanest code in the project."
5. **Match the team's existing voice.** The codebase already has comments like `// Josh's interface` and `// Karthik's donation service` in `Program.cs`. Replace those casual markers with the structured format, but keep the tone unfussy.
6. **Use ASCII box-drawing characters** (`─`) consistently in Format A headers — don't mix `=`, `-`, and `─`.

---

## Verification

After completing the annotation pass, output:

1. A **traceability matrix** as a markdown table for the report appendix:

   | Story ID | Story Name | Owner | Files Touched | BCE Coverage | Status |
   |---|---|---|---|---|---|
   | DN01 | Search Fundraising Campaigns | Khoo Si Kai | `Areas/Campaigns/Pages/Index.cshtml.cs` | B + C | ✅ Implemented |
   | ... | ... | ... | ... | ... | ... |

   "BCE Coverage" lists which roles (B/C/E) the listed files contribute to. "Status" is one of: ✅ Implemented · 🟡 Partial · 🔴 Pending.

2. A **gap list** — every story marked Pending or Partial, with a one-line note on what's missing.

3. A **diff summary** — number of files modified, total lines of comments added. Do NOT print the full diff; the human will get that from `git diff`.

---

## Output

Walk the file inventory in the order given above. For each file:

1. State the path.
2. State whether you applied Format A, Format B, or both.
3. Show the comment block(s) you added — but NOT the rest of the file.
4. If the file required a `#region` rearrangement, state which methods now sit under which region, in one line per region.

End with the three verification artifacts (matrix, gap list, diff summary).

How to use this prompt
Best fit: paste it into Cursor or Claude Code with the repo open, since the agent can navigate the file inventory directly. It also works in a fresh chat session if you paste the relevant files alongside it.
Two things to add before you send, if you want the cleanest result:

A pre-flight constraint at the very top: "Before doing any work, list every file in the inventory you cannot find in the workspace. Pause and report. Do not invent paths." — this catches stale paths if the team has reorganised since this audit.
The output format you want for the report: if your final report uses LaTeX, ask the agent to also emit the traceability matrix in \begin{tabular} form. If it's Word, the markdown table pastes cleanly.

The matrix that comes back from this prompt is gold for two parts of your CSIT314 marking scheme: "Analysis and Design — consistency between design and code" (because every story now points at a file) and "Effective use of methodologies — weekly progress as observed and noted" (because contributions are individually traceable).
Want me to also draft a follow-up prompt that extracts these comments back out into a single CONTRIBUTIONS.md file at the end of the project, so you don't have to copy-paste from across the repo for the final report?You said: format this for github's readmeAnnotation walkthrough
For each file: path, format applied, and the block(s) added (comment text only — surrounding code omitted).

Dan Ze (DN05, PM06)
1. FundRaisingAssignment.Application/Areas/Campaigns/Pages/DonationConfirmation.cshtml.cs — Format A


// User Story:   DN05 – View Donation pastedformat this for github's readme11:40 PMClaude responded: Here's a polished GitHub README.Here's a polished GitHub README. I'd recommend dropping it at docs/CONTRIBUTIONS.md (or README.md if your repo is just this assignment), since this is contribution-traceability content rather than a setup guide.
markdown# Contributions & User Story Traceability

> CSIT314 Software Development Methodologies — Group Project (SIM S2 2026)
> Annotation pass linking every implemented user story to its owner, files, and BCE coverage.

This document is the source of truth for **who built what** in this codebase. Every annotated file in the project carries an inline header comment matching the entries below, so traceability holds in both directions: code → owner, and owner → code.

---

## Table of Contents

- [Team & Stories](#team--stories)
- [Traceability Matrix](#traceability-matrix)
- [Implementation Gaps](#implementation-gaps)
- [Annotation Format](#annotation-format)
- [File-by-File Annotation Log](#file-by-file-annotation-log)
- [Annotation Pass Summary](#annotation-pass-summary)

---

## Team & Stories

| Member | Stories Owned |
|---|---|
| Ho Dan Ze | DN05, PM06 |
| Yong Jun Jie (JJ) | FR04 |
| Zhu Jianshan (Josh) | FR01, PM01 |
| Unnikrishna Pillai Karthik | DN02, UA02 |
| Khoo Shi Hao Nicholas | UA01, FR03 |
| Khoo Si Kai | DN01, PM05 |
| Shared (consolidated) | DN03 |

> **DN03** is the consolidated donation flow that resulted from refactoring four duplicate implementations originally split between Josh, Karthik, and others. See [`Services/CampaignService.cs`](FundRaisingAssignment.Application/Services/CampaignService.cs) and the `Donations/Shared` region for the consolidation history.

---

## Traceability Matrix

| Story ID | Story Name | Owner | BCE Coverage | Status |
|---|---|---|---|---|
| **DN01** | Search Fundraising Campaigns | Khoo Si Kai | B + C | ✅ Implemented |
| **DN02** | Set Donation Budget and Target | Unnikrishna Pillai Karthik | B + C + E | ✅ Implemented |
| **DN03** | Make a Donation to a Campaign | Shared (Josh + Karthik consolidated) | B + C + E + Tests | ✅ Implemented |
| **DN05** | View Donation Receipt | Ho Dan Ze | B | ✅ Implemented |
| **FR01** | Set Funding Goal and Deadline | Zhu Jianshan (Josh) | B + C + E | ✅ Implemented |
| **FR03** | Send Thank-You Message to Donors | Khoo Shi Hao Nicholas | B | ✅ Implemented |
| **FR04** | Campaign Access Delegation | Yong Jun Jie (JJ) | — | 🔴 Pending |
| **PM01** | Review Flagged Campaign | Zhu Jianshan (Josh) | B + C + E | ✅ Implemented |
| **PM05** | View Platform Analytics Dashboard | Khoo Si Kai | — | 🔴 Pending |
| **PM06** | View Top Donors Leaderboard | Ho Dan Ze | B (partial) + C | 🟡 Partial |
| **UA01** | User Admin – Add and Manage Users | Khoo Shi Hao Nicholas | B | ✅ Implemented |
| **UA02** | Export Platform Performance and Financial Report | Unnikrishna Pillai Karthik | B + C + E + Tests | ✅ Implemented |

**Legend:** ✅ Implemented · 🟡 Partial · 🔴 Pending · **B** = Boundary · **C** = Control · **E** = Entity

### Story → Files mapping

<details>
<summary><b>DN01 — Search Fundraising Campaigns</b> (Khoo Si Kai)</summary>

- `Areas/Campaigns/Pages/Index.cshtml.cs`
- `Services/ICampaignService.cs` — `SearchCampaignsAsync`
- `Services/CampaignService.cs` — `SearchCampaignsAsync`
- `Models/Campaign.cs` — `Location` field

</details>

<details>
<summary><b>DN02 — Set Donation Budget and Target</b> (Unnikrishna Pillai Karthik)</summary>

- `Pages/MyBudget.cshtml.cs`
- `Models/DonationGoal.cs`
- `Data/ApplicationDbContext.cs` — `DonationGoal` and `Donee` regions

</details>

<details>
<summary><b>DN03 — Make a Donation to a Campaign</b> (Shared)</summary>

- `Services/ICampaignService.cs` — `DonateAsync`
- `Services/CampaignService.cs` — `DonateAsync`, refund logic
- `Services/DonationContracts.cs`
- `Models/Donation.cs`
- `Areas/Campaigns/Pages/Details.cshtml.cs`
- `Areas/Donations/Pages/Create.cshtml.cs`
- `Areas/Dashboard/Pages/CampaignPage.cshtml.cs`
- `Controllers/DonationsController.cs`
- `FundRaisingAssignment.Test/CampaignServiceDonateTests.cs`
- `Data/ApplicationDbContext.cs` — `Donation` region

</details>

<details>
<summary><b>DN05 — View Donation Receipt</b> (Ho Dan Ze)</summary>

- `Areas/Campaigns/Pages/DonationConfirmation.cshtml.cs`
- `Areas/Dashboard/Pages/DonationHistoryPage.cs`

</details>

<details>
<summary><b>FR01 — Set Funding Goal and Deadline</b> (Zhu Jianshan / Josh)</summary>

- `Areas/Dashboard/Pages/CreateCampaign.cshtml.cs`
- `Areas/Dashboard/Pages/UpdateCampaign.cshtml.cs`
- `Models/Campaign.cs` — `SubmitForReview`, `UpdateGoalAndDeadline`
- `Services/ICampaignService.cs` — CRUD region
- `Services/CampaignService.cs` — CRUD region

</details>

<details>
<summary><b>FR03 — Send Thank-You Message to Donors</b> (Khoo Shi Hao Nicholas)</summary>

- `Areas/Dashboard/Pages/ThankDonors.cshtml.cs`
- `Services/MailjetEmailService.cs` (registered in `Program.cs`)

</details>

<details>
<summary><b>PM01 — Review Flagged Campaign</b> (Zhu Jianshan / Josh)</summary>

- `Areas/Dashboard/Pages/FlaggedCampaigns.cshtml.cs`
- `Areas/Dashboard/Pages/ManageCampaigns.cshtml.cs`
- `Models/Campaign.cs` — lifecycle methods
- `Services/ICampaignService.cs` — admin / reviews regions
- `Services/CampaignService.cs` — admin / reviews regions
- `Data/ApplicationDbContext.cs` — `CampaignReview`, `FundRaiserNotification` regions

</details>

<details>
<summary><b>PM06 — View Top Donors Leaderboard</b> (Ho Dan Ze) 🟡 Partial</summary>

- `Services/ICampaignService.cs` — `GetTopDonationsAsync`
- `Services/CampaignService.cs` — `GetTopDonationsAsync`
- `Areas/Dashboard/Pages/CampaignPage.cshtml.cs` — inline "Top Donations" tab
- `Program.cs` — `#region PM06 (Partial)`

</details>

<details>
<summary><b>UA01 — User Admin: Add and Manage Users</b> (Khoo Shi Hao Nicholas)</summary>

- `Areas/Internal/Pages/Users/Index.cshtml.cs`
- `Areas/Internal/Pages/Users/Create.cshtml.cs`
- `Areas/Internal/Pages/Users/Edit.cshtml.cs`
- `Areas/Internal/Pages/Users/Delete.cshtml.cs`
- `Areas/Internal/Pages/FundraiserApprovals.cshtml.cs` — *added beyond original scope to support the registration → pending-fundraiser → approval pipeline*
- `Program.cs` — Identity registration

</details>

<details>
<summary><b>UA02 — Export Platform Performance and Financial Report</b> (Unnikrishna Pillai Karthik)</summary>

- `Areas/Internal/Pages/Reports.cshtml.cs`
- `Areas/Internal/Pages/Reports.cshtml`
- `Models/PlatformReport.cs`
- `Models/ExportFile.cs`
- `Data/ApplicationDbContext.cs` — `ExportFile` region
- `FundRaisingAssignment.Test/ReportExporterTests.cs`

</details>

---

## Implementation Gaps

| Story | Owner | Status | What's missing |
|---|---|---|---|
| **FR04** — Campaign Access Delegation | Yong Jun Jie (JJ) | 🔴 No implementation | No `Delegate*` / `CampaignAccess*` files exist. No entity for delegated ownership or co-fundraiser permissions; no Razor pages or DI registrations. The current `Campaign.OwnerId` is single-valued. A `#region FR04 (Pending)` marker is in `Program.cs` so the Final Report can flag this as a clean gap. |
| **PM05** — Platform Analytics Dashboard | Khoo Si Kai | 🔴 No standalone page | UA02 (Karthik's `Reports.cshtml`) covers ad-hoc admin reporting with charts, but there is no always-on analytics dashboard. No `Analytics*` files exist. Marker added in `Program.cs`. |
| **PM06** — Top Donors Leaderboard | Ho Dan Ze | 🟡 Partial | Leaderboard data is exposed via `ICampaignService.GetTopDonationsAsync` and surfaced as the "Top Donations" tab inline on `Areas/Dashboard/Pages/CampaignPage.cshtml`. There is no standalone `/Leaderboard` page or platform-wide top-donors view. Marker added in `Program.cs`. |

---

## Annotation Format

Two complementary comment formats are used throughout the codebase. Pick the right one based on context.

### Format A — File-level header

Sits between the `using` directives and the `namespace` declaration:

```csharp
// ─────────────────────────────────────────────────────────────────────────────
// User Story:    – 
// Owner:        
// BCE Role:     
// Description:  <1–2 sentences explaining what this file contributes.>
// Notes:        
// ─────────────────────────────────────────────────────────────────────────────
```

When a file contributes to **multiple** stories, list each on its own `User Story:` line under one block:

```csharp
// ─────────────────────────────────────────────────────────────────────────────
// User Story:   DN03 – Make a Donation to a Campaign        Owner: Shared
// User Story:   FR01 – Set Funding Goal and Deadline        Owner: Zhu Jianshan (Josh)
// User Story:   PM01 – Review Flagged Campaign              Owner: Zhu Jianshan (Josh)
// BCE Role:     Control
// Description:  Application service exposing campaign lifecycle and donation operations.
// Notes:        DN03 was consolidated from four duplicate implementations; see
//               git history and the Final Report § "Donation flow consolidation".
// ─────────────────────────────────────────────────────────────────────────────
```

### Format B — Inline `#region` marker

Used inside files containing code from multiple owners. Wraps each contribution in `#region` markers and adds an XML-doc `<remarks>` block:

```csharp
#region DN02 – Set Donation Budget and Target (Karthik)
/// 
/// Persists the donee's budget and target, recomputes status from donation
/// history, and returns the snapshot for display.
/// 
/// 
/// User Story: DN02 — Set Donation Budget and Target.
/// Owner: Unnikrishna Pillai Karthik.
/// Sub-flows handled: 4a, 7a, 9a, 12a, 13a, 15a.
/// 
public async Task OnPostSaveAsync()
{
    // ...
}
#endregion
```

---

## File-by-File Annotation Log

### 🟦 Ho Dan Ze — DN05, PM06

| # | File | Format | Notes |
|---|---|---|---|
| 1 | `Areas/Campaigns/Pages/DonationConfirmation.cshtml.cs` | A | DN05 — Boundary. Read-only post-donation receipt. |
| 2 | `Areas/Dashboard/Pages/DonationHistoryPage.cs` | A | DN05 — Boundary. Donor-facing history with drilldown. |

> **PM06** has no dedicated leaderboard page. It is surfaced inline on `CampaignPage.cshtml` and via `ICampaignService.GetTopDonationsAsync`. A `#region PM06 (Partial)` marker was added at the bottom of `Program.cs`.

### 🟦 Yong Jun Jie (JJ) — FR04

> No `Delegate*` or `CampaignAccess*` files exist. A `#region FR04 (Pending)` marker was added to `Program.cs`.

### 🟦 Zhu Jianshan (Josh) — FR01, PM01

| # | File | Format | Notes |
|---|---|---|---|
| 3 | `Areas/Dashboard/Pages/CreateCampaign.cshtml.cs` | A | FR01 — Boundary. Status forced to `Draft` server-side. |
| 4 | `Areas/Dashboard/Pages/UpdateCampaign.cshtml.cs` | A | FR01 — Boundary. Excludes `Flagged` / `PendingReview` from manual selection. |
| 5 | `Areas/Dashboard/Pages/FlaggedCampaigns.cshtml.cs` | A | PM01 — Boundary. Maps to BCE Diagram 2. |
| 6 | `Areas/Dashboard/Pages/ManageCampaigns.cshtml.cs` | A | PM01 — Boundary. Lifecycle actions: Publish / Flag / Pause / Release / Terminate. |
| 7 | `Models/Campaign.cs` | A + B | Two regions: `FR01` wraps `SubmitForReview` and `UpdateGoalAndDeadline`; `PM01` wraps lifecycle methods. |

### 🟦 Unnikrishna Pillai Karthik — DN02, UA02

| # | File | Format | Notes |
|---|---|---|---|
| 8 | `Pages/MyBudget.cshtml.cs` | A | DN02 — Boundary + Control. Sub-flows: 4a, 7a, 9a, 12a, 13a, 15a. |
| 9 | `Models/DonationGoal.cs` | A | DN02 — Entity. |
| 10 | `Areas/Internal/Pages/Reports.cshtml.cs` | A | UA02 — Boundary + Control. Casual existing comment upgraded. |
| 11 | `Models/ExportFile.cs` | A | UA02 — Entity. |

### 🟦 Khoo Si Kai — DN01

| # | File | Format | Notes |
|---|---|---|---|
| 12 | `Areas/Campaigns/Pages/Index.cshtml.cs` | A | DN01 — Boundary + Control. Casual `// (Karthik)` / `// (Josh)` markers replaced; Notes line credits Josh's grid styling. |

> **PM05** — no analytics dashboard page. Pending region added in `Program.cs`.

### 🟦 Khoo Shi Hao Nicholas — UA01, FR03

| # | File | Format | Notes |
|---|---|---|---|
| 13 | `Areas/Internal/Pages/Users/Index.cshtml.cs` | A | UA01 — Boundary. |
| 14 | `Areas/Internal/Pages/Users/Create.cshtml.cs` | A | UA01 — Boundary. |
| 15 | `Areas/Internal/Pages/Users/Edit.cshtml.cs` | A | UA01 — Boundary. |
| 16 | `Areas/Internal/Pages/Users/Delete.cshtml.cs` | A | UA01 — Boundary. |
| 17 | `Areas/Internal/Pages/FundraiserApprovals.cshtml.cs` | A | UA01 — Boundary. *Added beyond original UA01 scope to support the registration → pending-fundraiser → approval pipeline.* |
| 18 | `Areas/Dashboard/Pages/ThankDonors.cshtml.cs` | A | FR03 — Boundary. Reuses `MailjetEmailService` from `Program.cs`; no new transport code. |

### 🟦 Shared / Consolidated — DN03

| # | File | Format | Notes |
|---|---|---|---|
| 19 | `Services/ICampaignService.cs` | A + B | Header lists DN03 (Shared), FR01 (Josh), PM01 (Josh), DN01 (Si Kai). Methods grouped under `#region` per story. `DonateAsync` carries a `<remarks>` block attributing it to DN03 (Shared). |
| 20 | `Services/CampaignService.cs` | A + B | Mirrors the interface region layout. `DonateAsync` and `GetTopDonationsAsync` carry attribution `<remarks>`. |
| 21 | `Services/DonationContracts.cs` | A | DN03 — Control. |
| 22 | `Areas/Dashboard/Pages/CampaignPage.cshtml.cs` | A | Header lists DN03 (donate form), PM01 (reviews form), PM06 (Top Donations tab). |
| 23 | `Areas/Campaigns/Pages/Details.cshtml.cs` | A | DN03 — Boundary. |
| 24 | `Areas/Donations/Pages/Create.cshtml.cs` | A | DN03 — Boundary. *Refactored from a vulnerable direct-bind implementation; now uses the canonical `ICampaignService.DonateAsync`.* |
| 25 | `Controllers/DonationsController.cs` | A | DN03 — Boundary. *Web API entry point; mirrors the Razor Page boundaries that all funnel through `ICampaignService.DonateAsync`.* |
| 26 | `Models/Donation.cs` | A | DN03 — Entity. |
| 27 | `FundRaisingAssignment.Test/CampaignServiceDonateTests.cs` | A | DN03 — supporting tests. |

> `Controllers/DonationRecordController.cs` was not in the annotation inventory and was not annotated.

### 🟦 Cross-cutting — Team

| # | File | Format | Notes |
|---|---|---|---|
| 28 | `Program.cs` | A + B | File header attributes file to "Team (cross-cutting)". Each `AddDbContext` / `AddDefaultIdentity` / `AddTransient` / `AddScoped` / `AddRazorPages` / `ExcelPackage.License` line carries a trailing `// <story> — <reason>` comment. Three pending regions appended at end: `FR04`, `PM05`, `PM06`. The previous casual `// canonical campaign + donation service` was replaced by `// DN01, DN03, FR01, PM01, PM06 — backbone service`. |
| 29 | `Data/ApplicationDbContext.cs` | A + B | Header attributes the file to "Team (cross-cutting)". `DbSet` declarations now carry trailing per-story comments. Inside `OnModelCreating`, each entity block is wrapped in a region: `Campaign (FR01, PM01)`, `Donation (DN03)`, `DonationGoal (DN02)`, `Donee (DN02)`, `ExportFile (UA02)`, `RefundLog (DN03 — refund pipeline)`, `CampaignReview (PM01)`, `FundRaiserNotification (PM01)`. The casual `// ── Karthik's DbSets ──` and `// ── Josh's DbSets ──` separators were replaced. |

---

## Annotation Pass Summary

| Metric | Count |
|---|---|
| Files touched | **29** |
| Format A headers added | 27 |
| Format B `#region` blocks added | 29 (across `Campaign.cs`, `ICampaignService.cs`, `CampaignService.cs`, `Program.cs`, `ApplicationDbContext.cs`) |
| Trailing per-line story tags | 14 in `Program.cs`, 8 on `DbSet` lines in `ApplicationDbContext.cs` |
| Pending markers | 3 (FR04, PM05, PM06) at the bottom of `Program.cs` |
| Build status | ✅ Application + Test projects compile clean (0 warnings, 0 errors) |
| Behavioural changes | **None** — comments only. No method bodies modified, no signatures changed, no 
