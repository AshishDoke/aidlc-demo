# <ID> <Title>: Plan

## Tasks
- **T1**: <title> (covers AC1)
- **T2**: <title> (covers AC2, AC3)

## Plan data
```aidlc-plan
{
  "tasks": [
    {"id": "T1", "title": "<title>", "covers": ["AC1"]}
  ],
  "allowed_files": [
    "app/src/LeaveTracker.Api/**",
    "app/tests/LeaveTracker.Tests/**"
  ],
  "test_command": "dotnet test app/LeaveTracker.sln"
}
```
