using Xunit;

// One database file, one writer at a time.
//
// These are integration tests over a real SQLite file, and SQLite in its default
// rollback-journal mode gives a writer an exclusive lock on the whole database. Run in
// parallel, test classes collide on it and fail with "database is locked" — not a flaw in
// what is being tested, just several processes' worth of concurrency aimed at a file the
// deployed game only ever has one writer for.
//
// Write-ahead logging would allow that concurrency, and is deliberately not used: staging
// keeps its file on App Service's /home, which is a network share, and WAL's index needs
// shared memory. Rather than let the tests run in a mode the game never runs in, they run
// one at a time.
//
// The suite is a handful of seconds either way, so this costs little and removes a whole
// class of flakiness that has nothing to do with the rules under test.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
