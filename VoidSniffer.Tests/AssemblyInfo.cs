using Xunit;

// The active language is global state; run tests sequentially so language-switching tests
// cannot race with the formatter tests that expect the default (Russian) pack.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
