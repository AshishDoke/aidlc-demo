# Team Leave Tracker: requirements

Product: a small internal web app where employees request leave and managers approve or reject it.

Build them **in order**. Each one is a separate AIDLC run with its own gates and MR.
The engine refuses to start a requirement until its `depends_on` items are merged into `main`.

| ID | Section | Depends on |
|---|---|---|
| [LT-001](LT-001.md) | Database, data model and solution skeleton | none |
| [LT-002](LT-002.md) | Backend REST API | LT-001 |
| [LT-003](LT-003.md) | Frontend: employee view | LT-002 |
| [LT-004](LT-004.md) | Frontend: manager view | LT-002 |
| [LT-005](LT-005.md) | Business rules and validation | LT-003, LT-004 |

The texts are deliberately written the way real tickets are: mostly clear, with a few gaps.
The ANALYZE stage is expected to surface those gaps as open questions at gate G1.
