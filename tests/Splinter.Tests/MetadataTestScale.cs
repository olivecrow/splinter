namespace Splinter.Tests;

// Routine regression exercises page traversal and reopen on bounded inputs.
// Capacity qualification is explicit and is never silently part of build.ps1.
internal static class MetadataTestScale
{
    internal static bool Large => Environment.GetEnvironmentVariable("SPLINTER_LARGE_CATALOG_TESTS") == "1";
    internal static int[] Counts => Large ? [32, 1024, 32769, 262145, 1048576] : [32, 1024];
}
