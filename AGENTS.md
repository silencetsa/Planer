# Planer development rules

- Implement a native Windows planner using C#, .NET 10, WinUI 3, MVVM and SQLite.
- Preserve the pastel weekly layout and the agreed behavior documented in `docs/requirements.md`.
- Work on `master`. One independently reviewable feature per commit.
- Update `Agnets.md` with completed work, validation, decisions and material blockers at each feature commit.
- Keep domain logic in Planer.Core, SQLite persistence in Planer.Data, presentation in Planer.App. Code-behind may implement view-only input, dialogs, drag/drop and window lifecycle; business rules belong to services and view models.
- Never commit the user's database, credentials, signing keys, caches or build output.
- Run meaningful tests for rollover, habit history, payment recurrence and historical preservation; build the Windows application and verify its rendered UI before delivery.
- No parallel agents are requested. Implement in this chat.
