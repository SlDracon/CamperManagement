# PDF regression checks

Run from the repository root with the .NET 10 SDK:

```sh
dotnet run --project tests/CamperManagement.RegressionTests
```

The executable exits with a nonzero status when a check fails. It exercises real PDF generation with isolated storage-provider and launcher substitutes. No UI, printer or database connection is required. It checks selected filenames and Unicode paths, cancellation, save-dialog thread affinity, selection snapshots, temporary-file cleanup on success and failure, and opening the selected storage object.

Responsiveness checks run every export in a single-thread synchronization context. A deliberately blocked PDF output stream verifies that UI callbacks continue while the renderer writes; storage-provider calls and progress callbacks must stay on the UI thread.
