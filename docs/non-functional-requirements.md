# Non-Functional Requirements

Derived from [Senior_Software_Engineer_—_Take-Home.md](../Senior_Software_Engineer_—_Take-Home.md). Only requirements explicitly stated or directly implied by that document are listed here.

Note: A trailing `*` marks requirements added after the original take-home prompt.

## 1. Technology & Platform

- NFR-1.1: The backend must be implemented as a C# API. *
- NFR-1.2: The frontend must be implemented in React. *
- NFR-1.3: The project must be runnable locally via `docker compose up`. *
- NFR-1.4: Local development startup should require no more than one or two commands, with `docker compose up` as the primary path. *
- NFR-1.5: The datastore must be SQLite. *
- NFR-1.6: The datastore choice must not require a hosted cloud dependency for local development.

## 2. Security & Access

- NFR-2.1: No authentication/authorization is required.
- NFR-2.2: Capacity enforcement for registrations must occur on the server, not solely in the UI (i.e., it must not be bypassable via the client).

## 3. Data Integrity & Concurrency

- NFR-3.1: The system's capacity check for the "last seat" must remain correct under concurrent registration attempts (implied by the design write-up question: "what happens under concurrent registrations for the last seat?").
- NFR-3.2: Duplicate-registration cases must be handled sensibly (per evaluation criteria on correctness).

## 4. Extensibility / Maintainability

- NFR-4.1: The template system must be designed so that a 4th game type could be added without modifying core event logic.
- NFR-4.2: Template design should reflect genuine extensibility rather than conditional branching (if/else) on game names (per evaluation criteria).
- NFR-4.3: Game-type behavior must be template-driven and must not rely on hard-coded game-type strings in core logic.

## 5. Implementation Quality

- NFR-5.1: QR code generation must use a library, not a hand-rolled implementation.
- NFR-5.2: `.ics` calendar invite generation must use a library, not a hand-rolled implementation.

## 6. Documentation & Deliverables

- NFR-6.1: The code repository must be hosted on a public git host (GitHub, GitLab, BitBucket, or similar), not delivered as a zip/tar bundle.
- NFR-6.2: A `README.md` must be included describing how to run the project locally, preferably in one or two commands (a `docker compose up` or seed script is called out as a plus).
- NFR-6.3: The `README.md` must include a design write-up (~1 page) covering:
  - How capacity is determined and enforced, where it lives, and behavior under concurrent registrations for the last seat.
  - How the template system works and what adding a 4th game (or non-card game) would require.
  - What was deliberately cut or faked to stay within the timebox, and what would be built next.
- NFR-6.4: The `README.md` must include an AI usage note: which AI tools were used, for what purpose, and one example of AI output that was rejected or had to be fixed.
- NFR-6.5: Commit history is a factor in evaluation (per evaluation criteria on judgment), implying incremental, meaningful commits rather than a single dump.

## 7. Project Constraints

- NFR-7.1: The exercise is timeboxed to 3 hours; polish is not expected everywhere, but judgment about where time is spent is expected.
- NFR-7.2: If time runs out, the README must honestly document what does not work rather than shipping a broken feature silently.

## 8. Out of Evaluation Scope

Per the source document, the following are explicitly **not** evaluated:
- CSS polish
- Deployment
