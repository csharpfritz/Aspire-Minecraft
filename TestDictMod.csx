var dict = new Dictionary<string, string> { ["a"] = "1", ["b"] = "2", ["c"] = "3" };
try
{
    foreach (var (key, value) in dict)
    {
        if (key == "b")
            dict[key] = "modified";
    }
    Console.WriteLine("No exception");
}
catch (InvalidOperationException ex)
{
    Console.WriteLine("InvalidOperationException: " + ex.Message);
}
